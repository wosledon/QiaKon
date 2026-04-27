using System.Text.Json;
using Microsoft.Extensions.Logging;
using QiaKon.Llm;
using QiaKon.Retrieval;

namespace QiaKon.Retrieval.Chunnking.MoE;

/// <summary>
/// MoE（Mixture of Experts）智能分块策略
///
/// 核心设计：
/// 1. 使用小体积大模型进行语义理解分块，而非简单的字符/段落切割
/// 2. 模型根据文档语义结构（主题切换、逻辑边界）决定分块位置
/// 3. 支持全模态输入——当 SkipDocumentProcessing=true 时，可直接接收原始文件
/// 4. LLM 客户端由调用方从数据库读取配置后创建，直接传入 MoE 使用
///
/// 与传统分块的区别：
/// - 传统分块：基于字符/段落/句子等固定规则切割，可能切断语义
/// - MoE 分块：基于 LLM 语义理解，在主题边界处切割，保留语义完整性
/// </summary>
public sealed class MoEChunkingStrategy : IMoEChunkingStrategy, IAsyncDisposable
{
    private readonly ILlmClient _llmClient;
    private readonly MoEChunkingOptions _options;
    private readonly ILogger<MoEChunkingStrategy>? _logger;

    public string Name => "MoE";

    /// <summary>
    /// 构造函数：直接传入 ILlmClient 实例
    /// LLM 客户端由调用方从数据库读取配置后创建，直接传入使用
    /// </summary>
    public MoEChunkingStrategy(ILlmClient llmClient, MoEChunkingOptions options, ILogger<MoEChunkingStrategy>? logger = null)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<IReadOnlyList<IChunk>> ChunkAsync(
        Guid documentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<IChunk>();

        _logger?.LogDebug("MoE 智能分块开始，文档长度: {Length} 字符", content.Length);

        // 如果内容较短，直接单次分块
        if (content.Length <= _options.MaxChunkSize * 2)
        {
            return await ChunkSingleAsync(documentId, content, cancellationToken);
        }

        // 长文档采用递归分块策略
        return await ChunkRecursiveAsync(documentId, content, cancellationToken);
    }

    /// <summary>
    /// 单次分块：适用于短文档
    /// </summary>
    private async Task<IReadOnlyList<IChunk>> ChunkSingleAsync(
        Guid documentId,
        string content,
        CancellationToken cancellationToken)
    {
        var prompt = BuildChunkingPrompt(content, _options.MaxChunkSize);

        var request = new ChatCompletionRequest
        {
            Model = _llmClient.Model,
            Messages = new[]
            {
                ChatMessage.System("你是一个智能文档分块专家。你的任务是将文档内容按照语义边界切分为多个块。每个块应该围绕一个主题或概念，保持语义完整性。"),
                ChatMessage.User(prompt)
            },
            InferenceOptions = new LlmInferenceOptions
            {
                Temperature = 0.1,
                MaxTokens = 4096
            }
        };

        var response = await _llmClient.CompleteAsync(request, cancellationToken);
        var result = ParseChunkingResult(response.Message.GetTextContent() ?? string.Empty, documentId);

        _logger?.LogDebug("MoE 单次分块完成，生成 {Count} 个块", result.Count);
        return result;
    }

    /// <summary>
    /// 递归分块：适用于长文档
    /// 1. 将文档分为多个窗口（每个窗口约 MaxChunkSize * 3 字符）
    /// 2. 对每个窗口调用 LLM 分块
    /// 3. 合并重叠区域的块，处理边界
    /// </summary>
    private async Task<IReadOnlyList<IChunk>> ChunkRecursiveAsync(
        Guid documentId,
        string content,
        CancellationToken cancellationToken)
    {
        var allChunks = new List<IChunk>();
        var windowSize = _options.MaxChunkSize * 3;
        var overlap = _options.MaxChunkSize;
        int index = 0;
        int sequence = 0;

        while (index < content.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int windowEnd = Math.Min(index + windowSize, content.Length);
            string window = content.Substring(index, windowEnd - index);

            _logger?.LogDebug("MoE 递归分块窗口 [{Start}-{End}]，长度: {Length}",
                index, windowEnd, window.Length);

            // 构建窗口提示词，标注这是文档的哪个部分
            var prompt = BuildChunkingPrompt(window, _options.MaxChunkSize, index == 0, windowEnd >= content.Length);

            var request = new ChatCompletionRequest
            {
                Model = _llmClient.Model,
                Messages = new[]
                {
                    ChatMessage.System("你是一个智能文档分块专家。你的任务是将文档内容按照语义边界切分为多个块。每个块应该围绕一个主题或概念，保持语义完整性。注意处理窗口边界，避免在句子中间截断。"),
                    ChatMessage.User(prompt)
                },
                InferenceOptions = new LlmInferenceOptions
                {
                    Temperature = 0.1,
                    MaxTokens = 4096
                }
            };

            var response = await _llmClient.CompleteAsync(request, cancellationToken);
            var windowChunks = ParseChunkingResult(response.Message.GetTextContent(), documentId);

            // 调整偏移量
            foreach (var chunk in windowChunks)
            {
                var actualChunk = (Chunk)chunk;
                actualChunk = actualChunk with
                {
                    StartIndex = index + actualChunk.StartIndex,
                    EndIndex = index + actualChunk.EndIndex,
                    Sequence = sequence++
                };
                allChunks.Add(actualChunk);
            }

            // 滑动窗口
            if (windowEnd >= content.Length)
                break;

            index += windowSize - overlap;
        }

        // 去重和合并
        var mergedChunks = MergeOverlappingChunks(allChunks, documentId);

        _logger?.LogDebug("MoE 递归分块完成，共生成 {Count} 个块", mergedChunks.Count);
        return mergedChunks;
    }

    /// <summary>
    /// 构建分块提示词
    /// </summary>
    private string BuildChunkingPrompt(string content, int maxChunkSize, bool isFirstWindow = true, bool isLastWindow = true)
    {
        var customPrompt = _options.CustomPrompt;

        if (!string.IsNullOrWhiteSpace(customPrompt))
        {
            return customPrompt + "\n\n文档内容：\n---\n" + content
                + "\n---\n\n请将上述文档按照语义边界切分为多个块。每个块不超过 "
                + maxChunkSize + " 字符。\n以 JSON 数组格式返回，每个元素包含 text（块内容）、startIndex（起始位置）、endIndex（结束位置）：\n"
                + "[\n  {\"text\": \"...\", \"startIndex\": 0, \"endIndex\": 100},\n  ...\n]";
        }

        var windowContext = "";
        if (!isFirstWindow) windowContext += "这是文档的中间部分。";
        if (!isLastWindow) windowContext += "内容在窗口边界处可能被截断，请尽量在语义边界处分块。";

        return "请将以下文档按照语义边界切分为多个块。\n\n"
            + "要求：\n"
            + "1. 每个块不超过 " + maxChunkSize + " 字符\n"
            + "2. 在主题切换、逻辑段落边界处切分\n"
            + "3. 不要在句子中间切分\n"
            + "4. 每个块应围绕一个主题或概念，保持语义完整性\n"
            + "5. " + windowContext + "\n\n"
            + "文档内容：\n---\n" + content + "\n---\n\n"
            + "请以 JSON 数组格式返回分块结果，每个元素包含：\n"
            + "- text: 块的文本内容\n"
            + "- startIndex: 块在文档中的起始字符位置\n"
            + "- endIndex: 块在文档中的结束字符位置\n\n"
            + "示例输出：\n"
            + "[\n"
            + "  {\"text\": \"第一块内容...\", \"startIndex\": 0, \"endIndex\": 500},\n"
            + "  {\"text\": \"第二块内容...\", \"startIndex\": 500, \"endIndex\": 1000}\n"
            + "]";
    }

    /// <summary>
    /// 解析 LLM 返回的分块结果
    /// </summary>
    private IReadOnlyList<IChunk> ParseChunkingResult(string responseText, Guid documentId)
    {
        var chunks = new List<IChunk>();

        try
        {
            // 尝试提取 JSON 数组
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                responseText,
                @"\[\s*\{.*?\}\s*\]",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            string jsonText;
            if (jsonMatch.Success)
            {
                jsonText = jsonMatch.Value;
            }
            else
            {
                // 尝试提取代码块中的 JSON
                var codeBlockMatch = System.Text.RegularExpressions.Regex.Match(
                    responseText,
                    @"```(?:json)?\s*([\s\S]*?)```",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                if (codeBlockMatch.Success)
                {
                    jsonText = codeBlockMatch.Groups[1].Value.Trim();
                }
                else
                {
                    jsonText = responseText.Trim();
                }
            }

            var chunkResults = JsonSerializer.Deserialize<List<ChunkResult>>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });

            if (chunkResults == null || chunkResults.Count == 0)
            {
                _logger?.LogWarning("MoE 分块结果为空，回退到整文档作为单一块");
                return new List<IChunk>
                {
                    new Chunk
                    {
                        DocumentId = documentId,
                        Text = responseText.Trim(),
                        StartIndex = 0,
                        EndIndex = responseText.Length,
                        Sequence = 0,
                        Metadata = new Dictionary<string, object?> { ["strategy"] = Name, ["fallback"] = true }
                    }
                };
            }

            for (int i = 0; i < chunkResults.Count; i++)
            {
                var result = chunkResults[i];
                if (string.IsNullOrWhiteSpace(result.Text))
                    continue;

                chunks.Add(new Chunk
                {
                    DocumentId = documentId,
                    Text = result.Text.Trim(),
                    StartIndex = result.StartIndex,
                    EndIndex = result.EndIndex,
                    Sequence = i,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["strategy"] = Name,
                        ["length"] = result.Text.Length
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MoE 分块结果解析失败，回退到整文档作为单一块");
            chunks.Add(new Chunk
            {
                DocumentId = documentId,
                Text = responseText.Trim(),
                StartIndex = 0,
                EndIndex = responseText.Length,
                Sequence = 0,
                Metadata = new Dictionary<string, object?> { ["strategy"] = Name, ["fallback"] = true, ["error"] = ex.Message }
            });
        }

        return chunks;
    }

    /// <summary>
    /// 合并重叠的块（处理窗口边界处的重复）
    /// </summary>
    private IReadOnlyList<IChunk> MergeOverlappingChunks(List<IChunk> chunks, Guid documentId)
    {
        if (chunks.Count <= 1)
            return chunks;

        var merged = new List<IChunk>();
        var sorted = chunks.OrderBy(c => c.StartIndex).ToList();

        var current = sorted[0];
        for (int i = 1; i < sorted.Count; i++)
        {
            var next = sorted[i];

            // 检查是否高度重叠（超过 80% 重叠则认为是重复）
            int overlap = Math.Max(0, current.EndIndex - next.StartIndex);
            int currentLength = current.EndIndex - current.StartIndex;
            int nextLength = next.EndIndex - next.StartIndex;

            if (overlap > currentLength * 0.8 || overlap > nextLength * 0.8)
            {
                // 保留较长的一个
                if (nextLength > currentLength)
                {
                    current = next;
                }
                continue;
            }

            // 部分重叠：截断当前块
            if (overlap > 0)
            {
                var trimmedText = current.Text;
                if (current.Text.Length > overlap)
                {
                    trimmedText = current.Text.Substring(0, current.Text.Length - overlap);
                }

                merged.Add(new Chunk
                {
                    Id = current.Id,
                    DocumentId = current.DocumentId,
                    Text = trimmedText.Trim(),
                    StartIndex = current.StartIndex,
                    EndIndex = next.StartIndex,
                    Sequence = current.Sequence,
                    Metadata = new Dictionary<string, object?>(current.Metadata)
                });
                current = next;
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);

        // 重新分配序号
        for (int i = 0; i < merged.Count; i++)
        {
            var c = (Chunk)merged[i];
            merged[i] = c with { Sequence = i };
        }

        return merged;
    }

    /// <summary>
    /// LLM 分块结果内部模型
    /// </summary>
    private sealed class ChunkResult
    {
        public string Text { get; set; } = string.Empty;
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // MoE 不负责释放 ILlmClient，因为客户端由调用方创建和拥有
        await Task.CompletedTask;
    }
}
