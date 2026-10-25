using System.IO;
using System.Windows;
using System.Windows.Controls;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Tests.Wpf;

public sealed class AutomaticBackupTests
{
    [Fact]
    public void StartupBackup_BlocksOnlyWriteControlsUntilSuccess() => AccountingPeriodVerificationTests.Run(async () =>
    {
        var service = new Backups { PauseStartup = true };
        var state = new AutomaticBackupViewModel(service, new LocalizationService());
        var writeButton = new Button();
        writeButton.Resources["AutomaticBackup"] = state;
        DatabaseWriteGuard.SetIsWriteAction(writeButton, true);
        writeButton.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var readButton = new Button();

        var operation = state.InitializeAsync();
        Assert.True(state.IsBusy);
        Assert.False(state.CanWrite);
        Assert.False(writeButton.IsEnabled);
        Assert.True(readButton.IsEnabled);
        Assert.Throws<DatabaseWriteBlockedException>(state.EnsureCanWrite);
        service.ReleaseStartup.TrySetResult();
        await operation;
        Assert.True(state.CanWrite);
        Assert.True(writeButton.IsEnabled);
        Assert.False(state.HasError);
        Assert.Equal("daily.db", state.BackupPath);
    });

    [Fact]
    public void FailedStartup_RemainsReadOnlyUntilRetrySucceeds() => AccountingPeriodVerificationTests.Run(async () =>
    {
        var service = new Backups { FailStartup = true };
        var state = new AutomaticBackupViewModel(service, new LocalizationService());
        await state.InitializeAsync();
        Assert.False(state.CanWrite);
        Assert.True(state.HasError);
        Assert.True(state.RetryCommand.CanExecute(null));
        service.FailStartup = false;

        state.RetryCommand.Execute(null);
        await WaitAsync(state);

        Assert.True(state.CanWrite);
        Assert.False(state.HasError);
        Assert.Equal(2, service.StartupCalls);
    });

    [Fact]
    public void FailedAfterClose_DoesNotRevokeWriteAccess_AndRetryKeepsMonth() => AccountingPeriodVerificationTests.Run(async () =>
    {
        var service = new Backups { FailAfterClose = true };
        var state = new AutomaticBackupViewModel(service, new LocalizationService());
        await state.InitializeAsync();
        await state.BackupAfterClosingAsync(2026, 1);
        Assert.True(state.CanWrite);
        Assert.True(state.HasError);
        service.FailAfterClose = false;

        state.RetryCommand.Execute(null);
        await WaitAsync(state);

        Assert.False(state.HasError);
        Assert.Equal(new[] { (2026, 1), (2026, 1) }, service.ClosedMonths);
        Assert.Equal(1, service.StartupCalls);
    });

    [Fact]
    public void RetentionFailure_DoesNotBlockWrites_AndRetryDoesNotRequestReplacement() => AccountingPeriodVerificationTests.Run(async () =>
    {
        var service = new Backups { RetentionSucceeded = false };
        var state = new AutomaticBackupViewModel(service, new LocalizationService());
        await state.InitializeAsync();
        Assert.True(state.CanWrite);
        Assert.True(state.HasError);
        service.RetentionSucceeded = true;

        state.RetryCommand.Execute(null);
        await WaitAsync(state);

        Assert.False(state.HasError);
        Assert.Equal(2, service.StartupCalls);
        Assert.Empty(service.ClosedMonths);
    });

    [Fact]
    public void IdleWait_IncludesQueuedBackupAndItsFailure() => AccountingPeriodVerificationTests.Run(async () =>
    {
        var service = new Backups { PauseStartup = true, PauseAfterClose = true, FailAfterClose = true };
        var state = new AutomaticBackupViewModel(service, new LocalizationService());
        Assert.True(state.WaitForIdleAsync().IsCompleted);
        var startup = state.InitializeAsync();
        var closingBackup = state.BackupAfterClosingAsync(2026, 1);
        var idle = state.WaitForIdleAsync();
        Assert.False(idle.IsCompleted);
        service.ReleaseStartup.TrySetResult();
        await service.AfterCloseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(idle.IsCompleted);
        service.ReleaseAfterClose.TrySetResult();
        await Task.WhenAll(startup, closingBackup, idle).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(state.IsBusy);
        Assert.True(state.HasError);
    });

    private static async Task WaitAsync(AutomaticBackupViewModel state)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (state.IsBusy) await Task.Delay(5, timeout.Token);
    }

    internal sealed class Backups : IAutomaticDatabaseBackupService
    {
        public bool PauseStartup { get; init; }
        public bool FailStartup { get; set; }
        public bool FailAfterClose { get; set; }
        public bool PauseAfterClose { get; init; }
        public bool RetentionSucceeded { get; set; } = true;
        public int StartupCalls { get; private set; }
        public List<(int, int)> ClosedMonths { get; } = [];
        public TaskCompletionSource ReleaseStartup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AfterCloseEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseAfterClose { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<AutomaticBackupResult> EnsureDailyAsync(CancellationToken ct = default)
        {
            StartupCalls++;
            if (PauseStartup) await ReleaseStartup.Task.WaitAsync(ct);
            if (FailStartup) throw new IOException("Simulated startup backup failure.");
            return new("daily.db", new DateOnly(2026, 10, 25), true, RetentionSucceeded);
        }
        public async Task<AutomaticBackupResult> CreateAfterClosingAsync(int year, int month, CancellationToken ct = default)
        {
            ClosedMonths.Add((year, month));
            AfterCloseEntered.TrySetResult();
            if (PauseAfterClose) await ReleaseAfterClose.Task.WaitAsync(ct);
            if (FailAfterClose) throw new IOException("Simulated after-close backup failure.");
            return new AutomaticBackupResult("daily.db", new DateOnly(2026, 10, 25), true, RetentionSucceeded);
        }
    }
}
