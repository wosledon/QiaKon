namespace QiaKon.Llm;

/// <summary>
/// 重试策略
/// </summary>
public static class LlmRetryStrategy
{
    /// <summary>
    /// 创建指数退避重试策略
    /// </summary>
    public static Func<int, TimeSpan> ExponentialBackoff(
        int baseDelayMs = 500,
        int maxDelayMs = 30000,
        double multiplier = 2.0)
    {
        return retryCount =>
        {
            var delay = baseDelayMs * Math.Pow(multiplier, retryCount - 1);
            var jitter = Random.Shared.Next(0, (int)(delay * 0.1));
            return TimeSpan.FromMilliseconds(Math.Min(delay + jitter, maxDelayMs));
        };
    }

    /// <summary>
    /// 创建线性退避重试策略
    /// </summary>
    public static Func<int, TimeSpan> LinearBackoff(
        int delayMs = 1000,
        int maxDelayMs = 10000)
    {
        return retryCount =>
        {
            var delay = Math.Min(delayMs * retryCount, maxDelayMs);
            return TimeSpan.FromMilliseconds(delay);
        };
    }
}

/// <summary>
/// 可恢复的LLM异常
/// </summary>
public class LlmRetryableException : Exception
{
    public int RetryCount { get; }
    public TimeSpan? RetryAfter { get; }

    public LlmRetryableException(string message, int retryCount, TimeSpan? retryAfter = null)
        : base(message)
    {
        RetryCount = retryCount;
        RetryAfter = retryAfter;
    }

    public LlmRetryableException(string message, Exception innerException, int retryCount, TimeSpan? retryAfter = null)
        : base(message, innerException)
    {
        RetryCount = retryCount;
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// LLM异常（不可重试）
/// </summary>
public class LlmException : Exception
{
    public int? StatusCode { get; }

    public LlmException(string message) : base(message) { }

    public LlmException(string message, int? statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public LlmException(string message, Exception innerException) : base(message, innerException) { }
}
