using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QiaKon.Llm.Providers;

/// <summary>
/// Anthropic API客户端
/// </summary>
public sealed class AnthropicClient : HttpLlmClientBase
{
    private const string MessagesPath = "messages";

    private static readonly JsonSerializerOptions AnthropicJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public override LlmProviderType Provider => LlmProviderType.Anthropic;

    public AnthropicClient(HttpClient httpClient, LlmOptions options)
        : base(httpClient, options)
    {
    }

    protected override void ConfigureHeaders(HttpClient client, LlmOptions options)
    {
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public override async Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = ExtractSystemPrompt(request.Messages);
        var messages = request.Messages.Where(m => m.Role != MessageRole.System).ToList();

        var apiRequest = new AnthropicMessageRequest
        {
            Model = request.Model,
            Messages = messages.Select(MapToAnthropicMessage).ToList(),
            MaxTokens = request.InferenceOptions?.MaxTokens ?? 1024,
            Temperature = request.InferenceOptions?.Temperature,
            TopP = request.InferenceOptions?.TopP,
            StopSequences = request.InferenceOptions?.StopSequences?.ToList(),
            System = systemPrompt
        };

        var response = await SendRequestAsync<AnthropicMessageResponse>(
            HttpMethod.Post,
            BuildVersionedRelativeUrl("v1", MessagesPath),
            apiRequest,
            cancellationToken);

        return MapToChatCompletionResponse(response);
    }

    public override async IAsyncEnumerable<ChatCompletionChunk> CompleteStreamAsync(
        ChatCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var systemPrompt = ExtractSystemPrompt(request.Messages);
        var messages = request.Messages.Where(m => m.Role != MessageRole.System).ToList();

        var apiRequest = new AnthropicMessageRequest
        {
            Model = request.Model,
            Messages = messages.Select(MapToAnthropicMessage).ToList(),
            MaxTokens = request.InferenceOptions?.MaxTokens ?? 1024,
            Temperature = request.InferenceOptions?.Temperature,
            TopP = request.InferenceOptions?.TopP,
            StopSequences = request.InferenceOptions?.StopSequences?.ToList(),
            System = systemPrompt,
            Stream = true
        };

        await foreach (var chunk in StreamRequestAsync(
            HttpMethod.Post,
            BuildVersionedRelativeUrl("v1", MessagesPath),
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
            var chunk = JsonSerializer.Deserialize<AnthropicStreamChunk>(data, AnthropicJsonOptions);
            if (chunk == null)
                return null;

            if (chunk.Type == "content_block_delta")
            {
                return new ChatCompletionChunk
                {
                    Content = chunk.Delta?.Text,
                    IsComplete = false
                };
            }
            else if (chunk.Type == "message_stop")
            {
                return new ChatCompletionChunk
                {
                    IsComplete = true,
                    FinishReason = "end_turn"
                };
            }
        }
        catch
        {
            // Ignore parse errors for stream chunks
        }

        return null;
    }

    private static string? ExtractSystemPrompt(IReadOnlyList<ChatMessage> messages)
    {
        var systemMessage = messages.FirstOrDefault(m => m.Role == MessageRole.System);
        return systemMessage?.GetTextContent();
    }

    private static AnthropicMessage MapToAnthropicMessage(ChatMessage message)
    {
        var content = message.Role == MessageRole.Tool
            ? new AnthropicContentBlock { Type = "tool_result", Id = message.ToolCallId, Content = message.GetTextContent() }
            : new AnthropicContentBlock { Type = "text", Text = message.GetTextContent() };

        return new AnthropicMessage
        {
            Role = message.Role switch
            {
                MessageRole.User => "user",
                MessageRole.Assistant => "assistant",
                MessageRole.Tool => "user",
                _ => "user"
            },
            Content = [content]
        };
    }

    private static ChatCompletionResponse MapToChatCompletionResponse(AnthropicMessageResponse response)
    {
        var contentText = string.Join("",
            response.Content
                .OfType<AnthropicContentBlock>()
                .Where(b => b.Type == "text")
                .Select(b => b.Text ?? ""));

        return new ChatCompletionResponse
        {
            Id = $"anthropic-{response.Id}",
            Model = response.Model ?? "",
            Message = new ChatMessage
            {
                Role = MessageRole.Assistant,
                ContentBlocks = string.IsNullOrEmpty(contentText)
                    ? []
                    : [new TextContentBlock { Text = contentText }]
            },
            UsagePromptTokens = response.Usage?.InputTokens ?? 0,
            UsageCompletionTokens = response.Usage?.OutputTokens ?? 0,
            UsageTotalTokens = (response.Usage?.InputTokens ?? 0) + (response.Usage?.OutputTokens ?? 0),
            FinishReason = response.StopReason
        };
    }
}

#region Anthropic API Models

internal sealed class AnthropicMessageRequest
{
    public string Model { get; set; } = "";
    public List<AnthropicMessage> Messages { get; set; } = [];
    public int MaxTokens { get; set; }
    public string? System { get; set; }
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public List<string>? StopSequences { get; set; }
    public bool Stream { get; set; }
}

internal sealed class AnthropicMessage
{
    public string Role { get; set; } = "";
    public List<AnthropicContentBlock> Content { get; set; } = [];
}

internal class AnthropicContentBlock
{
    public string Type { get; set; } = "";
    public string? Text { get; set; }
    public string? Id { get; set; }
    public string? Content { get; set; }
}

internal sealed class AnthropicMessageResponse
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Model { get; set; }
    public List<AnthropicContentBlock> Content { get; set; } = [];
    public string? StopReason { get; set; }
    public AnthropicUsage? Usage { get; set; }
}

internal sealed class AnthropicUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

internal sealed class AnthropicStreamChunk
{
    public string Type { get; set; } = "";
    public AnthropicStreamDelta? Delta { get; set; }
}

internal sealed class AnthropicStreamDelta
{
    public string? Text { get; set; }
}

#endregion
