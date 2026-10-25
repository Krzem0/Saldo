namespace Saldo.Application.Interfaces;

public enum NotificationSeverity { Information, Success, Warning, Error }

/// <summary>A session event. Text is localized by the presentation layer.</summary>
public sealed record Notification(DateTimeOffset Timestamp, string Code, NotificationSeverity Severity,
    string Message, string? Details = null);

public interface INotificationService
{
    IReadOnlyList<Notification> History { get; }
    event EventHandler? Changed;
    void Publish(string code, string message, NotificationSeverity severity = NotificationSeverity.Information,
        string? details = null);
}
