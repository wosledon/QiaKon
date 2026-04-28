using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QiaKon.Connector.Http;

/// <summary>
/// HTTP 连接器实现（配置驱动）
/// </summary>
public sealed class HttpConnector : ConnectorBase, IHttpConnector
{
    private readonly HttpConnectorOptions _options;
    private readonly Dictionary<string, HttpEndpointConfig> _endpoints = new();
    private HttpClient? _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public HttpConnector(HttpConnectorOptions options)
        : base(options.Name, ConnectorType.Http)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new ArgumentException("BaseUrl cannot be null or empty", nameof(options));
        }

        // 注册所有端点
        foreach (var endpoint in options.Endpoints)
        {
            _endpoints[endpoint.Name] = endpoint;
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <inheritdoc />
    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        State = ConnectorState.Connecting;

        try
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = _options.MaxConnections
            };

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_options.BaseUrl),
                Timeout = TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds)
            };

            // 设置默认请求头
            foreach (var (key, value) in _options.DefaultHeaders)
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
            }

            State = ConnectorState.Connected;
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            State = ConnectorState.Unhealthy;
            throw new ConnectorException($"Failed to initialize HTTP connector '{Name}'", ex);
        }
    }

    /// <inheritdoc />
    public async Task<ConnectorResponse> ExecuteAsync(
        string endpointName,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        if (!_endpoints.TryGetValue(endpointName, out var endpoint))
        {
            throw new KeyNotFoundException($"Endpoint '{endpointName}' not found in connector '{Name}'");
        }

        var request = BuildHttpRequest(endpoint, parameters);

        try
        {
            var response = await _httpClient!.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            var headers = response.Headers
                .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

            if (IsSuccessStatusCode(response.StatusCode, endpoint.SuccessStatusCodes))
            {
                object? data = content;

                // 如果指定了响应数据路径，提取数据
                if (!string.IsNullOrEmpty(endpoint.ResponseDataPath) &&
                    !string.IsNullOrEmpty(content))
                {
                    using var doc = JsonDocument.Parse(content);
                    var element = ExtractJsonPath(doc.RootElement, endpoint.ResponseDataPath);
                    data = element.GetRawText();
                }

                return new ConnectorResponse(
                    IsSuccess: true,
                    Data: data,
                    StatusCode: (int)response.StatusCode,
                    Headers: headers);
            }
            else
            {
                return new ConnectorResponse(
                    IsSuccess: false,
                    ErrorMessage: $"HTTP {response.StatusCode}: {content}",
                    StatusCode: (int)response.StatusCode,
                    Headers: headers);
            }
        }
        catch (Exception ex)
        {
            return new ConnectorResponse(
                IsSuccess: false,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<T?> ExecuteAsync<T>(
        string endpointName,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var response = await ExecuteAsync(endpointName, parameters, cancellationToken);

        if (!response.IsSuccess || response.Data is null)
        {
            throw new ConnectorException($"HTTP request failed: {response.ErrorMessage}");
        }

        if (response.Data is string jsonString)
        {
            return JsonSerializer.Deserialize<T>(jsonString, _jsonOptions);
        }

        return response.Data as T;
    }

    /// <inheritdoc />
    public override Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureConnected();
            return Task.FromResult(new HealthCheckResult(true, "Connected"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new HealthCheckResult(false, ex.Message));
        }
    }

    /// <inheritdoc />
    public override Task CloseAsync(CancellationToken cancellationToken = default)
    {
        State = ConnectorState.Disconnected;
        _httpClient?.Dispose();
        _httpClient = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await CloseAsync(CancellationToken.None);
        await base.DisposeAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        CloseAsync(CancellationToken.None).GetAwaiter().GetResult();
        base.Dispose();
    }

    /// <summary>
    /// 构建 HTTP 请求
    /// </summary>
    private HttpRequestMessage BuildHttpRequest(HttpEndpointConfig endpoint, IDictionary<string, object>? parameters)
    {
        parameters ??= new Dictionary<string, object>();

        // 替换 URL 模板变量
        var url = ReplaceTemplateVariables(endpoint.Url, parameters);

        // 构建查询参数
        var queryString = BuildQueryString(endpoint.QueryParameters, parameters);
        if (!string.IsNullOrEmpty(queryString))
        {
            url += (url.Contains("?") ? "&" : "?") + queryString;
        }

        // 创建请求
        var request = new HttpRequestMessage(
            new HttpMethod(endpoint.Method),
            url);

        // 设置请求头
        foreach (var (key, value) in endpoint.Headers)
        {
            request.Headers.TryAddWithoutValidation(key, ReplaceTemplateVariables(value, parameters));
        }

        // 设置请求体
        if (!string.IsNullOrEmpty(endpoint.BodyTemplate) &&
            endpoint.Method is "POST" or "PUT" or "PATCH")
        {
            var body = ReplaceTemplateVariables(endpoint.BodyTemplate, parameters);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return request;
    }

    /// <summary>
    /// 替换模板变量 {param}
    /// </summary>
    private static string ReplaceTemplateVariables(string template, IDictionary<string, object> parameters)
    {
        var result = template;
        foreach (var (key, value) in parameters)
        {
            result = result.Replace($"{{{key}}}", value.ToString() ?? "");
        }
        return result;
    }

    /// <summary>
    /// 构建查询字符串
    /// </summary>
    private static string BuildQueryString(
        Dictionary<string, string> queryTemplate,
        IDictionary<string, object> parameters)
    {
        var queryParams = new List<string>();

        foreach (var (key, valueTemplate) in queryTemplate)
        {
            var value = ReplaceTemplateVariables(valueTemplate, parameters);
            if (!string.IsNullOrEmpty(value))
            {
                queryParams.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }
        }

        return string.Join("&", queryParams);
    }

    /// <summary>
    /// 判断状态码是否成功
    /// </summary>
    private static bool IsSuccessStatusCode(System.Net.HttpStatusCode statusCode, string? successCodes)
    {
        if (string.IsNullOrEmpty(successCodes))
        {
            return (int)statusCode >= 200 && (int)statusCode < 300;
        }

        var codes = successCodes.Split(',').Select(c => int.Parse(c.Trim()));
        return codes.Contains((int)statusCode);
    }

    /// <summary>
    /// 提取 JSON 路径数据
    /// </summary>
    private static JsonElement ExtractJsonPath(JsonElement root, string path)
    {
        var parts = path.Split('.');
        var current = root;

        foreach (var part in parts)
        {
            if (current.TryGetProperty(part, out var element))
            {
                current = element;
            }
            else
            {
                throw new KeyNotFoundException($"JSON path '{path}' not found");
            }
        }

        return current;
    }
}
