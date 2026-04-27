namespace QiaKon.Workflow.Configuration;

/// <summary>
/// 步骤配置
/// </summary>
public class StepConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
    public string? Condition { get; set; }
    public int? TimeoutSeconds { get; set; }
    public int RetryCount { get; set; }
}

/// <summary>
/// 阶段配置
/// </summary>
public class StageConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Mode { get; set; } = "Sequential";
    public List<StepConfiguration> Steps { get; set; } = new();
}

/// <summary>
/// 流水线配置
/// </summary>
public class PipelineConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<StageConfiguration> Stages { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// 工作流选项
/// </summary>
public class WorkflowOptions
{
    /// <summary>
    /// 是否启用事件发布
    /// </summary>
    public bool EnableEvents { get; set; } = true;

    /// <summary>
    /// 默认步骤超时时间（秒）
    /// </summary>
    public int DefaultStepTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 默认重试次数
    /// </summary>
    public int DefaultRetryCount { get; set; }

    /// <summary>
    /// 是否在步骤失败时继续执行后续步骤
    /// </summary>
    public bool ContinueOnStepFailure { get; set; }

    /// <summary>
    /// 配置文件路径
    /// </summary>
    public string? ConfigurationPath { get; set; }
}
