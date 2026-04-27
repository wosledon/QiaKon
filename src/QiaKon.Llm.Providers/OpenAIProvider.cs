using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using QiaKon.Llm;

namespace QiaKon.Llm.Providers;

/// <summary>
/// OpenAI兼容API Provider实现（支持OpenAI、Azure、Ollama、LocalAI等）
/// </summary>
public sealed class OpenAIProvider : BaseLLMProvider
{
    private const string DefaultBaseUrl = "https://api.openai.com/v1";
    private readonly LLMProviderConfig _config;
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public OpenAIProvider(LLMProviderConfig config, HttpClient? httpClient = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _baseUrl = config.BaseUrl ?? DefaultBaseUrl;
        ProviderName = config.Name ?? DetectProviderName(_baseUrl);
        ProviderType = ProviderType.OpenAICompatible;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

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

    private static string DetectProviderName(string baseUrl)
    {
        var url = baseUrl.ToLowerInvariant();
        if (url.Contains("azure.com")) return "Azure OpenAI";
        if (url.Contains("ollama")) return "Ollama";
        if (url.Contains("localai")) return "LocalAI";
        return "OpenAI";
    }

    public override async Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildCompletionPayload(request);
        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/chat/completions",
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

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
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
        var accumulatedText = new StringBuilder();

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("data: "))
                continue;

            var data = line.Substring(6).Trim();
            if (data == "[DONE]")
            {
                yield return new StreamEvent { IsDone = true };
                break;
            }

            var json = JsonSerializer.Deserialize<OpenAIStreamResponse>(data);
            if (json?.Choices?.Count > 0)
            {
                var choice = json.Choices[0];
                var delta = choice.Delta;
                var content = delta?.Content ?? "";

                if (!string.IsNullOrEmpty(content))
                {
                    accumulatedText.Append(content);
                }

                yield return new StreamEvent
                {
                    DeltaText = content,
                    IsDone = choice.FinishReason != null
                };
            }
        }
    }

    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/models", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var modelsResponse = JsonSerializer.Deserialize<OpenAIModelsResponse>(json);

        return modelsResponse?.Data?.Select(m => m.Id).ToList() ?? new List<string>();
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

        // 系统提示词
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            messages.Add(new { role = "system", content = request.SystemPrompt });
        }

        // 其他消息
        foreach (var msg in request.Messages)
        {
            if (msg.Role == MessageRole.System && !string.IsNullOrEmpty(request.SystemPrompt))
                continue;

            var textContent = msg.GetTextContent();
            messages.Add(new
            {
                role = msg.Role.ToString().ToLowerInvariant(),
                content = textContent
            });
        }

        var payload = new
        {
            model = request.Model,
            messages,
            max_tokens = request.MaxTokens,
            temperature = request.Temperature,
            top_p = request.TopP,
            stream = request.Stream,
            stop = request.StopSequences
        };

        return payload;
    }

    private ChatCompletionResponse ParseCompletionResponse(string json, string requestedModel)
    {
        var response = JsonSerializer.Deserialize<OpenAICompletionResponse>(json);
        if (response == null || response.Choices.Count == 0)
        {
            throw new InvalidOperationException("Invalid response from OpenAI API");
        }

        var choice = response.Choices[0];
        var message = choice.Message;

        return new ChatCompletionResponse
        {
            Id = response.Id,
            Model = response.Model ?? requestedModel,
            FinishReason = choice.FinishReason ?? "stop",
            Message = ChatMessage.Assistant(message.Content ?? ""),
            Usage = response.Usage != null ? new UsageStats
            {
                PromptTokens = response.Usage.PromptTokens,
                CompletionTokens = response.Usage.CompletionTokens,
                TotalTokens = response.Usage.TotalTokens
            } : null
        };
    }

    private sealed record OpenAICompletionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("choices")]
        public List<OpenAIChoice> Choices { get; init; } = new();

        [JsonPropertyName("usage")]
        public OpenAIUsage? Usage { get; init; }
    }

    private sealed record OpenAIChoice
    {
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; init; }

        [JsonPropertyName("message")]
        public OpenAIMessage Message { get; init; } = new();
    }

    private sealed record OpenAIMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = "";

        [JsonPropertyName("content")]
        public string Content { get; init; } = "";
    }

    private sealed record OpenAIUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; init; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; init; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; init; }
    }

    private sealed record OpenAIStreamResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAIStreamChoice> Choices { get; init; } = new();
    }

    private sealed record OpenAIStreamChoice
    {
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; init; }

        [JsonPropertyName("delta")]
        public OpenAIDelta? Delta { get; init; }
    }

    private sealed record OpenAIDelta
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }

    private sealed record OpenAIModelsResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAIModel> Data { get; init; } = new();
    }

    private sealed record OpenAIModel
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";
    }
}
