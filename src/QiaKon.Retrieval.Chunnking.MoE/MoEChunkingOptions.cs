using QiaKon.Llm;

namespace QiaKon.Retrieval.Chunnking.MoE;

/// <summary>
/// MoE（Mixture of Experts）智能分块配置选项
/// MoE 分块与传统分块的核心区别：
/// 1. 使用小体积大模型进行语义理解分块，而非简单的字符/段落切割
/// 2. 支持全模态输入（图片、PDF 等），模型可直接理解原始格式
/// 3. 可控制是否跳过文档预处理（当模型本身具备多模态能力时）
///
/// 配置驱动设计：
/// - 通过 <see cref="ProviderConfig"/> 传入完整的 LLM Provider 配置，MoE 会根据配置自动创建 Provider 实例
/// - 如果未配置 ProviderConfig，则复用 DI 容器中已注册的默认 <see cref="ILLMProvider"/>
/// - 模型名称通过 <see cref="ModelName"/> 指定，会覆盖 ProviderConfig 中的 DefaultModel
/// </summary>
public sealed class MoEChunkingOptions
{
    /// <summary>
    /// LLM Provider 配置（可选）
    /// 如果提供，MoE 会根据此配置独立创建一个 Provider 实例用于分块
    /// 如果不提供，MoE 会复用 DI 容器中已注册的默认 ILLMProvider
    /// </summary>
    public LLMProviderConfig? ProviderConfig { get; set; }

    /// <summary>
    /// 分块时使用的模型名称（可选）
    /// 如果指定，会覆盖 ProviderConfig 中的 DefaultModel
    /// 如果未指定且 ProviderConfig 也未设置，则使用 Provider 的默认模型
    /// </summary>
    public string? ModelName { get; set; }

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
    /// 分块温度参数（0-2，默认 0.1，低温度使分块更稳定）
    /// </summary>
    public double Temperature { get; set; } = 0.1;

    /// <summary>
    /// 最大输出 Token 数（默认 4096）
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

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

    /// <summary>
    /// 解析最终使用的模型名称
    /// 优先级：ModelName > ProviderConfig.DefaultModel > 兜底默认值
    /// </summary>
    internal string ResolveModelName()
    {
        return ModelName
            ?? ProviderConfig?.DefaultModel
            ?? "gpt-4o-mini";
    }
}
