using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QiaKon.Llm.Providers;

/// <summary>
/// OpenAI兼容API客户端
/// </summary>
public sealed class OpenAiClient : HttpLlmClientBase
{
    public override LlmProviderType Provider => LlmProviderType.OpenAI;

    public OpenAiClient(HttpClient httpClient, LlmOptions options)
        : base(httpClient, options)
    {
    }

    protected override void ConfigureHeaders(HttpClient client, LlmOptions options)
    {
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");

        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }

        if (!string.IsNullOrEmpty(options.Organization))
        {
            client.DefaultRequestHeaders.Add("OpenAI-Organization", options.Organization);
        }
    }

    public override async Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var apiRequest = new OpenAiChatRequest
        {
            Model = request.Model,
            Messages = request.Messages.Select(MapToOpenAiMessage).ToList(),
            MaxTokens = request.InferenceOptions?.MaxTokens,
            Temperature = request.InferenceOptions?.Temperature,
            TopP = request.InferenceOptions?.TopP,
            Stop = request.InferenceOptions?.StopSequences,
            Stream = false,
            Tools = request.Tools?.Select(t => new OpenAiTool
            {
                Type = "function",
                Function = new OpenAiFunctionDefinition
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = JsonSerializer.Deserialize<OpenAiJsonSchema>(t.ParametersJsonSchema)
                }
            }).ToList()
        };

        var response = await SendRequestAsync<OpenAiChatResponse>(
            HttpMethod.Post,
            "v1/chat/completions",
            apiRequest,
            cancellationToken);

        return MapToChatCompletionResponse(response);
    }

    public override async IAsyncEnumerable<ChatCompletionChunk> CompleteStreamAsync(
        ChatCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var apiRequest = new OpenAiChatRequest
        {
            Model = request.Model,
            Messages = request.Messages.Select(MapToOpenAiMessage).ToList(),
            MaxTokens = request.InferenceOptions?.MaxTokens,
            Temperature = request.InferenceOptions?.Temperature,
            TopP = request.InferenceOptions?.TopP,
            Stop = request.InferenceOptions?.StopSequences,
            Stream = true
        };

        await foreach (var chunk in StreamRequestAsync(
            HttpMethod.Post,
            "v1/chat/completions",
            apiRequest,
            cancellationToken))
        {
            yield return chunk;
        }
    }

    protected override ChatCompletionChunk? ParseStreamChunk(string data)
    {
        try
        {
            var delta = JsonSerializer.Deserialize<OpenAiStreamDelta>(data, JsonOptions);
            if (delta == null)
                return null;

            return new ChatCompletionChunk
            {
                Id = delta.Id,
                Model = delta.Model,
                Content = delta.Choices?.FirstOrDefault()?.Delta?.Content,
                IsComplete = delta.Choices?.FirstOrDefault()?.FinishReason != null,
                FinishReason = delta.Choices?.FirstOrDefault()?.FinishReason
            };
        }
        catch
        {
            return null;
        }
    }

    private static OpenAiMessage MapToOpenAiMessage(ChatMessage message)
    {
        var content = string.Join("", message.ContentBlocks.OfType<TextContentBlock>().Select(b => b.Text));

        return new OpenAiMessage
        {
            Role = message.Role switch
            {
                MessageRole.System => "system",
                MessageRole.User => "user",
                MessageRole.Assistant => "assistant",
                MessageRole.Tool => "tool",
                _ => "user"
            },
            Content = content,
            Name = message.Name,
            ToolCallId = message.ToolCallId
        };
    }

    private static ChatCompletionResponse MapToChatCompletionResponse(OpenAiChatResponse response)
    {
        var choice = response.Choices?.FirstOrDefault();
        var message = choice?.Message;

        return new ChatCompletionResponse
        {
            Id = response.Id ?? "",
            Model = response.Model ?? "",
            Message = new ChatMessage
            {
                Role = message?.Role switch
                {
                    "system" => MessageRole.System,
                    "user" => MessageRole.User,
                    "assistant" => MessageRole.Assistant,
                    "tool" => MessageRole.Tool,
                    _ => MessageRole.Assistant
                },
                ContentBlocks = string.IsNullOrEmpty(message?.Content)
                    ? []
                    : [new TextContentBlock { Text = message.Content! }]
            },
            UsagePromptTokens = response.Usage?.PromptTokens ?? 0,
            UsageCompletionTokens = response.Usage?.CompletionTokens ?? 0,
            UsageTotalTokens = response.Usage?.TotalTokens ?? 0,
            FinishReason = choice?.FinishReason
        };
    }
}

#region OpenAI API Models

internal sealed class OpenAiChatRequest
{
    public string Model { get; set; } = "";
    public List<OpenAiMessage> Messages { get; set; } = [];
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public IReadOnlyList<string>? Stop { get; set; }
    public bool Stream { get; set; }
    public List<OpenAiTool>? Tools { get; set; }
}

internal sealed class OpenAiMessage
{
    public string Role { get; set; } = "";
    public string? Content { get; set; }
    public string? Name { get; set; }
    public string? ToolCallId { get; set; }
}

internal sealed class OpenAiTool
{
    public string Type { get; set; } = "function";
    public OpenAiFunctionDefinition Function { get; set; } = new();
}

internal sealed class OpenAiFunctionDefinition
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public OpenAiJsonSchema? Parameters { get; set; }
}

internal class OpenAiJsonSchema
{
    public string Type { get; set; } = "object";
    public Dictionary<string, object>? Properties { get; set; }
    public List<string>? Required { get; set; }
}

internal sealed class OpenAiChatResponse
{
    public string? Id { get; set; }
    public string? Model { get; set; }
    public List<OpenAiChoice>? Choices { get; set; }
    public OpenAiUsage? Usage { get; set; }
}

internal sealed class OpenAiChoice
{
    public int Index { get; set; }
    public OpenAiMessage? Message { get; set; }
    public string? FinishReason { get; set; }
}

internal sealed class OpenAiUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

internal sealed class OpenAiStreamDelta
{
    public string? Id { get; set; }
    public string? Model { get; set; }
    public List<OpenAiStreamChoice>? Choices { get; set; }
}

internal sealed class OpenAiStreamChoice
{
    public int Index { get; set; }
    public OpenAiStreamDeltaContent? Delta { get; set; }
    public string? FinishReason { get; set; }
}

internal sealed class OpenAiStreamDeltaContent
{
    public string? Content { get; set; }
}

#endregion
