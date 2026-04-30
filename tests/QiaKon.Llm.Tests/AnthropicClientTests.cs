using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using QiaKon.Llm;
using QiaKon.Llm.Providers;
using Xunit;

namespace QiaKon.Llm.Tests;

public class AnthropicClientTests
{
    [Fact]
    public async Task CompleteAsync_ShouldSupportReusedHttpClientWithoutMutatingSharedState()
    {
        // Arrange
        var handler = new CaptureHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": "msg_test",
                  "type": "message",
                  "model": "claude-3-5-sonnet-20241022",
                  "content": [
                    {
                      "type": "text",
                      "text": "ok"
                    }
                  ],
                  "stop_reason": "end_turn",
                  "usage": {
                    "input_tokens": 1,
                    "output_tokens": 1
                  }
                }
                """,
                Encoding.UTF8,
                "application/json")
        });
        using var sharedHttpClient = new HttpClient(handler);

        var firstClient = new AnthropicClient(sharedHttpClient, new LlmOptions
        {
            Provider = LlmProviderType.Anthropic,
            Model = "claude-3-5-sonnet-20241022",
            BaseUrl = "https://api.anthropic.com",
            ApiKey = "ant-first"
        });

        var secondClient = new AnthropicClient(sharedHttpClient, new LlmOptions
        {
            Provider = LlmProviderType.Anthropic,
            Model = "claude-3-5-sonnet-20241022",
            BaseUrl = "https://api.anthropic.com/v1",
            ApiKey = "ant-second"
        });

        // Act
        await firstClient.CompleteAsync(new ChatCompletionRequest
        {
            Model = "claude-3-5-sonnet-20241022",
            Messages = [ChatMessage.User("hello")]
        });

        await secondClient.CompleteAsync(new ChatCompletionRequest
        {
            Model = "claude-3-5-sonnet-20241022",
            Messages = [ChatMessage.User("hello")]
        });

        // Assert
        handler.RequestUris.Should().Equal(
            new Uri("https://api.anthropic.com/v1/messages"),
            new Uri("https://api.anthropic.com/v1/messages"));
        handler.ApiKeys.Should().Equal("ant-first", "ant-second");
        handler.AnthropicVersions.Should().Equal("2023-06-01", "2023-06-01");
    }

    private sealed class CaptureHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public CaptureHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<Uri> RequestUris { get; } = [];
        public List<string?> ApiKeys { get; } = [];
        public List<string?> AnthropicVersions { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
            {
                RequestUris.Add(request.RequestUri);
            }

            ApiKeys.Add(request.Headers.TryGetValues("x-api-key", out var apiKeys) ? apiKeys.SingleOrDefault() : null);
            AnthropicVersions.Add(request.Headers.TryGetValues("anthropic-version", out var versions) ? versions.SingleOrDefault() : null);
            return Task.FromResult(_responseFactory(request));
        }
    }
}