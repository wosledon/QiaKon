using System.Collections.Concurrent;

namespace QiaKon.Workflow.Abstractions;

/// <summary>
/// 工作流上下文，贯穿整个流水线执行过程
/// </summary>
public class WorkflowContext
{
    private readonly ConcurrentDictionary<string, object> _items = new();
    private readonly ConcurrentDictionary<string, object?> _output = new();

    /// <summary>
    /// 流水线名称
    /// </summary>
    public string PipelineName { get; set; } = string.Empty;

    /// <summary>
    /// 当前执行阶段名称
    /// </summary>
    public string? CurrentStageName { get; set; }

    /// <summary>
    /// 当前执行步骤名称
    /// </summary>
    public string? CurrentStepName { get; set; }

    /// <summary>
    /// 关联标识符，用于追踪整个流水线的执行
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 执行标记，用于取消或中断
    /// </summary>
    public CancellationTokenSource? CancellationTokenSource { get; init; } = new();

    /// <summary>
    /// 启动时间
    /// </summary>
    public DateTime StartTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 获取数据项
    /// </summary>
    public T? GetItem<T>(string key) => _items.TryGetValue(key, out var value) ? (T?)value : default;

    /// <summary>
    /// 设置数据项
    /// </summary>
    public void SetItem<T>(string key, T value) => _items[key] = value!;

    /// <summary>
    /// 尝试获取数据项
    /// </summary>
    public bool TryGetItem<T>(string key, out T? value)
    {
        if (_items.TryGetValue(key, out var v))
        {
            value = (T?)v;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// 获取输出值
    /// </summary>
    public T? GetOutput<T>(string key) => _output.TryGetValue(key, out var value) ? (T?)value : default;

    /// <summary>
    /// 设置输出值
    /// </summary>
    public void SetOutput<T>(string key, T value) => _output[key] = value!;

    /// <summary>
    /// 获取所有输出
    /// </summary>
    public IReadOnlyDictionary<string, object?> GetOutputs() => _output;

    /// <summary>
    /// 请求取消执行
    /// </summary>
    public void Cancel() => CancellationTokenSource?.Cancel();

    /// <summary>
    /// 检查是否已请求取消
    /// </summary>
    public bool IsCancellationRequested => CancellationTokenSource?.IsCancellationRequested ?? false;
}
