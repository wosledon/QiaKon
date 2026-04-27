namespace QiaKon.Retrieval.Chunnking.MoE;

/// <summary>
/// MoE（Mixture of Experts）智能分块配置选项
/// MoE 分块与传统分块的核心区别：
/// 1. 使用小体积大模型进行语义理解分块，而非简单的字符/段落切割
/// 2. 支持全模态输入（图片、PDF 等），模型可直接理解原始格式
/// 3. 可控制是否跳过文档预处理（当模型本身具备多模态能力时）
///
/// LLM 客户端由调用方从数据库读取配置后创建，直接传入 MoE 使用
/// </summary>
public sealed class MoEChunkingOptions
{
    /// <summary>
    /// 每个块的最大字符数（默认 2000）
    /// 模型会尽量在此限制内找到语义边界
    /// </summary>
    public int MaxChunkSize { get; set; } = 2000;

    /// <summary>
    /// 块之间的最小重叠字符数（默认 100）
    /// </summary>
    public int MinOverlapSize { get; set; } = 100;

    /// <summary>
    /// 是否跳过文档预处理阶段
    /// 当 MoE 模型本身是全模态（支持直接理解 PDF/图片等）时，设为 true
    /// 当 MoE 模型仅支持文本输入时，设为 false（先由 MarkItDown 转为 Markdown）
    /// </summary>
    public bool SkipDocumentProcessing { get; set; } = false;

    /// <summary>
    /// 自定义分块提示词（可选，覆盖默认提示词）
    /// </summary>
    public string? CustomPrompt { get; set; }

    /// <summary>
    /// 是否保留章节标题作为块元数据
    /// </summary>
    public bool PreserveHeaders { get; set; } = true;

    /// <summary>
    /// 并发处理的最大块数（默认 5）
    /// </summary>
    public int MaxConcurrency { get; set; } = 5;
}
