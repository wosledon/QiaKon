namespace QiaKon.Retrieval.Embedding;

/// <summary>
/// 嵌入选项
/// </summary>
public sealed class EmbeddingOptions
{
    /// <summary>
    /// 嵌入向量维度（必须与模型一致）
    /// </summary>
    public int Dimensions { get; set; } = 1024;

    /// <summary>
    /// 模型文件夹路径（包含 onnx 模型和 tokenizer 文件）
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>
    /// 模型名称
    /// </summary>
    public string ModelName { get; set; } = "Qwen3-Embedding";

    /// <summary>
    /// 输入节点名称
    /// </summary>
    public string InputName { get; set; } = "input_ids";

    /// <summary>
    /// 输出节点名称
    /// </summary>
    public string OutputName { get; set; } = "last_hidden_state";

    /// <summary>
    /// 最大序列长度
    /// </summary>
    public int MaxSequenceLength { get; set; } = 8192;
}
