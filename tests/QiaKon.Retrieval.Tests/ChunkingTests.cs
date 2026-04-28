using FluentAssertions;
using Xunit;

namespace QiaKon.Retrieval.Tests;

public class ChunkingTests
{
    [Fact]
    public void SimpleTextChunker_ShouldSplitTextIntoChunks()
    {
        // Arrange
        var text = "This is a long text that needs to be split into chunks.";
        var chunkSize = 10;

        // Act
        var chunks = SimpleChunker.Chunk(text, chunkSize).ToList();

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.Sum(c => c.Length).Should().Be(text.Length);
    }

    [Fact]
    public void SimpleChunker_EmptyText_ShouldReturnEmpty()
    {
        // Arrange
        var text = "";

        // Act
        var chunks = SimpleChunker.Chunk(text, 10).ToList();

        // Assert
        chunks.Should().BeEmpty();
    }

    [Fact]
    public void SimpleChunker_ShortText_ShouldReturnSingleChunk()
    {
        // Arrange
        var text = "Short";

        // Act
        var chunks = SimpleChunker.Chunk(text, 10).ToList();

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Should().Be("Short");
    }

    private static class SimpleChunker
    {
        public static IEnumerable<string> Chunk(string text, int chunkSize)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            for (int i = 0; i < text.Length; i += chunkSize)
            {
                yield return text.Substring(i, Math.Min(chunkSize, text.Length - i));
            }
        }
    }
}
