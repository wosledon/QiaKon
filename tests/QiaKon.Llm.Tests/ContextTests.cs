using FluentAssertions;
using QiaKon.Llm;
using Xunit;

namespace QiaKon.Llm.Tests;

public class ContextTests
{
    [Fact]
    public void ChatMessage_ShouldHaveCorrectRole()
    {
        // Arrange
        var message = ChatMessage.User("Hello");

        // Assert
        message.Role.Should().Be(MessageRole.User);
        message.GetTextContent().Should().Be("Hello");
    }

    [Fact]
    public void ChatMessage_SystemMessage_ShouldHaveSystemRole()
    {
        // Arrange
        var message = ChatMessage.System("You are helpful");

        // Assert
        message.Role.Should().Be(MessageRole.System);
    }

    [Fact]
    public void LlmOptions_ShouldStoreRequiredProperties()
    {
        // Arrange
        var options = new LlmOptions
        {
            Provider = LlmProviderType.OpenAI,
            Model = "gpt-4",
            BaseUrl = "https://api.openai.com"
        };

        // Assert
        options.Provider.Should().Be(LlmProviderType.OpenAI);
        options.Model.Should().Be("gpt-4");
    }
}
