using Saldo.Application.Interfaces;
using Saldo.Application.Notifications;

namespace Saldo.Tests.Unit.Notifications;

public sealed class NotificationServiceTests
{
    [Fact]
    public void History_KeepsLast50AndSnapshotsDoNotChange()
    {
        var service = new NotificationService();
        service.Publish("first", "First", NotificationSeverity.Warning, "Details");
        var snapshot = service.History;
        for (var i = 0; i < 75; i++) service.Publish(i.ToString(), $"Message {i}");
        Assert.Equal(50, service.History.Count);
        Assert.Equal("25", service.History[0].Code);
        Assert.Equal("74", service.History[^1].Code);
        Assert.Equal(NotificationSeverity.Warning, Assert.Single(snapshot).Severity);
        Assert.Equal("Details", snapshot[0].Details);
        Assert.Empty(new NotificationService().History);
    }

    [Fact]
    public void ConcurrentPublication_DeliversEveryEventInHistoryOrder()
    {
        var service = new NotificationService();
        var delivered = new List<Notification>();
        service.Changed += (_, _) => delivered.Add(service.History[^1]);
        Parallel.For(0, 200, i => service.Publish(i.ToString(), "Result"));
        Assert.Equal(200, delivered.Count);
        Assert.Equal(200, delivered.Select(item => item.Code).Distinct().Count());
        Assert.Equal(delivered.TakeLast(50), service.History);
    }
}
