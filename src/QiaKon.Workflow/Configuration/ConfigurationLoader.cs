using System.Text.Json;
using QiaKon.Workflow.Abstractions;
using QiaKon.Workflow.Core;

namespace QiaKon.Workflow.Configuration;

/// <summary>
/// 流水线配置加载器
/// </summary>
public class PipelineConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 从 JSON 文件加载流水线配置
    /// </summary>
    public static PipelineConfiguration? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<PipelineConfiguration>(json, JsonOptions);
    }

    /// <summary>
    /// 从 JSON 字符串加载流水线配置
    /// </summary>
    public static PipelineConfiguration? LoadFromJson(string json)
    {
        return JsonSerializer.Deserialize<PipelineConfiguration>(json, JsonOptions);
    }

    /// <summary>
    /// 将配置转换为流水线实例
    /// </summary>
    /// <remarks>
    /// 注意：此方法需要配合具体的步骤工厂使用，以根据 StepConfiguration.Type 创建对应的步骤实例
    /// </remarks>
    public static IPipeline BuildPipeline(PipelineConfiguration config, IStepFactory stepFactory)
    {
        var pipeline = new Pipeline(config.Name, config.Description);

        foreach (var stageConfig in config.Stages)
        {
            var mode = stageConfig.Mode.Equals("Parallel", StringComparison.OrdinalIgnoreCase)
                ? StepMode.Parallel
                : StepMode.Sequential;

            var stage = new Stage(stageConfig.Name, mode);

            foreach (var stepConfig in stageConfig.Steps)
            {
                var step = stepFactory.CreateStep(stepConfig);
                stage.AddStep(step);
            }

            pipeline.AddStage(stage);
        }

        return pipeline;
    }
}

/// <summary>
/// 步骤工厂接口
/// </summary>
public interface IStepFactory
{
    IStep CreateStep(StepConfiguration config);
}

/// <summary>
/// 默认步骤工厂
/// </summary>
public class DefaultStepFactory : IStepFactory
{
    private readonly Dictionary<string, Func<StepConfiguration, IStep>> _builders = new(StringComparer.OrdinalIgnoreCase);

    public DefaultStepFactory()
    {
        // 注册默认的 Lambda 步骤构建器
        Register("Lambda", config =>
            new LambdaStep(config.Name, (_, _) => Task.FromResult(StepResult.Succeeded())));
    }

    /// <summary>
    /// 注册步骤构建器
    /// </summary>
    public void Register(string type, Func<StepConfiguration, IStep> builder)
    {
        _builders[type] = builder;
    }

    /// <inheritdoc />
    public IStep CreateStep(StepConfiguration config)
    {
        if (_builders.TryGetValue(config.Type, out var builder))
        {
            return builder(config);
        }

        throw new NotSupportedException($"Step type '{config.Type}' is not supported. Register a builder first.");
    }
}
