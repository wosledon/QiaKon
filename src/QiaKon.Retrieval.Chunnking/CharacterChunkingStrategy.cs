using QiaKon.Retrieval;

namespace QiaKon.Retrieval.Chunnking;

/// <summary>
/// 基于固定字符长度的滑动窗口分块策略
/// 适用场景：通用文本，追求速度
/// </summary>
public sealed class CharacterChunkingStrategy : IChunkingStrategy
{
    private readonly CharacterChunkingOptions _options;

    public string Name => "Character";

    public CharacterChunkingStrategy(CharacterChunkingOptions? options = null)
    {
        _options = options ?? new CharacterChunkingOptions();
    }

    public Task<IReadOnlyList<IChunk>> ChunkAsync(
        Guid documentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var chunks = new List<IChunk>();
        var maxSize = _options.MaxChunkSize;
        var overlap = _options.OverlapSize;

        if (string.IsNullOrEmpty(content))
            return Task.FromResult<IReadOnlyList<IChunk>>(chunks);

        int index = 0;
        int sequence = 0;

        while (index < content.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int remaining = content.Length - index;
            int chunkSize = Math.Min(maxSize, remaining);

            // 如果剩余内容大于最小块大小，尝试在句子或单词边界截断
            if (remaining > _options.MinChunkSize && chunkSize < remaining)
            {
                chunkSize = FindBreakPoint(content, index, chunkSize);
            }

            var text = content.Substring(index, chunkSize);
            chunks.Add(new Chunk
            {
                DocumentId = documentId,
                Text = text.Trim(),
                StartIndex = index,
                EndIndex = index + chunkSize,
                Sequence = sequence++,
                Metadata = new Dictionary<string, object?>
                {
                    ["strategy"] = Name,
                    ["length"] = text.Length
                }
            });

            // 滑动窗口：下一个块的起始位置 = 当前结束位置 - 重叠大小
            index += Math.Max(1, chunkSize - overlap);

            // 避免无限循环
            if (chunkSize <= overlap && index < content.Length)
            {
                index = Math.Min(index + chunkSize, content.Length);
            }
        }

        return Task.FromResult<IReadOnlyList<IChunk>>(chunks);
    }

    /// <summary>
    /// 在指定范围内寻找最佳截断点（优先句子边界，其次单词边界）
    /// </summary>
    private int FindBreakPoint(string content, int start, int preferredSize)
    {
        int searchEnd = Math.Min(start + preferredSize, content.Length);
        int minSize = _options.MinChunkSize;

        // 向后搜索句子结束符（句号、问号、感叹号）
        for (int i = searchEnd - 1; i >= start + minSize; i--)
        {
            if (i < content.Length - 1)
            {
                char c = content[i];
                char next = content[i + 1];
                if ((c == '.' || c == '?' || c == '!') && char.IsWhiteSpace(next))
                {
                    return i + 1 - start;
                }
            }
        }

        // 向后搜索换行符
        for (int i = searchEnd - 1; i >= start + minSize; i--)
        {
            if (content[i] == '\n')
            {
                return i - start;
            }
        }

        // 向后搜索空格
        for (int i = searchEnd - 1; i >= start + minSize; i--)
        {
            if (char.IsWhiteSpace(content[i]))
            {
                return i - start;
            }
        }

        // 未找到合适的截断点，使用原始大小
        return preferredSize;
    }
}

/// <summary>
/// 字符分块策略配置
/// </summary>
public sealed class CharacterChunkingOptions : ChunkingOptionsBase
{
}
