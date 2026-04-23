using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Connector.Http;

/// <summary>
/// HTTP 连接器 DI 注册扩展
/// </summary>
public static class HttpConnectorServiceCollectionExtensions
{
    /// <summary>
    /// 注册 HTTP 连接器工厂（配置驱动）
    /// </summary>
    public static IServiceCollection AddHttpConnectorSupport(
        this IServiceCollection services)
    {
        services.AddSingleton<IConnectorFactory, HttpConnectorFactory>();
        return services;
    }

    /// <summary>
    /// 从配置解析 HTTP 连接器选项
    /// </summary>
    internal static HttpConnectorOptions ParseFromConfiguration(ConnectorConfiguration config)
    {
        var options = new HttpConnectorOptions
        {
            Name = config.Name
        };

        if (config.Settings.TryGetValue("BaseUrl", out var baseUrl))
        {
            options.BaseUrl = baseUrl.ToString()!;
        }

        if (config.Settings.TryGetValue("ConnectionTimeoutSeconds", out var timeout))
        {
            options.ConnectionTimeoutSeconds = Convert.ToInt32(timeout);
        }

        if (config.Settings.TryGetValue("MaxConnections", out var maxConn))
        {
            options.MaxConnections = Convert.ToInt32(maxConn);
        }

        // 解析端点配置
        if (config.Settings.TryGetValue("Endpoints", out var endpointsObj) &&
            endpointsObj is List<object> endpoints)
        {
            foreach (var endpointObj in endpoints)
            {
                if (endpointObj is Dictionary<string, object> endpointDict)
                {
                    var endpoint = new HttpEndpointConfig();

                    if (endpointDict.TryGetValue("Name", out var name))
                        endpoint.Name = name.ToString()!;
                    if (endpointDict.TryGetValue("Method", out var method))
                        endpoint.Method = method.ToString()!;
                    if (endpointDict.TryGetValue("Url", out var url))
                        endpoint.Url = url.ToString()!;
                    if (endpointDict.TryGetValue("BodyTemplate", out var body))
                        endpoint.BodyTemplate = body.ToString();
                    if (endpointDict.TryGetValue("ResponseDataPath", out var path))
                        endpoint.ResponseDataPath = path.ToString();
                    if (endpointDict.TryGetValue("TimeoutSeconds", out var timeoutSec))
                        endpoint.TimeoutSeconds = Convert.ToInt32(timeoutSec);
                    if (endpointDict.TryGetValue("RetryCount", out var retry))
                        endpoint.RetryCount = Convert.ToInt32(retry);

                    options.Endpoints.Add(endpoint);
                }
            }
        }

        return options;
    }
}

