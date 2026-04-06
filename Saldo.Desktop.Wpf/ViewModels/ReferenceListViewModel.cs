using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.ViewModels;

/// <summary>Generic ViewModel for a simple name-based reference list (Category / Member / Counterparty).</summary>
public abstract class ReferenceListViewModel<T> : ViewModelBase where T : class
{
    private readonly IServiceScopeFactory _scopeFactory;
    protected readonly IDialogService DialogService;

    private ObservableCollection<T> _items = [];
    private T? _selectedItem;
    private bool _isLoading;

    public ObservableCollection<T> Items
    {
        get => _items;
        private set => SetField(ref _items, value);
    }

    public T? SelectedItem
    {
        get => _selectedItem;
        set
        {
            SetField(ref _selectedItem, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsLoading { get => _isLoading; private set => SetField(ref _isLoading, value); }

    public ICommand LoadCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }

    protected ReferenceListViewModel(IServiceScopeFactory scopeFactory, IDialogService dialogService)
    {
        _scopeFactory = scopeFactory;
        DialogService = dialogService;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedItem is not null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedItem is not null);
    }

    protected abstract string EntityDisplayName { get; }
    protected abstract Task<IReadOnlyList<T>> GetAllAsync(IServiceScope scope, CancellationToken ct);
    protected abstract string GetName(T item);
    protected abstract Task AddCoreAsync(IServiceScope scope, string name, CancellationToken ct);
    protected abstract Task UpdateCoreAsync(IServiceScope scope, T item, string name, CancellationToken ct);
    protected abstract Task DeleteCoreAsync(IServiceScope scope, T item, CancellationToken ct);

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var list = await GetAllAsync(scope, CancellationToken.None);
            Items = new ObservableCollection<T>(list);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, $"Error loading {EntityDisplayName}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsLoading = false; }
    }

    private async Task AddAsync()
    {
        var name = DialogService.ShowNameDialog($"Add {EntityDisplayName}");
        if (name is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await AddCoreAsync(scope, name.Trim(), CancellationToken.None);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, $"Error adding {EntityDisplayName}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task EditAsync()
    {
        if (SelectedItem is null) return;
        var name = DialogService.ShowNameDialog($"Edit {EntityDisplayName}", GetName(SelectedItem));
        if (name is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await UpdateCoreAsync(scope, SelectedItem, name.Trim(), CancellationToken.None);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, $"Error updating {EntityDisplayName}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedItem is null) return;
        var confirm = MessageBox.Show(
            $"Delete \"{GetName(SelectedItem)}\"?",
            $"Delete {EntityDisplayName}",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await DeleteCoreAsync(scope, SelectedItem, CancellationToken.None);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, $"Error deleting {EntityDisplayName}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
