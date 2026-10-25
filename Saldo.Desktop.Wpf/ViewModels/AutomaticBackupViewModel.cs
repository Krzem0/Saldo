using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Localization;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class AutomaticBackupViewModel(IAutomaticDatabaseBackupService backups,
    ILocalizationService localization, ILogger<AutomaticBackupViewModel>? logger = null,
    INotificationService? notifications = null)
    : LocalizedViewModelBase(localization), IDatabaseWriteAccess
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _canWrite;
    private int _pendingRequests;
    private string? _errorKey;
    private AutomaticBackupResult? _lastResult;
    private (int Year, int Month)? _pendingClosedMonth;
    private ICommand? _retryCommand;
    private TaskCompletionSource? _idle;

    public bool CanWrite => _canWrite;
    public bool IsBusy => _pendingRequests > 0;
    public bool HasError => _errorKey is not null;
    public string BackupPath => _lastResult?.Path ?? string.Empty;
    public string StatusText => IsBusy ? T("AutoBackup_Running") : _errorKey is not null ? T(_errorKey)
        : _lastResult is { } result ? string.Format(Localization.CurrentCulture, T("AutoBackup_Ready"), result.Date.ToDateTime(TimeOnly.MinValue))
        : T("AutoBackup_Waiting");
    public ICommand RetryCommand => _retryCommand ??= new AsyncRelayCommand(() => RunAsync(_pendingClosedMonth), () => HasError && !IsBusy);

    public Task InitializeAsync() => RunAsync(null);
    public Task BackupAfterClosingAsync(int year, int month) => RunAsync((year, month));
    public Task WaitForIdleAsync() => _idle?.Task ?? Task.CompletedTask;

    public void EnsureCanWrite()
    {
        if (!CanWrite) throw new DatabaseWriteBlockedException();
    }

    private async Task RunAsync((int Year, int Month)? closedMonth)
    {
        if (_pendingRequests++ == 0)
            _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NotifyState();
        await _gate.WaitAsync();
        try
        {
            var result = closedMonth is { } month
                ? await backups.CreateAfterClosingAsync(month.Year, month.Month)
                : await backups.EnsureDailyAsync();
            // Only a successful daily startup check unlocks database writes.
            if (closedMonth is null) _canWrite = true;
            _lastResult = result;
            _pendingClosedMonth = null;
            _errorKey = result.RetentionSucceeded ? null : "AutoBackup_RetentionFailed";
            if (result.Created)
                notifications?.Publish("Backup.Automatic.Ready", T("Notifications_BackupCreated"),
                    NotificationSeverity.Success, result.Path);
            if (_errorKey is not null)
                notifications?.Publish(_errorKey, T(_errorKey), NotificationSeverity.Warning);
        }
        catch (Exception ex)
        {
            (logger ?? NullLogger<AutomaticBackupViewModel>.Instance).LogError(ex, "Automatic database backup failed.");
            _pendingClosedMonth = closedMonth;
            _errorKey = CanWrite ? "AutoBackup_AfterCloseFailed" : "AutoBackup_StartupFailed";
            notifications?.Publish(_errorKey, T(_errorKey), NotificationSeverity.Error);
        }
        finally
        {
            _gate.Release();
            _pendingRequests--;
            if (_pendingRequests == 0) _idle!.TrySetResult();
            NotifyState();
        }
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(BackupPath));
        CommandManager.InvalidateRequerySuggested();
    }

    protected override void OnCultureChanged() => NotifyState();
}
