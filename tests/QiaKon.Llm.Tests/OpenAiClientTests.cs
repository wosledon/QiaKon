using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using QiaKon.Llm;
using QiaKon.Llm.Providers;
using Xunit;

namespace QiaKon.Llm.Tests;

public class OpenAiClientTests
{
    [Theory]
    [InlineData("https://api.openai.com/v1", "https://api.openai.com/v1/chat/completions")]
    [InlineData("https://dashscope.aliyuncs.com/compatible-mode/api/v1", "https://dashscope.aliyuncs.com/compatible-mode/api/v1/chat/completions")]
    [InlineData("https://api.openai.com", "https://api.openai.com/v1/chat/completions")]
    public async Task CompleteAsync_ShouldComposeExpectedEndpoint(string baseUrl, string expectedUrl)
    {
        // Arrange
        var handler = new CaptureHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": "chatcmpl-test",
                      "model": "gpt-4o-mini",
                      "choices": [
                        {
                          "index": 0,
                          "message": {
                            "role": "assistant",
                            "content": "ok"
                          },
                          "finish_reason": "stop"
                        }
                      ],
                      "usage": {
                        "prompt_tokens": 1,
                        "completion_tokens": 1,
                        "total_tokens": 2
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAiClient(httpClient, new LlmOptions
        {
            Provider = LlmProviderType.OpenAI,
            Model = "gpt-4o-mini",
            BaseUrl = baseUrl,
            ApiKey = "sk-test"
        });

        // Act
        var response = await client.CompleteAsync(new ChatCompletionRequest
        {
            Model = "gpt-4o-mini",
            Messages = [ChatMessage.User("hello")]
        });

        // Assert
        handler.LastRequestUri.Should().NotBeNull();
        handler.LastRequestUri!.ToString().Should().Be(expectedUrl);
        response.Message.GetTextContent().Should().Be("ok");
    }

    [Fact]
    public async Task CompleteAsync_ShouldSupportReusedHttpClientWithoutMutatingSharedState()
    {
        // Arrange
        var handler = new CaptureHttpMessageHandler(_ => CreateSuccessResponse("ok"));
        using var sharedHttpClient = new HttpClient(handler);

        var firstClient = new OpenAiClient(sharedHttpClient, new LlmOptions
        {
            Provider = LlmProviderType.OpenAI,
            Model = "gpt-4o-mini",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "sk-first",
            Organization = "org-first"
        });

        var secondClient = new OpenAiClient(sharedHttpClient, new LlmOptions
        {
            Provider = LlmProviderType.OpenAI,
            Model = "qwen-max",
            BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
            ApiKey = "sk-second",
            Organization = "org-second"
        });

        // Act
        await firstClient.CompleteAsync(new ChatCompletionRequest
        {
            Model = "gpt-4o-mini",
            Messages = [ChatMessage.User("hello")]
        });

        await secondClient.CompleteAsync(new ChatCompletionRequest
        {
            Model = "qwen-max",
            Messages = [ChatMessage.User("hello")]
        });

        // Assert
        handler.RequestUris.Should().HaveCount(2);
        handler.RequestUris[0].ToString().Should().Be("https://api.openai.com/v1/chat/completions");
        handler.RequestUris[1].ToString().Should().Be("https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions");
        handler.AuthorizationHeaders.Should().Equal("Bearer sk-first", "Bearer sk-second");
        handler.OrganizationHeaders.Should().Equal("org-first", "org-second");
    }

    private static HttpResponseMessage CreateSuccessResponse(string content)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                {
                  "id": "chatcmpl-test",
                  "model": "gpt-4o-mini",
                  "choices": [
                    {
                      "index": 0,
                      "message": {
                        "role": "assistant",
                        "content": "{{content}}"
                      },
                      "finish_reason": "stop"
                    }
                  ],
                  "usage": {
                    "prompt_tokens": 1,
                    "completion_tokens": 1,
                    "total_tokens": 2
                  }
                }
                """,
                Encoding.UTF8,
                "application/json")
        };

    private sealed class CaptureHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public CaptureHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public Uri? LastRequestUri { get; private set; }
        public List<Uri> RequestUris { get; } = [];
        public List<string?> AuthorizationHeaders { get; } = [];
        public List<string?> OrganizationHeaders { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            if (request.RequestUri is not null)
            {
                RequestUris.Add(request.RequestUri);
            }

            AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());
            OrganizationHeaders.Add(request.Headers.TryGetValues("OpenAI-Organization", out var values) ? values.SingleOrDefault() : null);
            return Task.FromResult(_responseFactory(request));
        }
    }
}