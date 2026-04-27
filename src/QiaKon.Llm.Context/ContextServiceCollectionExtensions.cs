using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Llm.Context;

/// <summary>
/// 上下文服务注册扩展
/// </summary>
public static class ContextServiceCollectionExtensions
{
    /// <summary>
    /// 注册上下文模板注册表
    /// </summary>
    public static IServiceCollection AddContextTemplates(
        this IServiceCollection services,
        Action<ContextTemplateRegistry> configure)
    {
        services.AddSingleton<ContextTemplateRegistry>();
        services.AddSingleton(sp =>
        {
            var registry = sp.GetRequiredService<ContextTemplateRegistry>();
            configure(registry);
            return registry;
        });

        return services;
    }

    /// <summary>
    /// 注册对话上下文工厂
    /// </summary>
    public static IServiceCollection AddConversationContext(
        this IServiceCollection services,
        int? maxMessages = null,
        int? maxTokens = null)
    {
        services.AddScoped(sp => new ConversationContext(
            maxMessages: maxMessages,
            maxTokens: maxTokens));

        return services;
    }
}
