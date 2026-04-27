using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using QiaKon.Workflow.Abstractions;

namespace QiaKon.Workflow.Events;

/// <summary>
/// 工作流事件总线实现（内存内事件发布/订阅）
/// </summary>
public sealed class WorkflowEventBus : IWorkflowEventBus
{
    private readonly ConcurrentDictionary<Type, List<Func<IWorkflowEvent, Task>>> _handlers = new();
    private readonly ConcurrentDictionary<Type, List<object>> _handlerTokens = new();
    private readonly ILogger<WorkflowEventBus>? _logger;

    public WorkflowEventBus(ILogger<WorkflowEventBus>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IWorkflowEvent
    {
        var eventType = typeof(TEvent);

        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            foreach (var handler in handlers.ToList())
            {
                try
                {
                    await handler(@event);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error handling event {EventType}", eventType.Name);
                }
            }
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IWorkflowEvent
    {
        var eventType = typeof(TEvent);

        _handlers.AddOrUpdate(
            eventType,
            _ => new List<Func<IWorkflowEvent, Task>> { WrapHandler(handler) },
            (_, handlers) =>
            {
                handlers.Add(WrapHandler(handler));
                return handlers;
            });

        var token = new EventSubscriptionToken<TEvent>(this, handler);
        return token;
    }

    /// <inheritdoc />
    public bool HasSubscribers<TEvent>() where TEvent : IWorkflowEvent
    {
        return _handlers.TryGetValue(typeof(TEvent), out var handlers) && handlers.Count > 0;
    }

    private Func<IWorkflowEvent, Task> WrapHandler<TEvent>(Func<TEvent, Task> handler) where TEvent : IWorkflowEvent
    {
        return @event => handler((TEvent)@event);
    }

    private void Unsubscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IWorkflowEvent
    {
        var eventType = typeof(TEvent);

        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            Func<IWorkflowEvent, Task>? wrapper = null;
            foreach (var h in handlers)
            {
                if (h.Target is EventSubscriptionToken<TEvent> token && ReferenceEquals(token.Handler, handler))
                {
                    wrapper = h;
                    break;
                }
            }

            if (wrapper != null)
            {
                handlers.Remove(wrapper);
            }
        }
    }

    private sealed class EventSubscriptionToken<TEvent> : IDisposable where TEvent : IWorkflowEvent
    {
        private readonly WorkflowEventBus _bus;
        private readonly Func<TEvent, Task> _handler;

        public Func<TEvent, Task> Handler => _handler;

        public EventSubscriptionToken(WorkflowEventBus bus, Func<TEvent, Task> handler)
        {
            _bus = bus;
            _handler = handler;
        }

        public void Dispose()
        {
            _bus.Unsubscribe(_handler);
        }
    }
}
