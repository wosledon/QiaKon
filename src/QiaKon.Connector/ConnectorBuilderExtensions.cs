using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Connector;

/// <summary>
/// 连接器一站式注册扩展
/// </summary>
public static class ConnectorBuilderExtensions
{
    /// <summary>
    /// 添加所有支持的连接器类型
    /// 使用方式:
    /// builder.Services.AddConnectorsFromConfiguration(
    ///     builder.Configuration,
    ///     options => options.AddHttp().AddNpgsql());
    /// </summary>
    public static IServiceCollection AddConnectorsFromConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ConnectorTypeBuilder> configure,
        string sectionName = "Connectors")
    {
        var builder = new ConnectorTypeBuilder();
        configure(builder);

        var connectorConfigs = configuration.GetSection(sectionName).Get<ConnectorsConfiguration>()
            ?? new ConnectorsConfiguration();

        // 解析所有连接器选项
        var optionsDict = new Dictionary<string, IConnectorOptions>();
        foreach (var config in connectorConfigs.Connectors)
        {
            var options = builder.ParseOptions(config);
            optionsDict[config.Name] = options;
        }

        // 注册核心服务
        services.AddSingleton<ConnectorRegistry>();
        services.AddSingleton<IConnectorRegistry>(sp => sp.GetRequiredService<ConnectorRegistry>());
        services.AddSingleton<IReadOnlyDictionary<string, IConnectorOptions>>(optionsDict);
        services.AddSingleton<ConnectorManager>();
        services.AddHostedService<ConnectorHostedService>();

        return services;
    }
}

/// <summary>
/// 连接器类型构建器
/// </summary>
public sealed class ConnectorTypeBuilder
{
    private readonly Dictionary<ConnectorType, Func<ConnectorConfiguration, IConnectorOptions>> _parsers = new();
    private readonly HashSet<ConnectorType> _registeredFactories = new();

    /// <summary>
    /// 启用 HTTP 连接器支持
    /// </summary>
    public ConnectorTypeBuilder AddHttp(
        Func<ConnectorConfiguration, IConnectorOptions> parser,
        Type factoryType)
    {
        _parsers[ConnectorType.Http] = parser;
        _registeredFactories.Add(ConnectorType.Http);
        return this;
    }

    /// <summary>
    /// 启用 PostgreSQL 连接器支持
    /// </summary>
    public ConnectorTypeBuilder AddNpgsql(
        Func<ConnectorConfiguration, IConnectorOptions> parser,
        Type factoryType)
    {
        _parsers[ConnectorType.Npgsql] = parser;
        _registeredFactories.Add(ConnectorType.Npgsql);
        return this;
    }

    /// <summary>
    /// 获取需要注册的工厂类型
    /// </summary>
    internal IEnumerable<Type> GetFactoryTypes()
    {
        // 返回已注册的工厂类型（由调用方提供）
        yield break;
    }

    /// <summary>
    /// 解析连接器配置
    /// </summary>
    internal IConnectorOptions ParseOptions(ConnectorConfiguration config)
    {
        var type = Enum.Parse<ConnectorType>(config.Type, ignoreCase: true);

        if (_parsers.TryGetValue(type, out var parser))
        {
            return parser(config);
        }

        throw new NotSupportedException(
            $"Connector type '{config.Type}' is not supported or not enabled. " +
            $"Enable it with ConnectorTypeBuilder.Add{config.Type}()");
    }
}
