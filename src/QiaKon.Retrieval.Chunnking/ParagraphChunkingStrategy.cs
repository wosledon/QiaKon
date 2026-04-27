using QiaKon.Retrieval;

namespace QiaKon.Retrieval.Chunnking;

/// <summary>
/// 基于段落的分块策略
/// 适用场景：结构清晰的文档（Markdown、HTML等），保持段落完整性
/// </summary>
public sealed class ParagraphChunkingStrategy : IChunkingStrategy
{
    private readonly ParagraphChunkingOptions _options;

    public string Name => "Paragraph";

    public ParagraphChunkingStrategy(ParagraphChunkingOptions? options = null)
    {
        _options = options ?? new ParagraphChunkingOptions();
    }

    public Task<IReadOnlyList<IChunk>> ChunkAsync(
        Guid documentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var chunks = new List<IChunk>();

        if (string.IsNullOrEmpty(content))
            return Task.FromResult<IReadOnlyList<IChunk>>(chunks);

        // 按段落分割（支持 \n\n 或 \r\n\r\n）
        var paragraphs = content.Split(
            new[] { "\n\n", "\r\n\r\n" },
            StringSplitOptions.RemoveEmptyEntries);

        int sequence = 0;
        int currentIndex = 0;
        var currentBuffer = new List<string>();
        int currentBufferLength = 0;

        foreach (var paragraph in paragraphs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var trimmedParagraph = paragraph.Trim();
            if (string.IsNullOrWhiteSpace(trimmedParagraph))
                continue;

            // 如果当前段落本身就超过最大大小，需要进一步拆分
            if (trimmedParagraph.Length > _options.MaxChunkSize)
            {
                // 先刷新缓冲区
                if (currentBuffer.Count > 0)
                {
                    FlushBuffer(documentId, content, ref currentIndex, ref sequence, currentBuffer, currentBufferLength, chunks);
                    currentBuffer.Clear();
                    currentBufferLength = 0;
                }

                // 将大段落按句子拆分
                var sentenceChunks = SplitLargeParagraph(documentId, trimmedParagraph, ref currentIndex, ref sequence);
                chunks.AddRange(sentenceChunks);
                continue;
            }

            // 检查添加当前段落后是否超过最大大小
            int projectedLength = currentBufferLength + (currentBuffer.Count > 0 ? 2 : 0) + trimmedParagraph.Length;

            if (projectedLength > _options.MaxChunkSize && currentBuffer.Count > 0)
            {
                // 刷新缓冲区
                FlushBuffer(documentId, content, ref currentIndex, ref sequence, currentBuffer, currentBufferLength, chunks);
                currentBuffer.Clear();
                currentBufferLength = 0;
            }

            currentBuffer.Add(trimmedParagraph);
            currentBufferLength += (currentBuffer.Count > 1 ? 2 : 0) + trimmedParagraph.Length;
        }

        // 刷新剩余内容
        if (currentBuffer.Count > 0)
        {
            FlushBuffer(documentId, content, ref currentIndex, ref sequence, currentBuffer, currentBufferLength, chunks);
        }

        return Task.FromResult<IReadOnlyList<IChunk>>(chunks);
    }

    private void FlushBuffer(
        Guid documentId,
        string content,
        ref int currentIndex,
        ref int sequence,
        List<string> buffer,
        int bufferLength,
        List<IChunk> chunks)
    {
        var text = string.Join("\n\n", buffer);
        int startIndex = currentIndex;
        int endIndex = startIndex + bufferLength;

        // 尝试在原始内容中定位
        int foundIndex = content.IndexOf(text, startIndex, StringComparison.Ordinal);
        if (foundIndex >= 0)
        {
            startIndex = foundIndex;
            endIndex = foundIndex + text.Length;
        }

        chunks.Add(new Chunk
        {
            DocumentId = documentId,
            Text = text,
            StartIndex = startIndex,
            EndIndex = endIndex,
            Sequence = sequence++,
            Metadata = new Dictionary<string, object?>
            {
                ["strategy"] = Name,
                ["paragraphCount"] = buffer.Count,
                ["length"] = text.Length
            }
        });

        currentIndex = endIndex;
    }

    private List<IChunk> SplitLargeParagraph(
        Guid documentId,
        string paragraph,
        ref int currentIndex,
        ref int sequence)
    {
        var chunks = new List<IChunk>();
        var sentences = paragraph.Split(
            new[] { ". ", "? ", "! ", "。", "？", "！" },
            StringSplitOptions.RemoveEmptyEntries);

        var sentenceBuffer = new List<string>();
        int sentenceBufferLength = 0;

        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            int projectedLength = sentenceBufferLength + (sentenceBuffer.Count > 0 ? 2 : 0) + trimmed.Length;

            if (projectedLength > _options.MaxChunkSize && sentenceBuffer.Count > 0)
            {
                var text = string.Join(". ", sentenceBuffer) + ".";
                chunks.Add(new Chunk
                {
                    DocumentId = documentId,
                    Text = text,
                    StartIndex = currentIndex,
                    EndIndex = currentIndex + text.Length,
                    Sequence = sequence++,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["strategy"] = $"{Name}-Sentence",
                        ["length"] = text.Length
                    }
                });
                currentIndex += text.Length;
                sentenceBuffer.Clear();
                sentenceBufferLength = 0;
            }

            sentenceBuffer.Add(trimmed);
            sentenceBufferLength += (sentenceBuffer.Count > 1 ? 2 : 0) + trimmed.Length;
        }

        if (sentenceBuffer.Count > 0)
        {
            var text = string.Join(". ", sentenceBuffer) + (sentenceBuffer.Count == 1 && !sentenceBuffer[0].EndsWith(".") ? "." : "");
            chunks.Add(new Chunk
            {
                DocumentId = documentId,
                Text = text,
                StartIndex = currentIndex,
                EndIndex = currentIndex + text.Length,
                Sequence = sequence++,
                Metadata = new Dictionary<string, object?>
                {
                    ["strategy"] = $"{Name}-Sentence",
                    ["length"] = text.Length
                }
            });
            currentIndex += text.Length;
        }

        return chunks;
    }
}

/// <summary>
/// 段落分块策略配置
/// </summary>
public sealed class ParagraphChunkingOptions : ChunkingOptionsBase
{
    /// <summary>
    /// 段落分隔符模式（默认按双换行符分割）
    /// </summary>
    public string[] ParagraphSeparators { get; set; } = new[] { "\n\n", "\r\n\r\n" };
}
