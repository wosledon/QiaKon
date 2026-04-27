using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using QiaKon.Llm;

namespace QiaKon.Llm.Providers;

/// <summary>
/// Anthropic Provider实现
/// </summary>
public sealed class AnthropicProvider : BaseLLMProvider
{
    private const string DefaultBaseUrl = "https://api.anthropic.com";
    private const string ApiVersion = "2023-06-01";
    private readonly LLMProviderConfig _config;
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public AnthropicProvider(LLMProviderConfig config, HttpClient? httpClient = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _baseUrl = config.BaseUrl ?? DefaultBaseUrl;
        ProviderName = config.Name ?? "Anthropic";
        ProviderType = ProviderType.Anthropic;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _config.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", config.ApiVersion ?? ApiVersion);

        // 添加自定义Headers
        if (config.CustomHeaders != null)
        {
            foreach (var (key, value) in config.CustomHeaders)
            {
                _httpClient.DefaultRequestHeaders.Add(key, value);
            }
        }
    }

    public override string ProviderName { get; }
    public override ProviderType ProviderType { get; }

    public override async Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildCompletionPayload(request);
        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/v1/messages",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseCompletionResponse(json, request.Model);
    }

    public override async IAsyncEnumerable<StreamEvent> CompleteStreamingAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamRequest = request with { Stream = true };
        var payload = BuildCompletionPayload(streamRequest);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/messages")
        {
            Content = JsonContent.Create(payload)
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        var eventType = "";
        var accumulatedText = new StringBuilder();
        var responseId = "";
        var responseModel = "";

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("event: "))
            {
                eventType = line.Substring(6).Trim();
                continue;
            }

            if (!line.StartsWith("data: "))
                continue;

            var data = line.Substring(6).Trim();
            var json = JsonDocument.Parse(data);

            switch (eventType)
            {
                case "message_start":
                    responseId = json.RootElement.GetProperty("message").GetProperty("id").GetString() ?? "";
                    responseModel = json.RootElement.GetProperty("message").GetProperty("model").GetString() ?? request.Model;
                    break;

                case "content_block_delta":
                    var delta = json.RootElement.GetProperty("delta");
                    if (delta.TryGetProperty("text", out var textElement))
                    {
                        var text = textElement.GetString() ?? "";
                        if (!string.IsNullOrEmpty(text))
                        {
                            accumulatedText.Append(text);
                        }
                        yield return new StreamEvent { DeltaText = text };
                    }
                    break;

                case "message_delta":
                    var delta_usage = json.RootElement.GetProperty("usage");
                    if (json.RootElement.TryGetProperty("delta", out var delta_stop) &&
                        delta_stop.TryGetProperty("stop_reason", out var stopReason))
                    {
                        yield return new StreamEvent
                        {
                            IsDone = true,
                            FinalResponse = new ChatCompletionResponse
                            {
                                Id = responseId,
                                Model = responseModel,
                                FinishReason = stopReason.GetString() ?? "end_turn",
                                Message = ChatMessage.Assistant(accumulatedText.ToString()),
                                Usage = new UsageStats
                                {
                                    PromptTokens = delta_usage.GetProperty("input_tokens").GetInt32(),
                                    CompletionTokens = delta_usage.GetProperty("output_tokens").GetInt32(),
                                    TotalTokens = delta_usage.GetProperty("input_tokens").GetInt32() +
                                                  delta_usage.GetProperty("output_tokens").GetInt32()
                                }
                            }
                        };
                    }
                    break;
            }
        }
    }

    public override Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        // Anthropic没有公开的模型列表API，返回已知模型
        return Task.FromResult<IReadOnlyList<string>>(new[]
        {
            "claude-3-5-sonnet-20241022",
            "claude-3-5-haiku-20241022",
            "claude-3-opus-20240229",
            "claude-3-sonnet-20240229",
            "claude-3-haiku-20240307"
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient?.Dispose();
        }
    }

    private object BuildCompletionPayload(ChatCompletionRequest request)
    {
        var messages = new List<object>();

        foreach (var msg in request.Messages)
        {
            if (msg.Role == MessageRole.System)
                continue; // Anthropic的系统提示词单独传递

            var textContent = msg.GetTextContent();
            messages.Add(new
            {
                role = msg.Role == MessageRole.Assistant ? "assistant" : "user",
                content = textContent ?? ""
            });
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens ?? 4096,
            ["messages"] = messages,
            ["temperature"] = request.Temperature ?? 1.0,
            ["top_p"] = request.TopP ?? 0.9,
            ["stream"] = request.Stream
        };

        // 系统提示词
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            payload["system"] = request.SystemPrompt;
        }

        // 停止序列
        if (request.StopSequences?.Count > 0)
        {
            payload["stop_sequences"] = request.StopSequences;
        }

        return payload;
    }

    private ChatCompletionResponse ParseCompletionResponse(string json, string requestedModel)
    {
        var response = JsonSerializer.Deserialize<AnthropicCompletionResponse>(json);
        if (response == null)
        {
            throw new InvalidOperationException("Invalid response from Anthropic API");
        }

        var contentText = response.Content?.FirstOrDefault(c => c.Type == "text")?.Text ?? "";

        return new ChatCompletionResponse
        {
            Id = response.Id,
            Model = response.Model ?? requestedModel,
            FinishReason = response.StopReason ?? "end_turn",
            Message = ChatMessage.Assistant(contentText),
            Usage = response.Usage != null ? new UsageStats
            {
                PromptTokens = response.Usage.InputTokens,
                CompletionTokens = response.Usage.OutputTokens,
                TotalTokens = response.Usage.InputTokens + response.Usage.OutputTokens
            } : null
        };
    }

    private sealed record AnthropicCompletionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("role")]
        public string Role { get; init; } = "";

        [JsonPropertyName("content")]
        public List<AnthropicContentBlock>? Content { get; init; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; init; }

        [JsonPropertyName("usage")]
        public AnthropicUsage? Usage { get; init; }
    }

    private sealed record AnthropicContentBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "";

        [JsonPropertyName("text")]
        public string Text { get; init; } = "";
    }

    private sealed record AnthropicUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; init; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; init; }
    }
}
