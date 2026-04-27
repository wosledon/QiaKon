using Microsoft.Extensions.DependencyInjection;
using QiaKon.Workflow.Abstractions;
using QiaKon.Workflow.Configuration;
using QiaKon.Workflow.Core;
using QiaKon.Workflow.Events;

namespace QiaKon.Workflow.Extensions;

/// <summary>
/// 工作流服务集合扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加工作流核心服务
    /// </summary>
    public static IServiceCollection AddWorkflowCore(this IServiceCollection services)
    {
        services.AddSingleton<IWorkflowEventBus, WorkflowEventBus>();
        services.AddSingleton<IWorkflowExecutor, WorkflowExecutor>();
        services.AddSingleton<IPipelineRegistry, PipelineRegistry>();

        return services;
    }

    /// <summary>
    /// 添加工作流核心服务（带配置）
    /// </summary>
    public static IServiceCollection AddWorkflowCore(this IServiceCollection services, WorkflowOptions options)
    {
        services.AddSingleton(options);
        return AddWorkflowCore(services);
    }

    /// <summary>
    /// 添加工作流服务（带事件总线）
    /// </summary>
    public static IServiceCollection AddWorkflowCore(this IServiceCollection services, IWorkflowEventBus eventBus)
    {
        services.AddSingleton(eventBus);
        services.AddSingleton<IWorkflowExecutor>(sp => new WorkflowExecutor(eventBus));
        services.AddSingleton<IPipelineRegistry, PipelineRegistry>();

        return services;
    }

    /// <summary>
    /// 注册流水线
    /// </summary>
    public static IServiceCollection RegisterPipeline(this IServiceCollection services, IPipeline pipeline)
    {
        services.AddSingleton(pipeline);
        return services;
    }

    /// <summary>
    /// 注册流水线注册表中的所有流水线
    /// </summary>
    public static IServiceCollection RegisterPipelines(this IServiceCollection services, IEnumerable<IPipeline> pipelines)
    {
        foreach (var pipeline in pipelines)
        {
            services.AddSingleton(pipeline);
        }
        return services;
    }

    /// <summary>
    /// 获取工作流执行器
    /// </summary>
    public static IWorkflowExecutor GetWorkflowExecutor(this IServiceProvider services)
    {
        return services.GetRequiredService<IWorkflowExecutor>();
    }

    /// <summary>
    /// 获取流水线注册表
    /// </summary>
    public static IPipelineRegistry GetPipelineRegistry(this IServiceProvider services)
    {
        return services.GetRequiredService<IPipelineRegistry>();
    }
}
