using Saldo.Application.Interfaces;

namespace Saldo.Application.Notifications;

/// <summary>Thread-safe, bounded history; never persisted to the database.</summary>
public sealed class NotificationService(TimeProvider? clock = null) : INotificationService
{
    private readonly object _gate = new();
    private readonly Queue<Notification> _history = new();
    public IReadOnlyList<Notification> History { get { lock (_gate) return _history.ToArray(); } }
    public event EventHandler? Changed;

    public void Publish(string code, string message, NotificationSeverity severity = NotificationSeverity.Information,
        string? details = null)
    {
        lock (_gate)
        {
            _history.Enqueue(new Notification((clock ?? TimeProvider.System).GetUtcNow(), code, severity, message, details));
            while (_history.Count > 50) _history.Dequeue();
            // Serialize event delivery in publication order. Subscribers must not block on the UI thread.
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
