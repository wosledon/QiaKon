namespace QiaKon.Queue;

/// <summary>
/// 队列生产者接口
/// </summary>
public interface IQueueProducer : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 生产者名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 初始化生产者
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送单条消息
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="message">消息体</param>
    /// <param name="key">消息键（可选，用于分区）</param>
    /// <param name="partition">目标分区（-1 表示自动分配）</param>
    /// <param name="headers">消息头</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ProduceAsync(
        string topic,
        ReadOnlyMemory<byte> message,
        string? key = null,
        int partition = -1,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量发送消息
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="messages">消息体集合</param>
    /// <param name="partition">目标分区（-1 表示自动分配）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ProduceManyAsync(
        string topic,
        IEnumerable<ReadOnlyMemory<byte>> messages,
        int partition = -1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 刷新缓冲区（确保消息已发送）
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
