namespace QiaKon.Queue;

/// <summary>
/// 队列消费者接口
/// </summary>
public interface IQueueConsumer : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 消费者名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 订阅的主题列表
    /// </summary>
    IReadOnlyList<string> Topics { get; }

    /// <summary>
    /// 消费者组ID
    /// </summary>
    string GroupId { get; }

    /// <summary>
    /// 初始化消费者
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 消费消息流
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步消息流</returns>
    IAsyncEnumerable<IQueueMessage> ConsumeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 提交消费位移
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 暂停消费
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复消费
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ResumeAsync(CancellationToken cancellationToken = default);
}
