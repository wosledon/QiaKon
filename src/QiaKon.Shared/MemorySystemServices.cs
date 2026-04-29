using System.Text.Json;
using Microsoft.Extensions.Logging;
using QiaKon.Contracts.DTOs;

namespace QiaKon.Shared;

/// <summary>
/// 内存态LLM提供商服务实现
/// </summary>
public sealed class MemoryLlmProviderService : ILlmProviderService
{
    private readonly Dictionary<Guid, LlmProviderRecord> _providers = new();
    private readonly Dictionary<Guid, LlmModelRecord> _models = new();
    private readonly ILogger<MemoryLlmProviderService>? _logger;

    public MemoryLlmProviderService(ILogger<MemoryLlmProviderService>? logger = null)
    {
        _logger = logger;
        InitializeSeedData();
    }

    private void InitializeSeedData()
    {
        // Qwen provider
        var qwenId = Guid.Parse("11111111-0000-0000-0000-000000000001");
        var qwenProvider = new LlmProviderRecord(qwenId, "Qwen 云服务", LlmInterfaceType.OpenAI,
            "https://dashscope.aliyuncs.com/compatible-mode/api/v1", "sk-qwen-xxxxx", 60, 3, true);
        _providers[qwenId] = qwenProvider;

        _models[Guid.Parse("22222222-0000-0000-0000-000000000001")] = new LlmModelRecord(
            Guid.Parse("22222222-0000-0000-0000-000000000001"), qwenId, "Qwen-Max-推理", "qwen-max",
            LlmModelType.Inference, null, 128000, true, true);
        _models[Guid.Parse("22222222-0000-0000-0000-000000000002")] = new LlmModelRecord(
            Guid.Parse("22222222-0000-0000-0000-000000000002"), qwenId, "Qwen-Turbo-推理", "qwen-turbo",
            LlmModelType.Inference, null, 128000, true, false);
        _models[Guid.Parse("22222222-0000-0000-0000-000000000003")] = new LlmModelRecord(
            Guid.Parse("22222222-0000-0000-0000-000000000003"), qwenId, "Qwen-Embed-分块", "text-embedding-3-small",
            LlmModelType.Embedding, 1536, null, true, true);

        // OpenAI provider
        var openAiId = Guid.Parse("11111111-0000-0000-0000-000000000002");
        var openAiProvider = new LlmProviderRecord(openAiId, "OpenAI 服务", LlmInterfaceType.OpenAI,
            "https://api.openai.com/v1", "sk-openai-xxxxx", 60, 3, true);
        _providers[openAiId] = openAiProvider;

        _models[Guid.Parse("22222222-0000-0000-0000-000000000004")] = new LlmModelRecord(
            Guid.Parse("22222222-0000-0000-0000-000000000004"), openAiId, "GPT-4o-推理", "gpt-4o",
            LlmModelType.Inference, null, 128000, true, false);
        _models[Guid.Parse("22222222-0000-0000-0000-000000000005")] = new LlmModelRecord(
            Guid.Parse("22222222-0000-0000-0000-000000000005"), openAiId, "GPT-4o-Mini-推理", "gpt-4o-mini",
            LlmModelType.Inference, null, 128000, true, false);

        // Anthropic provider
        var anthropicId = Guid.Parse("11111111-0000-0000-0000-000000000003");
        var anthropicProvider = new LlmProviderRecord(anthropicId, "Anthropic 服务", LlmInterfaceType.Anthropic,
            "https://api.anthropic.com", "sk-ant-xxxxx", 60, 3, true);
        _providers[anthropicId] = anthropicProvider;

        _models[Guid.Parse("22222222-0000-0000-0000-000000000006")] = new LlmModelRecord(
            Guid.Parse("22222222-0000-0000-0000-000000000006"), anthropicId, "Claude-3.5-Sonnet-推理", "claude-3-5-sonnet-20241022",
            LlmModelType.Inference, null, 200000, true, false);

        // Built-in embedding models
        _models[Guid.Parse("22222222-ffff-ffff-ffff-fffffffffff1")] = new LlmModelRecord(
            Guid.Parse("22222222-ffff-ffff-ffff-fffffffffff1"), qwenId, "bge-large-zh", "bge-large-zh-v1.5",
            LlmModelType.Embedding, 1024, null, true, false)
        { IsBuiltIn = true };
        _models[Guid.Parse("22222222-ffff-ffff-ffff-fffffffffff2")] = new LlmModelRecord(
            Guid.Parse("22222222-ffff-ffff-ffff-fffffffffff2"), qwenId, "bge-base-zh", "bge-base-zh-v1.5",
            LlmModelType.Embedding, 768, null, true, false)
        { IsBuiltIn = true };
    }

    public IReadOnlyList<LlmProviderDto> GetAll()
    {
        return _providers.Values.Select(ToDto).ToList();
    }

    public LlmProviderDto? GetById(Guid id)
    {
        return _providers.TryGetValue(id, out var p) ? ToDto(p) : null;
    }

    public LlmProviderDto Create(CreateLlmProviderDto request)
    {
        var provider = new LlmProviderRecord(Guid.NewGuid(), request.Name, request.InterfaceType,
            request.BaseUrl, request.ApiKey, request.TimeoutSeconds, request.RetryCount, false);
        _providers[provider.Id] = provider;
        _logger?.LogInformation("LLM Provider created: {Id} - {Name}", provider.Id, provider.Name);
        return ToDto(provider);
    }

    public LlmProviderDto? Update(Guid id, CreateLlmProviderDto request)
    {
        if (!_providers.TryGetValue(id, out var provider))
            return null;

        provider.Name = request.Name;
        provider.InterfaceType = request.InterfaceType;
        provider.BaseUrl = request.BaseUrl;
        provider.ApiKey = request.ApiKey;
        provider.TimeoutSeconds = request.TimeoutSeconds;
        provider.RetryCount = request.RetryCount;

        return ToDto(provider);
    }

    public bool Delete(Guid id)
    {
        // 删除提供商及其所有模型
        if (!_providers.Remove(id))
            return false;

        var modelIds = _models.Values.Where(m => m.ProviderId == id).Select(m => m.Id).ToList();
        foreach (var modelId in modelIds)
        {
            _models.Remove(modelId);
        }
        return true;
    }

    public LlmModelDto? AddModel(CreateLlmModelDto request)
    {
        if (!_providers.ContainsKey(request.ProviderId))
            return null;

        var model = new LlmModelRecord(Guid.NewGuid(), request.ProviderId, request.Name,
            request.ActualModelName, request.ModelType, request.VectorDimensions, request.MaxTokens, true, request.SetAsDefault);

        if (request.SetAsDefault)
        {
            // 取消同类型其他模型的默认状态
            foreach (var m in _models.Values.Where(m => m.ProviderId == request.ProviderId && m.ModelType == request.ModelType))
            {
                m.IsDefault = false;
            }
        }

        _models[model.Id] = model;
        _logger?.LogInformation("LLM Model created: {Id} - {Name}", model.Id, model.Name);
        return ToModelDto(model);
    }

    public bool DeleteModel(Guid modelId)
    {
        if (!_models.TryGetValue(modelId, out var model) || model.IsBuiltIn)
            return false;
        return _models.Remove(modelId);
    }

    public IReadOnlyList<LlmModelDto> GetModelsByProviderId(Guid providerId)
    {
        return _models.Values.Where(m => m.ProviderId == providerId).Select(ToModelDto).ToList();
    }

    public LlmModelDto? UpdateModel(Guid modelId, UpdateLlmModelDto request)
    {
        if (!_models.TryGetValue(modelId, out var model))
            return null;

        if (!string.IsNullOrWhiteSpace(request.Name))
            model.Name = request.Name;
        if (!string.IsNullOrWhiteSpace(request.ActualModelName))
            model.ActualModelName = request.ActualModelName;
        if (request.VectorDimensions.HasValue)
            model.VectorDimensions = request.VectorDimensions;
        if (request.MaxTokens.HasValue)
            model.MaxTokens = request.MaxTokens;

        if (request.SetAsDefault == true)
        {
            foreach (var m in _models.Values.Where(m => m.ProviderId == model.ProviderId && m.ModelType == model.ModelType))
            {
                m.IsDefault = m.Id == modelId;
            }
        }

        _logger?.LogInformation("LLM Model updated: {Id} - {Name}", model.Id, model.Name);
        return ToModelDto(model);
    }

    public bool SetDefaultModel(Guid modelId)
    {
        if (!_models.TryGetValue(modelId, out var model))
            return false;

        foreach (var m in _models.Values.Where(m => m.ProviderId == model.ProviderId && m.ModelType == model.ModelType))
        {
            m.IsDefault = m.Id == modelId;
        }
        return true;
    }

    public bool EnableModel(Guid modelId, bool enabled)
    {
        if (!_models.TryGetValue(modelId, out var model))
            return false;
        model.IsEnabled = enabled;
        return true;
    }

    public (bool Success, string Message, double? ResponseTimeMs) TestConnection(Guid providerId)
    {
        if (!_providers.TryGetValue(providerId, out var provider))
            return (false, "Provider not found", null);

        // 模拟连接测试
        var responseTime = Random.Shared.NextDouble() * 200 + 50;
        var success = !string.IsNullOrWhiteSpace(provider.ApiKey);

        return (success, success ? "Connection successful" : "API key is missing or invalid", responseTime);
    }

    public IReadOnlyList<LlmModelDto> GetBuiltInEmbeddingModels()
    {
        return _models.Values.Where(m => m.IsBuiltIn && m.ModelType == LlmModelType.Embedding).Select(ToModelDto).ToList();
    }

    private LlmProviderDto ToDto(LlmProviderRecord p)
    {
        var models = _models.Values.Where(m => m.ProviderId == p.Id).Select(ToModelDto).ToList();
        return new LlmProviderDto(p.Id, p.Name, p.InterfaceType, p.BaseUrl, p.ApiKey, p.TimeoutSeconds, p.RetryCount, p.HasModels, models);
    }

    private LlmModelDto ToModelDto(LlmModelRecord m)
    {
        return new LlmModelDto(m.Id, m.ProviderId, m.Name, m.ActualModelName, m.ModelType, m.VectorDimensions, m.MaxTokens, m.IsEnabled, m.IsDefault);
    }

    private class LlmProviderRecord
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public LlmInterfaceType InterfaceType { get; set; }
        public string BaseUrl { get; set; }
        public string? ApiKey { get; set; }
        public int TimeoutSeconds { get; set; }
        public int RetryCount { get; set; }
        public bool HasModels { get; set; }
        public LlmProviderRecord(Guid id, string name, LlmInterfaceType interfaceType, string baseUrl, string? apiKey, int timeoutSeconds, int retryCount, bool hasModels)
        {
            Id = id;
            Name = name;
            InterfaceType = interfaceType;
            BaseUrl = baseUrl;
            ApiKey = apiKey;
            TimeoutSeconds = timeoutSeconds;
            RetryCount = retryCount;
            HasModels = hasModels;
        }
    }

    private class LlmModelRecord
    {
        public Guid Id { get; set; }
        public Guid ProviderId { get; set; }
        public string Name { get; set; }
        public string ActualModelName { get; set; }
        public LlmModelType ModelType { get; set; }
        public int? VectorDimensions { get; set; }
        public int? MaxTokens { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsDefault { get; set; }
        public bool IsBuiltIn { get; set; }
        public LlmModelRecord(Guid id, Guid providerId, string name, string actualModelName, LlmModelType modelType, int? vectorDimensions, int? maxTokens, bool isEnabled, bool isDefault)
        {
            Id = id;
            ProviderId = providerId;
            Name = name;
            ActualModelName = actualModelName;
            ModelType = modelType;
            VectorDimensions = vectorDimensions;
            MaxTokens = maxTokens;
            IsEnabled = isEnabled;
            IsDefault = isDefault;
        }
    }
}

/// <summary>
/// 内存态系统配置服务实现
/// </summary>
public sealed class MemorySystemConfigService : ISystemConfigService
{
    private SystemConfigRecord _config = new();
    private readonly ILogger<MemorySystemConfigService>? _logger;

    public MemorySystemConfigService(ILogger<MemorySystemConfigService>? logger = null)
    {
        _logger = logger;
    }

    public SystemConfigDto GetConfig()
    {
        return new SystemConfigDto(
            _config.DefaultChunkingStrategy,
            _config.ChunkSize,
            _config.ChunkOverlap,
            _config.DefaultVectorDimensions,
            _config.CacheStrategy,
            _config.CacheExpirationMinutes,
            _config.PromptTemplate);
    }

    public SystemConfigDto UpdateConfig(UpdateSystemConfigDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.DefaultChunkingStrategy))
            _config.DefaultChunkingStrategy = request.DefaultChunkingStrategy;
        if (request.ChunkSize.HasValue)
            _config.ChunkSize = request.ChunkSize.Value;
        if (request.ChunkOverlap.HasValue)
            _config.ChunkOverlap = request.ChunkOverlap.Value;
        if (request.DefaultVectorDimensions.HasValue)
            _config.DefaultVectorDimensions = request.DefaultVectorDimensions.Value;
        if (!string.IsNullOrWhiteSpace(request.CacheStrategy))
            _config.CacheStrategy = request.CacheStrategy;
        if (request.CacheExpirationMinutes.HasValue)
            _config.CacheExpirationMinutes = request.CacheExpirationMinutes.Value;
        if (!string.IsNullOrWhiteSpace(request.PromptTemplate))
            _config.PromptTemplate = request.PromptTemplate;

        _logger?.LogInformation("System config updated");
        return GetConfig();
    }

    public SystemConfigDto ResetConfig()
    {
        _config = new SystemConfigRecord();
        _logger?.LogInformation("System config reset to defaults");
        return GetConfig();
    }

    private class SystemConfigRecord
    {
        public string DefaultChunkingStrategy { get; set; } = "Recursive";
        public int ChunkSize { get; set; } = 512;
        public int ChunkOverlap { get; set; } = 50;
        public int DefaultVectorDimensions { get; set; } = 1536;
        public string CacheStrategy { get; set; } = "L1+L2+L3";
        public int CacheExpirationMinutes { get; set; } = 60;
        public string PromptTemplate { get; set; } = "你是一个知识库问答助手。请基于以下参考内容回答用户问题。\n\n参考内容：\n{context}\n\n用户问题：{question}\n\n回答：";
    }
}

/// <summary>
/// 内存态连接器服务实现
/// </summary>
public sealed class MemoryConnectorService : IConnectorService
{
    private readonly Dictionary<Guid, ConnectorRecord> _connectors = new();
    private readonly ILogger<MemoryConnectorService>? _logger;

    public MemoryConnectorService(ILogger<MemoryConnectorService>? logger = null)
    {
        _logger = logger;
        InitializeSeedData();
    }

    private void InitializeSeedData()
    {
        var http = new ConnectorRecord(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            "飞书 API", ConnectorType.Http, "https://open.feishu.cn", null, null, DateTime.UtcNow,
            new List<ConnectorEndpointRecord>
            {
                new("发送消息", "https://open.feishu.cn/open-apis/im/v1/messages", "POST"),
                new("获取用户", "https://open.feishu.cn/open-apis/contact/v3/users", "GET"),
            });
        _connectors[http.Id] = http;

        var npgsql = new ConnectorRecord(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            "PostgreSQL (生产)", ConnectorType.Npgsql, null, "Host=localhost;Database=qiakon;Username=postgres;Password=postgres", null, DateTime.UtcNow, null);
        _connectors[npgsql.Id] = npgsql;
    }

    public IReadOnlyList<ConnectorDto> GetAll()
    {
        return _connectors.Values.Select(ToDto).ToList();
    }

    public ConnectorDto? GetById(Guid id)
    {
        return _connectors.TryGetValue(id, out var c) ? ToDto(c) : null;
    }

    public ConnectorDto Create(CreateConnectorDto request)
    {
        var connector = new ConnectorRecord(Guid.NewGuid(), request.Name, request.Type,
            request.BaseUrl, request.ConnectionString, null, DateTime.UtcNow,
            request.Endpoints?.Select(e => new ConnectorEndpointRecord(e.Name, e.Url, e.Method)).ToList());
        _connectors[connector.Id] = connector;
        _logger?.LogInformation("Connector created: {Id} - {Name}", connector.Id, connector.Name);
        return ToDto(connector);
    }

    public ConnectorDto? Update(Guid id, CreateConnectorDto request)
    {
        if (!_connectors.TryGetValue(id, out var connector))
            return null;

        connector.Name = request.Name;
        connector.BaseUrl = request.BaseUrl;
        connector.ConnectionString = request.ConnectionString;
        connector.Endpoints = request.Endpoints?.Select(e => new ConnectorEndpointRecord(e.Name, e.Url, e.Method)).ToList();

        return ToDto(connector);
    }

    public bool Delete(Guid id)
    {
        return _connectors.Remove(id);
    }

    public ConnectorHealthResultDto CheckHealth(Guid id)
    {
        if (!_connectors.TryGetValue(id, out var connector))
            return new ConnectorHealthResultDto(id, false, "Connector not found", null);

        // 模拟健康检查
        var isHealthy = connector.State == ConnectorState.Healthy || connector.State == ConnectorState.Connected;
        var responseTime = Random.Shared.NextDouble() * 100;

        connector.LastHealthCheck = DateTime.UtcNow;
        connector.State = isHealthy ? ConnectorState.Healthy : ConnectorState.Unhealthy;

        return new ConnectorHealthResultDto(id, isHealthy, isHealthy ? "OK" : "Connection failed", responseTime);
    }

    private ConnectorDto ToDto(ConnectorRecord c)
    {
        return new ConnectorDto(c.Id, c.Name, c.Type, c.State, c.BaseUrl, MaskPassword(c.ConnectionString),
            c.LastHealthCheck, c.Endpoints?.Select(e => new ConnectorEndpointDto(e.Name, e.Url, e.Method)).ToList() ?? new List<ConnectorEndpointDto>());
    }

    private string? MaskPassword(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return connectionString;
        return System.Text.RegularExpressions.Regex.Replace(connectionString, @"(Password|password|Pwd)=([^;]*)", "$1=******");
    }

    private class ConnectorRecord
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public ConnectorType Type { get; set; }
        public ConnectorState State { get; set; }
        public string? BaseUrl { get; set; }
        public string? ConnectionString { get; set; }
        public DateTime? LastHealthCheck { get; set; }
        public List<ConnectorEndpointRecord>? Endpoints { get; set; }
        public ConnectorRecord(Guid id, string name, ConnectorType type, string? baseUrl, string? connectionString, ConnectorState? state, DateTime? lastHealthCheck, List<ConnectorEndpointRecord>? endpoints)
        {
            Id = id;
            Name = name;
            Type = type;
            State = state ?? ConnectorState.Connected;
            BaseUrl = baseUrl;
            ConnectionString = connectionString;
            LastHealthCheck = lastHealthCheck;
            Endpoints = endpoints;
        }
    }

    private class ConnectorEndpointRecord
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Method { get; set; }
        public ConnectorEndpointRecord(string name, string url, string method)
        {
            Name = name;
            Url = url;
            Method = method;
        }
    }
}

/// <summary>
/// 内存态审计日志服务实现
/// </summary>
public sealed class MemoryAuditLogService : IAuditLogService
{
    private readonly List<AuditLogRecord> _logs = new();
    private readonly object _lock = new();
    private readonly ILogger<MemoryAuditLogService>? _logger;

    public MemoryAuditLogService(ILogger<MemoryAuditLogService>? logger = null)
    {
        _logger = logger;
        InitializeSeedData();
    }

    private void InitializeSeedData()
    {
        var adminId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var engineerId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        AddLog(new AuditLogRecord(Guid.NewGuid(), adminId, "admin", "登录", "Auth", null, null, "成功", "127.0.0.1", "登录系统", DateTime.UtcNow.AddHours(-48)));
        AddLog(new AuditLogRecord(Guid.NewGuid(), engineerId, "engineer", "登录", "Auth", null, null, "成功", "192.168.1.100", "登录系统", DateTime.UtcNow.AddHours(-24)));
        AddLog(new AuditLogRecord(Guid.NewGuid(), adminId, "admin", "创建", "Document", Guid.Parse("d1111111-1111-1111-1111-111111111111"), "QiaKon平台架构设计文档", "成功", "127.0.0.1", "上传新文档", DateTime.UtcNow.AddHours(-20)));
        AddLog(new AuditLogRecord(Guid.NewGuid(), engineerId, "engineer", "更新", "Document", Guid.Parse("d6666666-6666-6666-6666-666666666666"), "研发部项目管理制度", "成功", "192.168.1.100", "修改文档内容", DateTime.UtcNow.AddHours(-12)));
        AddLog(new AuditLogRecord(Guid.NewGuid(), adminId, "admin", "创建", "GraphEntity", null, "QiaKon平台", "成功", "127.0.0.1", "创建图谱实体", DateTime.UtcNow.AddHours(-6)));
        AddLog(new AuditLogRecord(Guid.NewGuid(), engineerId, "engineer", "问答", "Retrieval", null, null, "成功", "192.168.1.100", "RAG问答: QiaKon是什么", DateTime.UtcNow.AddHours(-2)));
    }

    public AuditLogPagedResultDto GetLogs(int page, int pageSize, Guid? userId = null, string? action = null, DateTime? startTime = null, DateTime? endTime = null)
    {
        var query = _logs.AsEnumerable();

        if (userId.HasValue)
            query = query.Where(l => l.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action.Contains(action, StringComparison.OrdinalIgnoreCase));
        if (startTime.HasValue)
            query = query.Where(l => l.Timestamp >= startTime.Value);
        if (endTime.HasValue)
            query = query.Where(l => l.Timestamp <= endTime.Value);

        var totalCount = query.LongCount();
        var items = query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToDto)
            .ToList();

        return new AuditLogPagedResultDto(items, totalCount, page, pageSize);
    }

    public AuditLogDto? GetById(Guid id)
    {
        return _logs.FirstOrDefault(l => l.Id == id) is { } log ? ToDto(log) : null;
    }

    public void Log(Guid userId, string action, string resourceType, Guid? resourceId, string? resourceName, string result, string? ipAddress, string? details)
    {
        var username = "unknown";
        AddLog(new AuditLogRecord(Guid.NewGuid(), userId, username, action, resourceType, resourceId, resourceName, result, ipAddress, details, DateTime.UtcNow));
    }

    private void AddLog(AuditLogRecord record)
    {
        lock (_lock)
        {
            _logs.Add(record);
            // 保持最近1000条
            if (_logs.Count > 1000)
            {
                _logs.RemoveAt(0);
            }
        }
    }

    private AuditLogDto ToDto(AuditLogRecord r)
    {
        return new AuditLogDto(r.Id, r.UserId, r.Username, r.Action, r.ResourceType, r.ResourceId, r.ResourceName, r.Result, r.IpAddress, r.Details, r.Timestamp);
    }

    private class AuditLogRecord
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string Action { get; set; }
        public string ResourceType { get; set; }
        public Guid? ResourceId { get; set; }
        public string? ResourceName { get; set; }
        public string Result { get; set; }
        public string? IpAddress { get; set; }
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; }
        public AuditLogRecord(Guid id, Guid userId, string username, string action, string resourceType, Guid? resourceId, string? resourceName, string result, string? ipAddress, string? details, DateTime timestamp)
        {
            Id = id;
            UserId = userId;
            Username = username;
            Action = action;
            ResourceType = resourceType;
            ResourceId = resourceId;
            ResourceName = resourceName;
            Result = result;
            IpAddress = ipAddress;
            Details = details;
            Timestamp = timestamp;
        }
    }
}
