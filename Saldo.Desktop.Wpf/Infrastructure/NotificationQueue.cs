using Saldo.Application.Interfaces;

namespace Saldo.Desktop.Wpf.Infrastructure;

/// <summary>Presentation timing independent of dispatcher ticks, with bounded pending work.</summary>
public sealed class NotificationQueue
{
    private readonly List<(Notification Item, int Count)> _pending = [];
    private DateTimeOffset _shownAt;
    public Notification? Current { get; private set; }
    public int CurrentCount { get; private set; } = 1;

    public void Enqueue(Notification notification, DateTimeOffset now)
    {
        if (Current is null || (_pending.Count == 0 && now - _shownAt >= TimeSpan.FromSeconds(5)))
        {
            Current = notification;
            CurrentCount = 1;
            _shownAt = now;
            return;
        }
        // Repeated pending results use their latest wording; all occurrences remain in history.
        var index = _pending.FindIndex(entry => entry.Item.Code == notification.Code && entry.Item.Severity == notification.Severity);
        if (index >= 0)
        {
            _pending[index] = (notification, _pending[index].Count + 1);
            return;
        }
        _pending.Add((notification, 1));
        if (_pending.Count > 50) _pending.RemoveAt(0);
    }

    public void Tick(DateTimeOffset now)
    {
        if (_pending.Count == 0 || now - _shownAt < TimeSpan.FromSeconds(5)) return;
        (Current, CurrentCount) = _pending[0];
        _pending.RemoveAt(0);
        _shownAt = now;
    }
}
