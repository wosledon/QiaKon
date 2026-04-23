using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace QiaKon.Connector.Npgsql;

/// <summary>
/// PostgreSQL 连接器 DI 注册扩展
/// </summary>
public static class NpgsqlConnectorServiceCollectionExtensions
{
    /// <summary>
    /// 注册 PostgreSQL 连接器工厂（配置驱动）
    /// </summary>
    public static IServiceCollection AddNpgsqlConnectorSupport(
        this IServiceCollection services)
    {
        services.AddSingleton<IConnectorFactory, NpgsqlConnectorFactory>();
        return services;
    }

    /// <summary>
    /// 从配置解析 PostgreSQL 连接器选项
    /// </summary>
    internal static NpgsqlConnectorOptions ParseFromConfiguration(ConnectorConfiguration config)
    {
        var options = new NpgsqlConnectorOptions
        {
            Name = config.Name
        };

        if (config.Settings.TryGetValue("ConnectionString", out var connStr))
        {
            options.ConnectionString = connStr.ToString()!;
        }

        if (config.Settings.TryGetValue("MaxPoolSize", out var maxPool))
        {
            options.MaxPoolSize = Convert.ToInt32(maxPool);
        }

        if (config.Settings.TryGetValue("CommandTimeoutSeconds", out var cmdTimeout))
        {
            options.CommandTimeoutSeconds = Convert.ToInt32(cmdTimeout);
        }

        // 解析查询模板配置
        if (config.Settings.TryGetValue("QueryTemplates", out var templatesObj) &&
            templatesObj is List<object> templates)
        {
            foreach (var templateObj in templates)
            {
                if (templateObj is Dictionary<string, object> templateDict)
                {
                    var template = new DbQueryTemplateConfig();

                    if (templateDict.TryGetValue("Name", out var name))
                        template.Name = name.ToString()!;
                    if (templateDict.TryGetValue("SqlTemplate", out var sql))
                        template.SqlTemplate = sql.ToString()!;
                    if (templateDict.TryGetValue("CommandType", out var cmdType))
                        template.CommandType = cmdType.ToString()!;
                    if (templateDict.TryGetValue("TimeoutSeconds", out var timeout))
                        template.TimeoutSeconds = Convert.ToInt32(timeout);
                    if (templateDict.TryGetValue("EnableCache", out var cache))
                        template.EnableCache = Convert.ToBoolean(cache);
                    if (templateDict.TryGetValue("CacheTtlSeconds", out var cacheTtl))
                        template.CacheTtlSeconds = Convert.ToInt32(cacheTtl);

                    // 解析参数
                    if (templateDict.TryGetValue("Parameters", out var paramsObj) &&
                        paramsObj is List<object> parameters)
                    {
                        foreach (var paramObj in parameters)
                        {
                            if (paramObj is Dictionary<string, object> paramDict)
                            {
                                var param = new DbParameterConfig();
                                if (paramDict.TryGetValue("Name", out var paramName))
                                    param.Name = paramName.ToString()!;
                                if (paramDict.TryGetValue("DbType", out var dbType))
                                    param.DbType = dbType.ToString()!;
                                if (paramDict.TryGetValue("IsRequired", out var required))
                                    param.IsRequired = Convert.ToBoolean(required);
                                if (paramDict.TryGetValue("DefaultValue", out var defaultVal))
                                    param.DefaultValue = defaultVal.ToString();

                                template.Parameters.Add(param);
                            }
                        }
                    }

                    options.QueryTemplates.Add(template);
                }
            }
        }

        return options;
    }
}

