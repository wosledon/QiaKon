using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Connector;

/// <summary>
/// 连接器 DI 注册扩展
/// </summary>
public static class ConnectorServiceCollectionExtensions
{
    /// <summary>
    /// 从配置加载并注册连接器
    /// 配置格式示例（appsettings.json）:
    /// {
    ///   "Connectors": {
    ///     "Connectors": [
    ///       {
    ///         "Name": "my-api",
    ///         "Type": "Http",
    ///         "Settings": {
    ///           "BaseUrl": "https://api.example.com",
    ///           "Timeout": "00:00:30"
    ///         }
    ///       },
    ///       {
    ///         "Name": "main-db",
    ///         "Type": "Npgsql",
    ///         "Settings": {
    ///           "ConnectionString": "Host=localhost;Database=mydb",
    ///           "MaxPoolSize": "100"
    ///         }
    ///       }
    ///     ]
    ///   }
    /// }
    /// </summary>
    public static IServiceCollection AddConnectors(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<ConnectorConfiguration, IConnectorOptions> optionsParser,
        string sectionName = "Connectors")
    {
        var connectorConfigs = configuration.GetSection(sectionName).Get<ConnectorsConfiguration>()
            ?? new ConnectorsConfiguration();

        return AddConnectors(services, connectorConfigs, optionsParser);
    }

    /// <summary>
    /// 从连接器配置集合注册连接器
    /// </summary>
    public static IServiceCollection AddConnectors(
        this IServiceCollection services,
        ConnectorsConfiguration configuration,
        Func<ConnectorConfiguration, IConnectorOptions> optionsParser)
    {
        // 注册连接器注册表
        services.AddSingleton<ConnectorRegistry>();
        services.AddSingleton<IConnectorRegistry>(sp => sp.GetRequiredService<ConnectorRegistry>());

        // 注册连接器选项
        var optionsDict = new Dictionary<string, IConnectorOptions>();

        foreach (var config in configuration.Connectors)
        {
            var options = optionsParser(config);
            optionsDict[config.Name] = options;
        }

        services.AddSingleton<IReadOnlyDictionary<string, IConnectorOptions>>(optionsDict);

        // 注册连接器管理器
        services.AddSingleton<ConnectorManager>();

        // 注册宿主服务
        services.AddHostedService<ConnectorHostedService>();

        return services;
    }

    /// <summary>
    /// 注册连接器工厂
    /// </summary>
    public static IServiceCollection AddConnectorFactory<TFactory>(
        this IServiceCollection services)
        where TFactory : class, IConnectorFactory
    {
        services.AddSingleton<IConnectorFactory, TFactory>();
        return services;
    }
}
