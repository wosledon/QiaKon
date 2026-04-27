using System.Text.Json;

namespace QiaKon.Llm;

/// <summary>
/// Agent执行引擎
/// </summary>
public sealed class LlmAgentEngine : ILlmAgent
{
    private readonly ILlmClient _client;
    private readonly AgentConfiguration _config;
    private readonly List<ChatMessage> _messages = new();

    public string Name => _config.Name;

    public LlmAgentEngine(ILlmClient client, AgentConfiguration config)
    {
        _client = client;
        _config = config;
    }

    public async Task<AgentResponse> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        // 初始化消息列表
        if (request.Messages != null)
        {
            _messages.Clear();
            _messages.AddRange(request.Messages);
        }

        // 添加用户消息
        var userMessage = ChatMessage.User(request.UserInput);
        _messages.Add(userMessage);
        request.OnMessageAdded?.Invoke(userMessage);

        var toolResults = new List<ToolExecutionResult>();
        var maxTurns = request.MaxTurns > 0 ? request.MaxTurns : _config.MaxTurns;

        for (int turn = 0; turn < maxTurns && !cancellationToken.IsCancellationRequested; turn++)
        {
            try
            {
                var llmRequest = BuildRequest(request, toolResults);
                var response = await _client.CompleteAsync(llmRequest, cancellationToken);

                var assistantMessage = response.Message;
                _messages.Add(assistantMessage);
                request.OnMessageAdded?.Invoke(assistantMessage);

                // 检查是否需要工具调用
                var toolCalls = ExtractToolCalls(assistantMessage);
                if (toolCalls.Count == 0)
                {
                    return new AgentResponse
                    {
                        Response = assistantMessage.GetTextContent(),
                        IsComplete = true,
                        Turns = turn + 1,
                        ToolResults = toolResults
                    };
                }

                // 执行工具
                foreach (var toolCall in toolCalls)
                {
                    var tool = request.Tools?.FirstOrDefault(t => t.Name == toolCall.Name);
                    if (tool == null)
                    {
                        toolResults.Add(new ToolExecutionResult
                        {
                            ToolName = toolCall.Name,
                            ToolCallId = toolCall.Id,
                            Result = $"Tool '{toolCall.Name}' not found",
                            IsError = true
                        });
                        continue;
                    }

                    try
                    {
                        var result = await tool.Executor(toolCall.Name, toolCall.ArgumentsJson ?? "{}", cancellationToken);
                        toolResults.Add(result);
                        var toolMessage = ChatMessage.Tool(result.Result, toolCall.Id);
                        _messages.Add(toolMessage);
                        request.OnMessageAdded?.Invoke(toolMessage);
                    }
                    catch (Exception ex)
                    {
                        toolResults.Add(new ToolExecutionResult
                        {
                            ToolName = toolCall.Name,
                            ToolCallId = toolCall.Id,
                            Result = ex.Message,
                            IsError = true
                        });
                    }
                }
            }
            catch (LlmRetryableException ex)
            {
                return new AgentResponse
                {
                    Response = "",
                    IsComplete = false,
                    Turns = turn,
                    ToolResults = toolResults,
                    Error = $"Retryable error after {ex.RetryCount} retries: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new AgentResponse
                {
                    Response = "",
                    IsComplete = false,
                    Turns = turn,
                    ToolResults = toolResults,
                    Error = ex.Message
                };
            }
        }

        // 移除最后添加的用户消息（如果需要）
        if (_messages.Count > 0 && _messages[^1].Role == MessageRole.User)
        {
            _messages.RemoveAt(_messages.Count - 1);
        }

        return new AgentResponse
        {
            Response = _messages.LastOrDefault()?.GetTextContent() ?? "",
            IsComplete = false,
            Turns = maxTurns,
            ToolResults = toolResults,
            Error = "Max turns exceeded"
        };
    }

    private ChatCompletionRequest BuildRequest(
        AgentRequest request,
        List<ToolExecutionResult> toolResults)
    {
        var inferenceOptions = request.InferenceOptions ?? _config.InferenceOptions ?? new LlmInferenceOptions();

        return new ChatCompletionRequest
        {
            Model = _client.Model,
            Messages = _messages.ToList(),
            InferenceOptions = inferenceOptions,
            Tools = request.Tools?.Select(t => new ToolDefinition
            {
                Name = t.Name,
                Description = t.Description,
                ParametersJsonSchema = t.ParametersJsonSchema
            }).ToList()
        };
    }

    private static List<ToolCallInfo> ExtractToolCalls(ChatMessage message)
    {
        var calls = new List<ToolCallInfo>();

        foreach (var block in message.ContentBlocks)
        {
            if (block is ToolCallContentBlock toolCall)
            {
                calls.Add(new ToolCallInfo
                {
                    Id = toolCall.Id,
                    Name = toolCall.Name,
                    ArgumentsJson = toolCall.ArgumentsJson
                });
            }
        }

        // 如果是文本消息，尝试解析其中的tool call
        if (calls.Count == 0 && message.Role == MessageRole.Assistant)
        {
            var text = message.GetTextContent();
            // 简单的JSON解析尝试
            if (text.Contains("\"tool_calls\""))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("tool_calls", out var toolCalls))
                    {
                        foreach (var call in toolCalls.EnumerateArray())
                        {
                            var id = call.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
                            var name = call.GetProperty("function").GetProperty("name").GetString() ?? "";
                            var args = call.GetProperty("function").GetProperty("arguments").GetString();
                            calls.Add(new ToolCallInfo { Id = id, Name = name, ArgumentsJson = args });
                        }
                    }
                }
                catch { }
            }
        }

        return calls;
    }

    private record ToolCallInfo
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public string? ArgumentsJson { get; init; }
    }
}

/// <summary>
/// Agent配置
/// </summary>
public sealed record AgentConfiguration
{
    public required string Name { get; init; }
    public LlmInferenceOptions? InferenceOptions { get; init; }
    public int MaxTurns { get; init; } = 10;
}

/// <summary>
/// Agent构建器
/// </summary>
public sealed class AgentBuilder
{
    private readonly ILlmClient _client;
    private readonly AgentConfiguration _config;

    internal AgentBuilder(ILlmClient client, AgentConfiguration config)
    {
        _client = client;
        _config = config;
    }

    public static AgentBuilder Create(ILlmClient client, string name)
    {
        return new AgentBuilder(client, new AgentConfiguration { Name = name });
    }

    public AgentBuilder WithMaxTurns(int maxTurns)
    {
        return new AgentBuilder(_client, _config with { MaxTurns = maxTurns });
    }

    public AgentBuilder WithInferenceOptions(LlmInferenceOptions options)
    {
        return new AgentBuilder(_client, _config with { InferenceOptions = options });
    }

    public ILlmAgent Build()
    {
        return new LlmAgentEngine(_client, _config);
    }
}
