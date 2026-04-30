using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QiaKon.Llm;

/// <summary>
/// HTTP LLM客户端基类
/// </summary>
public abstract class HttpLlmClientBase : ILlmClient
{
    protected HttpClient HttpClient { get; }
    protected LlmOptions Options { get; }
    protected JsonSerializerOptions JsonOptions { get; }

    public abstract LlmProviderType Provider { get; }
    public string Model => Options.Model;

    protected HttpLlmClientBase(HttpClient httpClient, LlmOptions options)
    {
        HttpClient = httpClient;
        Options = options;

        JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
        };

        ConfigureHeaders(httpClient, options);
    }

    protected abstract void ConfigureHeaders(HttpClient client, LlmOptions options);

    public abstract Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    public abstract IAsyncEnumerable<ChatCompletionChunk> CompleteStreamAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    public virtual ValueTask DisposeAsync()
    {
        // HttpClient由调用方管理
        return ValueTask.CompletedTask;
    }

    protected async Task<T> SendRequestAsync<T>(
        HttpMethod method,
        string url,
        object? body,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await HttpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new LlmException($"LLM API error: {response.StatusCode} - {errorContent}", (int)response.StatusCode);
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(content, JsonOptions)
            ?? throw new LlmException("Failed to deserialize response");
    }

    protected string BuildVersionedRelativeUrl(string versionSegment, string relativePath)
    {
        var normalizedVersion = versionSegment.Trim('/');
        var normalizedRelativePath = relativePath.TrimStart('/');

        if (string.IsNullOrWhiteSpace(normalizedVersion))
        {
            return normalizedRelativePath;
        }

        var basePath = HttpClient.BaseAddress?.AbsolutePath
            ?? new Uri(Options.BaseUrl, UriKind.Absolute).AbsolutePath;
        var trimmedBasePath = basePath.TrimEnd('/');

        if (trimmedBasePath.EndsWith('/' + normalizedVersion, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmedBasePath, normalizedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedRelativePath;
        }

        return $"{normalizedVersion}/{normalizedRelativePath}";
    }

    protected async IAsyncEnumerable<ChatCompletionChunk> StreamRequestAsync(
        HttpMethod method,
        string url,
        object? body,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new LlmException($"LLM API error: {response.StatusCode} - {errorContent}", (int)response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
                break;
            if (string.IsNullOrEmpty(line))
                continue;

            if (line.StartsWith("data: "))
            {
                var data = line["data: ".Length..];
                if (data == "[DONE]")
                    break;

                var chunk = ParseStreamChunk(data);
                if (chunk != null)
                    yield return chunk;
            }
        }
    }

    protected abstract ChatCompletionChunk? ParseStreamChunk(string data);
}

/// <summary>
/// 请求重试处理器
/// </summary>
public class LlmRetryHandler : DelegatingHandler
{
    private readonly int _maxRetries;
    private readonly Func<int, TimeSpan> _backoffStrategy;

    public LlmRetryHandler(
        HttpMessageHandler innerHandler,
        int maxRetries = 3,
        Func<int, TimeSpan>? backoffStrategy = null)
        : base(innerHandler)
    {
        _maxRetries = maxRetries;
        _backoffStrategy = backoffStrategy ?? LlmRetryStrategy.ExponentialBackoff();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage? response = null;
        Exception? lastException = null;

        for (int retry = 0; retry <= _maxRetries; retry++)
        {
            try
            {
                response = await base.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode || !IsRetryable(response.StatusCode))
                {
                    return response;
                }

                if (retry < _maxRetries)
                {
                    var delay = _backoffStrategy(retry + 1);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (HttpRequestException ex) when (retry < _maxRetries)
            {
                lastException = ex;
                var delay = _backoffStrategy(retry + 1);
                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken && retry < _maxRetries)
            {
                lastException = ex;
                var delay = _backoffStrategy(retry + 1);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new LlmRetryableException(
            $"Request failed after {_maxRetries} retries",
            lastException!,
            _maxRetries);
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.GatewayTimeout;
    }
}
