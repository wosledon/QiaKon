namespace QiaKon.Llm;

/// <summary>
/// 聊天完成请求
/// </summary>
public sealed record ChatCompletionRequest
{
    /// <summary>
    /// 模型名称
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// 消息列表
    /// </summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>
    /// 最大输出Token数
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// 温度（0-2）
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>
    /// Top P（0-1）
    /// </summary>
    public double? TopP { get; init; }

    /// <summary>
    /// 可用工具列表
    /// </summary>
    public IReadOnlyList<ToolDefinition>? Tools { get; init; }

    /// <summary>
    /// 工具调用策略（auto/none/required）
    /// </summary>
    public string? ToolChoice { get; init; }

    /// <summary>
    /// 是否使用流式响应
    /// </summary>
    public bool Stream { get; init; }

    /// <summary>
    /// 停止序列
    /// </summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>
    /// 系统提示词（某些Provider需要单独传递）
    /// </summary>
    public string? SystemPrompt { get; init; }
}

/// <summary>
/// 聊天完成请求构建器
/// </summary>
public sealed class ChatCompletionRequestBuilder
{
    private string? _model;
    private readonly List<ChatMessage> _messages = new();
    private int? _maxTokens;
    private double? _temperature;
    private double? _topP;
    private List<ToolDefinition>? _tools;
    private string? _toolChoice;
    private bool _stream;
    private List<string>? _stopSequences;
    private string? _systemPrompt;

    public ChatCompletionRequestBuilder SetModel(string model)
    {
        _model = model;
        return this;
    }

    public ChatCompletionRequestBuilder AddMessage(ChatMessage message)
    {
        _messages.Add(message);
        return this;
    }

    public ChatCompletionRequestBuilder AddMessages(IEnumerable<ChatMessage> messages)
    {
        _messages.AddRange(messages);
        return this;
    }

    public ChatCompletionRequestBuilder SetMaxTokens(int maxTokens)
    {
        _maxTokens = maxTokens;
        return this;
    }

    public ChatCompletionRequestBuilder SetTemperature(double temperature)
    {
        _temperature = temperature;
        return this;
    }

    public ChatCompletionRequestBuilder SetTopP(double topP)
    {
        _topP = topP;
        return this;
    }

    public ChatCompletionRequestBuilder AddTool(ToolDefinition tool)
    {
        _tools ??= new();
        _tools.Add(tool);
        return this;
    }

    public ChatCompletionRequestBuilder SetToolChoice(string toolChoice)
    {
        _toolChoice = toolChoice;
        return this;
    }

    public ChatCompletionRequestBuilder SetStream(bool stream)
    {
        _stream = stream;
        return this;
    }

    public ChatCompletionRequestBuilder AddStopSequence(string stop)
    {
        _stopSequences ??= new();
        _stopSequences.Add(stop);
        return this;
    }

    public ChatCompletionRequestBuilder SetSystemPrompt(string systemPrompt)
    {
        _systemPrompt = systemPrompt;
        return this;
    }

    public ChatCompletionRequest Build()
    {
        ArgumentNullException.ThrowIfNull(_model, nameof(_model));

        return new ChatCompletionRequest
        {
            Model = _model!,
            Messages = _messages.ToList(),
            MaxTokens = _maxTokens,
            Temperature = _temperature,
            TopP = _topP,
            Tools = _tools?.ToList(),
            ToolChoice = _toolChoice,
            Stream = _stream,
            StopSequences = _stopSequences?.ToList(),
            SystemPrompt = _systemPrompt
        };
    }
}
