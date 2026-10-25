using Saldo.Application.Interfaces;
using Saldo.Application.Notifications;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;
using System.Windows.Threading;

namespace Saldo.Tests.Wpf;

public sealed class NotificationTests
{
    private static Notification Item(string code) => new(DateTimeOffset.UtcNow, code, NotificationSeverity.Success, code);

    [Fact]
    public void Queue_GivesEachMessageFiveSecondsAndKeepsTheLast()
    {
        var queue = new NotificationQueue();
        var start = DateTimeOffset.UtcNow;
        queue.Enqueue(Item("first"), start);
        queue.Enqueue(Item("second"), start.AddSeconds(1));
        queue.Enqueue(Item("third"), start.AddSeconds(2));
        queue.Tick(start.AddSeconds(4.999));
        Assert.Equal("first", queue.Current!.Code);
        queue.Tick(start.AddSeconds(5));
        Assert.Equal("second", queue.Current.Code);
        queue.Tick(start.AddSeconds(9.999));
        Assert.Equal("second", queue.Current.Code);
        queue.Tick(start.AddSeconds(10));
        Assert.Equal("third", queue.Current.Code);
        queue.Tick(start.AddHours(1));
        Assert.Equal("third", queue.Current.Code);
        queue.Enqueue(Item("fourth"), start.AddHours(1));
        Assert.Equal("fourth", queue.Current.Code);
    }

    [Fact]
    public void Queue_CoalescesRepeatedPendingResults()
    {
        var queue = new NotificationQueue();
        var start = DateTimeOffset.UtcNow;
        queue.Enqueue(Item("first"), start);
        for (var i = 0; i < 100; i++) queue.Enqueue(Item("saved") with { Message = i.ToString() }, start);
        queue.Enqueue(Item("last"), start);
        queue.Tick(start.AddSeconds(5));
        Assert.Equal("99", queue.Current!.Message);
        Assert.Equal(100, queue.CurrentCount);
        queue.Tick(start.AddSeconds(10));
        Assert.Equal("last", queue.Current.Code);
    }

    [Fact]
    public void Presenter_ReceivesBackgroundEventsAndLimitsVisibleHistory() => AccountingPeriodVerificationTests.Run(async () =>
    {
        var service = new NotificationService();
        using var vm = new NotificationsViewModel(service, new LocalizationService());
        Assert.Empty(vm.StatusText);
        Assert.Empty(vm.History);
        await Task.Run(() => { for (var i = 0; i < 80; i++) service.Publish(i.ToString(), $"Message {i}"); });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Assert.Equal(50, vm.History.Count);
        Assert.Equal("79", vm.History[0].Code);
        Assert.Equal("30", vm.History[^1].Code);
        Assert.Equal("Message 0", vm.StatusText);
    });

    [Fact]
    public void ManualNotificationBridge_RecordsSuccessWithoutOpeningDialog()
    {
        var service = new NotificationService();
        new WpfDialogService(service).Notify("Backup", "Created", "backup.db");
        var item = Assert.Single(service.History);
        Assert.Equal(NotificationSeverity.Success, item.Severity);
        Assert.Equal("backup.db", item.Details);
    }

    [Fact]
    public void BackupFailure_RemainsActionableAfterUnrelatedSuccess() => AccountingPeriodVerificationTests.Run(async () =>
    {
        var service = new NotificationService();
        var backups = new AutomaticBackupTests.Backups { FailStartup = true };
        var state = new AutomaticBackupViewModel(backups, new LocalizationService(), notifications: service);
        await state.InitializeAsync();
        service.Publish("Other", "Saved", NotificationSeverity.Success);
        Assert.False(state.CanWrite);
        Assert.True(state.HasError);
        Assert.True(state.RetryCommand.CanExecute(null));
        Assert.Equal(NotificationSeverity.Error, service.History[0].Severity);
        backups.FailStartup = false;
        await state.InitializeAsync();
        Assert.True(state.CanWrite);
        Assert.False(state.HasError);
        Assert.Equal(NotificationSeverity.Success, service.History[^1].Severity);
    });
}
