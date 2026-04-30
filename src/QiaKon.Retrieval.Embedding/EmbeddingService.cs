using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace QiaKon.Retrieval.Embedding;

/// <summary>
/// 基于 ONNX Runtime 的本地嵌入服务
/// 加载本地 ONNX 模型进行推理
/// </summary>
public sealed class LocalEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly InferenceSession? _session;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<LocalEmbeddingService>? _logger;
    private readonly ConcurrentDictionary<string, ReadOnlyMemory<float>> _cache = new();
    private readonly QwenTokenizer? _tokenizer;
    private readonly bool _useFallbackEmbeddings;

    public int Dimensions => _options.Dimensions;
    public string ModelName => _options.ModelName;

    public LocalEmbeddingService(EmbeddingOptions options, ILogger<LocalEmbeddingService>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        var modelPath = _options.ModelPath;

        // 如果是文件夹，查找 onnx 文件
        if (Directory.Exists(modelPath))
        {
            var onnxFiles = Directory.GetFiles(modelPath, "*.onnx");
            if (onnxFiles.Length == 0)
            {
                _useFallbackEmbeddings = true;
                _logger?.LogWarning("在文件夹 {ModelPath} 中未找到 ONNX 模型文件，将自动退化为本地哈希嵌入。", modelPath);
                return;
            }
            modelPath = onnxFiles[0];
        }
        else if (!File.Exists(modelPath))
        {
            _useFallbackEmbeddings = true;
            _logger?.LogWarning("未找到 ONNX 模型 {ModelPath}，将自动退化为本地哈希嵌入。", modelPath);
            return;
        }

        try
        {
            var sessionOptions = new SessionOptions();
            sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

            _session = new InferenceSession(modelPath, sessionOptions);

            // 加载 tokenizer 文件
            var modelDir = Path.GetDirectoryName(modelPath) ?? "";
            _tokenizer = QwenTokenizer.TryLoad(modelDir);

            _logger?.LogInformation("LocalEmbeddingService 初始化完成，模型: {ModelPath}, 维度: {Dimensions}",
                modelPath, _options.Dimensions);
        }
        catch (Exception ex)
        {
            _useFallbackEmbeddings = true;
            _logger?.LogWarning(ex, "本地 ONNX 嵌入模型初始化失败，将自动退化为本地哈希嵌入。ModelPath={ModelPath}", modelPath);
        }
    }

    /// <inheritdoc />
    public Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(new ReadOnlyMemory<float>(new float[_options.Dimensions]));

        if (_cache.TryGetValue(text, out var cached))
            return Task.FromResult(cached);

        return Task.Run(() =>
        {
            var embedding = ComputeEmbedding(text);
            _cache.TryAdd(text, embedding);
            return embedding;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (textList.Count == 0)
            return Task.FromResult<IReadOnlyList<ReadOnlyMemory<float>>>(Array.Empty<ReadOnlyMemory<float>>());

        return Task.Run(() => EmbedBatchInternal(textList), cancellationToken);
    }

    private ReadOnlyMemory<float> ComputeEmbedding(string text)
    {
        if (_useFallbackEmbeddings || _session is null)
        {
            return new ReadOnlyMemory<float>(ComputeFallbackEmbedding(text));
        }

        var (inputIds, attentionMask) = _tokenizer?.Tokenize(text, _options.MaxSequenceLength)
            ?? SimpleTokenize(text, _options.MaxSequenceLength);

        var seqLen = inputIds.Length;

        // 创建 2D tensor [batch=1, seq_len]
        var inputIdsTensor = new DenseTensor<long>(new[] { 1, seqLen });
        var attentionMaskTensor = new DenseTensor<long>(new[] { 1, seqLen });

        for (int i = 0; i < seqLen; i++)
        {
            inputIdsTensor[0, i] = inputIds[i];
            attentionMaskTensor[0, i] = attentionMask[i];
        }

        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor<long>(_options.InputName, inputIdsTensor),
            NamedOnnxValue.CreateFromTensor<long>("attention_mask", attentionMaskTensor)
        };

        using var outputs = _session.Run(inputs);
        var output = outputs.FirstOrDefault(o => o.Name == _options.OutputName)
                     ?? outputs.FirstOrDefault();

        var embedding = output?.AsEnumerable<float>().ToArray() ?? new float[_options.Dimensions];

        // Mean pooling + L2 normalize
        var pooled = MeanPooling(embedding, seqLen);
        Normalize(pooled);

        return new ReadOnlyMemory<float>(pooled);
    }

    private IReadOnlyList<ReadOnlyMemory<float>> EmbedBatchInternal(List<string> texts)
    {
        _logger?.LogDebug("开始批量嵌入，文本数量: {Count}", texts.Count);

        var results = new List<ReadOnlyMemory<float>>();
        foreach (var text in texts)
        {
            results.Add(ComputeEmbedding(text));
        }

        _logger?.LogDebug("批量嵌入完成，有效结果: {Count}", results.Count);
        return results;
    }

    private static (long[] InputIds, long[] AttentionMask) SimpleTokenize(string text, int maxLength)
    {
        // 简单的分词 - 适用于英文和中文
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokens = new List<long>(maxLength);

        // [CLS] = 151643
        tokens.Add(151643);

        foreach (var word in words.Take(maxLength - 2))
        {
            // 简化：使用字符哈希作为 token ID
            foreach (var c in word)
            {
                if (tokens.Count >= maxLength - 1) break;
                tokens.Add(Math.Abs(c.GetHashCode()) % 250000 + 100);
            }
        }

        // [SEP] = 151644
        tokens.Add(151644);

        var inputIds = tokens.ToArray();
        var attentionMask = new long[inputIds.Length];
        Array.Fill(attentionMask, 1L);

        return (inputIds, attentionMask);
    }

    private static float[] MeanPooling(float[] embedding, int tokenCount)
    {
        if (tokenCount <= 0) return embedding;

        var hiddenSize = embedding.Length / tokenCount;
        if (hiddenSize <= 0) hiddenSize = embedding.Length;

        var result = new float[hiddenSize];
        var offset = embedding.Length - hiddenSize * tokenCount;

        for (int i = 0; i < tokenCount; i++)
        {
            for (int j = 0; j < hiddenSize; j++)
            {
                var idx = offset + i * hiddenSize + j;
                if (idx < embedding.Length)
                    result[j] += embedding[idx];
            }
        }

        for (int i = 0; i < result.Length; i++)
            result[i] /= tokenCount;

        return result;
    }

    private static void Normalize(float[] vector)
    {
        var norm = (float)Math.Sqrt(vector.Sum(v => v * v));
        if (norm > 1e-10)
        {
            for (int i = 0; i < vector.Length; i++)
                vector[i] /= norm;
        }
    }

    private float[] ComputeFallbackEmbedding(string text)
    {
        var vector = new float[_options.Dimensions];
        if (string.IsNullOrWhiteSpace(text))
        {
            return vector;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var bucket = Math.Abs(HashCode.Combine(text[i], i)) % vector.Length;
            vector[bucket] += 1f + ((i % 7) * 0.01f);
        }

        Normalize(vector);
        return vector;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}

/// <summary>
/// Qwen 分词器
/// </summary>
internal sealed class QwenTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly int _unkTokenId = 151643;
    private readonly int _clsTokenId = 151643;
    private readonly int _sepTokenId = 151644;

    public QwenTokenizer(Dictionary<string, int> vocab)
    {
        _vocab = vocab;
    }

    public static QwenTokenizer? TryLoad(string modelDir)
    {
        try
        {
            var vocabPath = Path.Combine(modelDir, "vocab.json");
            if (!File.Exists(vocabPath))
                return null;

            var vocabJson = File.ReadAllText(vocabPath);

            // Qwen vocab.json 格式: {"token": id} 或 {id: "token"}
            Dictionary<string, int> vocab;
            try
            {
                // 先尝试直接解析为 {token: id}
                vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson) ?? new();
            }
            catch
            {
                // 如果失败，尝试反向映射
                var reversed = JsonSerializer.Deserialize<Dictionary<string, string>>(vocabJson);
                vocab = reversed?.ToDictionary(kv => kv.Value, kv => int.TryParse(kv.Key, out var id) ? id : 0)
                    ?? new();
            }

            if (vocab.Count == 0)
                return null;

            return new QwenTokenizer(vocab);
        }
        catch
        {
            return null;
        }
    }

    public (long[] InputIds, long[] AttentionMask) Tokenize(string text, int maxLength)
    {
        var tokens = new List<long>(maxLength);

        // [CLS]
        tokens.Add(_clsTokenId);

        // 简单分词 - 按空格和标点分割
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words.Take(maxLength - 2))
        {
            if (_vocab.TryGetValue(word, out var id))
            {
                tokens.Add(id);
            }
            else
            {
                // 尝试逐字符匹配
                foreach (var c in word)
                {
                    if (tokens.Count >= maxLength - 1) break;
                    var charStr = c.ToString();
                    tokens.Add(_vocab.TryGetValue(charStr, out var charId) ? charId : _unkTokenId);
                }
            }
        }

        // [SEP]
        tokens.Add(_sepTokenId);

        var inputIds = tokens.ToArray();
        var attentionMask = new long[inputIds.Length];
        Array.Fill(attentionMask, 1L);

        return (inputIds, attentionMask);
    }
}
