using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using NpgsqlTypes;

namespace QiaKon.Connector.Npgsql;

/// <summary>
/// PostgreSQL 连接器实现（配置驱动）
/// </summary>
public sealed class NpgsqlConnector : ConnectorBase, IDbConnector
{
    private readonly NpgsqlConnectorOptions _options;
    private readonly Dictionary<string, DbQueryTemplateConfig> _templates = new();
    private NpgsqlDataSource? _dataSource;
    private readonly JsonSerializerOptions _jsonOptions;

    public NpgsqlConnector(NpgsqlConnectorOptions options)
        : base(options.Name, ConnectorType.Npgsql)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("ConnectionString cannot be null or empty", nameof(options));
        }

        // 注册所有查询模板
        foreach (var template in options.QueryTemplates)
        {
            _templates[template.Name] = template;
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <inheritdoc />
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        State = ConnectorState.Connecting;

        try
        {
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_options.ConnectionString)
            {
                MinPoolSize = 0,
                MaxPoolSize = _options.MaxPoolSize,
                CommandTimeout = _options.CommandTimeoutSeconds,
                Pooling = true
            };

            _dataSource = NpgsqlDataSource.Create(connectionStringBuilder.ConnectionString);

            // 测试连接
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

            State = ConnectorState.Connected;
        }
        catch (Exception ex)
        {
            State = ConnectorState.Unhealthy;
            throw new ConnectorException($"Failed to initialize Npgsql connector '{Name}'", ex);
        }
    }

    /// <inheritdoc />
    public async Task<ConnectorResponse> QueryAsync(
        string templateName,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        if (!_templates.TryGetValue(templateName, out var template))
        {
            throw new KeyNotFoundException($"Query template '{templateName}' not found in connector '{Name}'");
        }

        try
        {
            await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(template.SqlTemplate, connection);
            command.CommandTimeout = template.TimeoutSeconds ?? _options.CommandTimeoutSeconds;

            // 绑定参数
            BindParameters(command, template.Parameters, parameters);

            // 执行查询
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var results = new List<Dictionary<string, object>>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var value = reader.GetValue(i);

                    // 应用结果映射
                    var mappedName = template.ResultMapping.TryGetValue(columnName, out var mapped)
                        ? mapped
                        : columnName;

                    row[mappedName] = value == DBNull.Value ? null! : value;
                }
                results.Add(row);
            }

            return new ConnectorResponse(
                IsSuccess: true,
                Data: results);
        }
        catch (Exception ex)
        {
            return new ConnectorResponse(
                IsSuccess: false,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string templateName,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        var response = await QueryAsync(templateName, parameters, cancellationToken);

        if (!response.IsSuccess || response.Data is null)
        {
            throw new ConnectorException($"Query failed: {response.ErrorMessage}");
        }

        if (response.Data is not List<Dictionary<string, object>> rows)
        {
            throw new InvalidCastException("Query data is not in expected format");
        }

        var results = new List<T>();
        foreach (var row in rows)
        {
            var json = JsonSerializer.Serialize(row);
            var obj = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            if (obj is not null)
            {
                results.Add(obj);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteCommandAsync(
        string templateName,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        if (!_templates.TryGetValue(templateName, out var template))
        {
            throw new KeyNotFoundException($"Command template '{templateName}' not found in connector '{Name}'");
        }

        try
        {
            await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(template.SqlTemplate, connection);
            command.CommandTimeout = template.TimeoutSeconds ?? _options.CommandTimeoutSeconds;

            // 绑定参数
            BindParameters(command, template.Parameters, parameters);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ConnectorException($"Command execution failed: {ex.Message}", ex);
        }
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
    public override async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        State = ConnectorState.Disconnected;

        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
            _dataSource = null;
        }
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
    /// 绑定 SQL 参数
    /// </summary>
    private static void BindParameters(
        NpgsqlCommand command,
        List<DbParameterConfig> paramConfigs,
        IDictionary<string, object>? parameters)
    {
        parameters ??= new Dictionary<string, object>();

        foreach (var paramConfig in paramConfigs)
        {
            var paramName = paramConfig.Name.TrimStart('@');

            if (!parameters.TryGetValue(paramName, out var value))
            {
                if (paramConfig.IsRequired)
                {
                    throw new ArgumentException(
                        $"Required parameter '{paramName}' is missing");
                }

                value = paramConfig.DefaultValue ?? string.Empty;
            }

            var dbType = ParseNpgsqlDbType(paramConfig.DbType);
            command.Parameters.AddWithValue(paramName, dbType, value);
        }
    }

    /// <summary>
    /// 解析 NpgsqlDbType
    /// </summary>
    private static NpgsqlDbType ParseNpgsqlDbType(string dbType)
    {
        return dbType.ToLowerInvariant() switch
        {
            "integer" or "int4" => NpgsqlDbType.Integer,
            "bigint" or "int8" => NpgsqlDbType.Bigint,
            "text" or "varchar" => NpgsqlDbType.Text,
            "boolean" or "bool" => NpgsqlDbType.Boolean,
            "timestamp" or "timestamptz" => NpgsqlDbType.TimestampTz,
            "date" => NpgsqlDbType.Date,
            "uuid" => NpgsqlDbType.Uuid,
            "jsonb" => NpgsqlDbType.Jsonb,
            "json" => NpgsqlDbType.Json,
            "numeric" or "decimal" => NpgsqlDbType.Numeric,
            "real" or "float4" => NpgsqlDbType.Real,
            "double" or "float8" => NpgsqlDbType.Double,
            "bytea" => NpgsqlDbType.Bytea,
            _ => NpgsqlDbType.Text
        };
    }
}

