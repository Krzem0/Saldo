using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Saldo.Application.Interfaces;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Views;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class NotificationsViewModel : LocalizedViewModelBase, IDisposable
{
    private readonly INotificationService _service;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;
    private readonly NotificationQueue _queue = new();
    private NotificationHistoryDialog? _window;
    private bool _disposed;
    public ObservableCollection<Notification> History { get; } = [];
    public Notification? Current => _queue.Current;
    public string StatusText => Current is null ? string.Empty : _queue.CurrentCount > 1
        ? string.Format(Localization.CurrentCulture, T("Notifications_Repeated"), Current.Message, _queue.CurrentCount)
        : Current.Message;
    public ICommand ShowHistoryCommand { get; }

    public NotificationsViewModel(INotificationService service, ILocalizationService localization) : base(localization)
    {
        _service = service;
        _dispatcher = Dispatcher.CurrentDispatcher;
        foreach (var item in service.History) { History.Insert(0, item); _queue.Enqueue(item, DateTimeOffset.UtcNow); }
        service.Changed += OnChanged;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background,
            (_, _) =>
            {
                var previous = Current;
                _queue.Tick(DateTimeOffset.UtcNow);
                if (!ReferenceEquals(previous, Current)) Refresh();
            }, _dispatcher);
        ShowHistoryCommand = new RelayCommand(() =>
        {
            if (_window is not null) { _window.Activate(); return; }
            _window = new NotificationHistoryDialog { DataContext = this, Owner = System.Windows.Application.Current.MainWindow };
            _window.Closed += (_, _) => _window = null;
            _window.Show();
        });
    }

    private void OnChanged(object? sender, EventArgs args)
    {
        if (_disposed || _dispatcher.HasShutdownStarted) return;
        var item = _service.History[^1];
        _dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            History.Insert(0, item);
            while (History.Count > 50) History.RemoveAt(History.Count - 1);
            _queue.Enqueue(item, DateTimeOffset.UtcNow);
            Refresh();
        });
    }
    private void Refresh() { OnPropertyChanged(nameof(Current)); OnPropertyChanged(nameof(StatusText)); }
    protected override void OnCultureChanged() => Refresh();
    public void Dispose() { _disposed = true; _timer.Stop(); _service.Changed -= OnChanged; _window?.Close(); }
}
