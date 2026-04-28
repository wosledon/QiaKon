using Microsoft.Extensions.DependencyInjection;
using QiaKon.Llm;

namespace QiaKon.Llm.Tokenization;

/// <summary>
/// Tokenizer DI 注册扩展
/// </summary>
public static class TokenizerServiceCollectionExtensions
{
    /// <summary>
    /// 注册估算 Token 计数器（适用于大多数场景）
    /// </summary>
    public static IServiceCollection AddLlmTokenizer(this IServiceCollection services, string name = "default")
    {
        services.AddSingleton<ITokenizer>(new EstimationTokenizer(name));
        return services;
    }

    /// <summary>
    /// 注册指定的 Token 计数器
    /// </summary>
    public static IServiceCollection AddLlmTokenizer<T>(this IServiceCollection services) where T : class, ITokenizer
    {
        services.AddSingleton<ITokenizer, T>();
        return services;
    }

    /// <summary>
    /// 注册多个 Token 计数器（按名称区分）
    /// </summary>
    public static IServiceCollection AddLlmTokenizers(this IServiceCollection services)
    {
        services.AddSingleton<ITokenizer>(new EstimationTokenizer("GPT-4", 0.25, 0.25));
        services.AddSingleton<ITokenizer>(new EstimationTokenizer("GPT-3.5", 0.25, 0.25));
        services.AddSingleton<ITokenizer>(new AnthropicTokenizer());
        services.AddSingleton<ITokenizer>(new EstimationTokenizer("Qwen", 0.5, 0.25));
        return services;
    }
}

/// <summary>
/// Token 预算计算器 - 用于估算 prompt 是否能在 context window 内
/// </summary>
public sealed class TokenBudget
{
    private readonly int _maxTokens;
    private readonly ITokenizer _tokenizer;

    public TokenBudget(int maxTokens, ITokenizer tokenizer)
    {
        _maxTokens = maxTokens;
        _tokenizer = tokenizer;
    }

    /// <summary>
    /// 计算可用 tokens 数量（保留给输出的 tokens）
    /// </summary>
    public int AvailableForInput(int reservedForOutput = 0)
    {
        return Math.Max(0, _maxTokens - reservedForOutput);
    }

    /// <summary>
    /// 检查输入是否能在 context window 内
    /// </summary>
    public bool CanFitInput(string text, int reservedForOutput = 0)
    {
        var inputTokens = _tokenizer.CountTokens(text);
        return inputTokens + reservedForOutput <= _maxTokens;
    }

    /// <summary>
    /// 检查消息列表是否能在 context window 内
    /// </summary>
    public bool CanFitMessages(IEnumerable<ChatMessage> messages, int reservedForOutput = 0)
    {
        var messageTokens = _tokenizer.CountMessages(messages);
        return messageTokens + reservedForOutput <= _maxTokens;
    }

    /// <summary>
    /// 计算最大可用输出 tokens
    /// </summary>
    public int MaxOutputTokens(int inputTokens)
    {
        return Math.Max(0, _maxTokens - inputTokens);
    }

    /// <summary>
    /// 计算能容纳的最大输入（返回输入 tokens 数和可用输出 tokens 数）
    /// </summary>
    public (int inputTokens, int availableOutput) CalculateBudget(int inputTokens)
    {
        var availableOutput = Math.Max(0, _maxTokens - inputTokens);
        return (inputTokens, availableOutput);
    }
}

/// <summary>
/// Token 预算扩展
/// </summary>
public static class TokenBudgetExtensions
{
    /// <summary>
    /// 为指定模型创建 token 预算计算器
    /// </summary>
    public static TokenBudget CreateBudget(string model, ITokenizer tokenizer)
    {
        var maxTokens = model.ToLowerInvariant() switch
        {
            var m when m.Contains("gpt-4-32k") => 32768,
            var m when m.Contains("gpt-4") => 128000,
            var m when m.Contains("gpt-3.5-turbo-16k") => 16384,
            var m when m.Contains("gpt-3.5-turbo") => 16385,
            var m when m.Contains("claude-3-opus") => 200000,
            var m when m.Contains("claude-3-sonnet") => 200000,
            var m when m.Contains("claude-3.5-sonnet") => 200000,
            var m when m.Contains("claude-3.5-haiku") => 200000,
            var m when m.Contains("claude-3-haiku") => 200000,
            var m when m.Contains("qwen-turbo") => 128000,
            var m when m.Contains("qwen-plus") => 131072,
            var m when m.Contains("qwen") => 8192,
            _ => 4096
        };

        return new TokenBudget(maxTokens, tokenizer);
    }
}
