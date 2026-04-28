using System.Text.RegularExpressions;
using QiaKon.Llm;

namespace QiaKon.Llm.Tokenization;

/// <summary>
/// 基于 TikToken 的 GPT-4/GPT-3.5 Tokenizer
/// 注意: 需要 Microsoft.Tiktoken.Core 包
/// </summary>
public sealed class TikTokenTokenizer : ITokenizer
{
    private readonly string _modelName;

    public string Name => $"TikToken({_modelName})";

    public TikTokenTokenizer(string modelName = "gpt-4")
    {
        _modelName = modelName;
        // 简化实现，实际应使用 Microsoft.Tiktoken.Core
        // 这里提供一个基于正则的粗略估算
    }

    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // 粗略估算: 中文约 2 chars/token, 英文约 4 chars/token
        var chineseChars = Regex.Matches(text, @"[\u4e00-\u9fff]").Count;
        var otherChars = text.Length - chineseChars;

        return (int)Math.Ceiling(chineseChars / 2.0) + (int)Math.Ceiling(otherChars / 4.0);
    }

    public int CountMessages(IEnumerable<ChatMessage> messages)
    {
        var count = 0;
        foreach (var msg in messages)
        {
            // 每条消息有 role/token overhead
            count += 4; // base overhead per message

            // 计算角色
            count += CountTokens(msg.Role.ToString().ToLower());

            // 计算内容
            foreach (var block in msg.ContentBlocks)
            {
                if (block is TextContentBlock textBlock)
                {
                    count += CountTokens(textBlock.Text);
                }
                else if (block is ToolCallContentBlock toolBlock)
                {
                    count += CountTokens(toolBlock.Name);
                    count += CountTokens(toolBlock.ArgumentsJson ?? "");
                }
            }
        }

        // 添加消息集合 overhead
        count += 3;

        return count;
    }
}

/// <summary>
/// Anthropic Claude Token 计数器
/// </summary>
public sealed class AnthropicTokenizer : ITokenizer
{
    public string Name => "Anthropic";

    // Claude 模型 context window 和 token 计算规则
    private static readonly Dictionary<string, int> ModelContextWindows = new()
    {
        ["claude-3-opus"] = 200000,
        ["claude-3-sonnet"] = 200000,
        ["claude-3-5-sonnet"] = 200000,
        ["claude-3-5-haiku"] = 200000,
    };

    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Claude 使用 SentencePiece，估算规则类似 GPT
        var chineseChars = Regex.Matches(text, @"[\u4e00-\u9fff]").Count;
        var otherChars = text.Length - chineseChars;

        return (int)Math.Ceiling(chineseChars / 2.0) + (int)Math.Ceiling(otherChars / 4.0);
    }

    public int CountMessages(IEnumerable<ChatMessage> messages)
    {
        var count = 0;
        foreach (var msg in messages)
        {
            // Anthropic message 格式 overhead
            count += CountTokens(msg.Role.ToString().ToLower());
            count += 5; // {\n  "role": ... overhead

            foreach (var block in msg.ContentBlocks)
            {
                if (block is TextContentBlock textBlock)
                {
                    count += CountTokens(textBlock.Text);
                    count += 13; // "content": "\n\n" overhead
                }
            }

            count += 1; // \n}
        }

        count += 5; // messages 数组 overhead: [\n\n]

        return count;
    }
}

/// <summary>
/// Qwen Token 计数器
/// </summary>
public sealed class QwenTokenizer : ITokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly int _unkTokenId;

    public string Name => "Qwen";

    public QwenTokenizer(Dictionary<string, int>? vocab = null)
    {
        _vocab = vocab ?? new Dictionary<string, int>();
        _unkTokenId = 151643;
    }

    public static QwenTokenizer? TryLoad(string modelDir)
    {
        try
        {
            var vocabPath = Path.Combine(modelDir, "vocab.json");
            if (!File.Exists(vocabPath))
                return null;

            var vocabJson = File.ReadAllText(vocabPath);
            Dictionary<string, int> vocab;

            try
            {
                vocab = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson) ?? new();
            }
            catch
            {
                var reversed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(vocabJson);
                vocab = reversed?.ToDictionary(kv => kv.Value, kv => int.TryParse(kv.Key, out var id) ? id : 0) ?? new();
            }

            return vocab.Count == 0 ? null : new QwenTokenizer(vocab);
        }
        catch
        {
            return null;
        }
    }

    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text) || _vocab.Count == 0)
            return 0;

        // 简单分词: 尝试匹配 vocab 中的 token
        var tokens = new List<int>();
        var i = 0;

        while (i < text.Length)
        {
            var matched = false;

            // 尝试最长匹配
            for (var len = Math.Min(8, text.Length - i); len > 0; len--)
            {
                var substr = text.Substring(i, len);
                if (_vocab.TryGetValue(substr, out var tokenId))
                {
                    tokens.Add(tokenId);
                    i += len;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                // 未匹配的字符使用 unk token
                tokens.Add(_unkTokenId);
                i++;
            }
        }

        return tokens.Count;
    }

    public int CountMessages(IEnumerable<ChatMessage> messages)
    {
        var count = 0;

        foreach (var msg in messages)
        {
            // 添加 role 和 content
            count += CountTokens(msg.Role.ToString().ToLower());
            count += CountTokens(msg.GetTextContent());
        }

        // 添加特殊 token
        count += 2; // <|beginoftext|> + <|endoftext|>

        return count;
    }
}

/// <summary>
/// 估算 Token 计数器（不加载词汇表）
/// </summary>
public sealed class EstimationTokenizer : ITokenizer
{
    public string Name { get; }

    private readonly double _chineseTokensPerChar;
    private readonly double _englishTokensPerChar;

    public EstimationTokenizer(string name, double chineseTokensPerChar = 0.5, double englishTokensPerChar = 0.25)
    {
        Name = name;
        _chineseTokensPerChar = chineseTokensPerChar;
        _englishTokensPerChar = englishTokensPerChar;
    }

    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var chineseChars = Regex.Matches(text, @"[\u4e00-\u9fff]").Count;
        var otherChars = text.Length - chineseChars;

        return (int)Math.Ceiling(chineseChars * _chineseTokensPerChar + otherChars * _englishTokensPerChar);
    }

    public int CountMessages(IEnumerable<ChatMessage> messages)
    {
        var count = 0;
        foreach (var msg in messages)
        {
            count += 4; // base overhead
            count += CountTokens(msg.Role.ToString().ToLower());

            foreach (var block in msg.ContentBlocks)
            {
                if (block is TextContentBlock textBlock)
                {
                    count += CountTokens(textBlock.Text);
                }
            }
        }
        return count + 3;
    }
}
