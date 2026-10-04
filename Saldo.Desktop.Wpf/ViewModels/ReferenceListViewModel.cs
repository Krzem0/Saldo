using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Services;
using Saldo.Application.Errors;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed record ReferenceItemInput(string Name, string? ColorCode = null);

/// <summary>Generic ViewModel for a simple name-based reference list (Category / Member / Counterparty).</summary>
public abstract class ReferenceListViewModel<T> : LocalizedViewModelBase where T : class
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

   protected ReferenceListViewModel(IServiceScopeFactory scopeFactory, IDialogService dialogService, ILocalizationService localization)
        : base(localization)
    {
        _scopeFactory = scopeFactory;
        DialogService = dialogService;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedItem is not null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedItem is not null);
    }

    protected abstract string EntityDisplayNameKey { get; }
    protected abstract Task<IReadOnlyList<T>> GetAllAsync(IServiceScope scope, CancellationToken ct);
    protected abstract string GetName(T item);
    protected abstract Task AddCoreAsync(IServiceScope scope, string name, string? colorCode, CancellationToken ct);
    protected abstract Task UpdateCoreAsync(IServiceScope scope, T item, string name, string? colorCode, CancellationToken ct);
    protected abstract Task DeleteCoreAsync(IServiceScope scope, T item, CancellationToken ct);

    protected virtual ReferenceItemInput? ShowAddDialog()
    {
        var name = DialogService.ShowNameDialog(string.Format(CultureInfo.CurrentCulture, T("AddEntityTitleTemplate"), EntityDisplayName));
        return name is null ? null : new ReferenceItemInput(name);
    }

    protected virtual ReferenceItemInput? ShowEditDialog(T item)
    {
        var name = DialogService.ShowNameDialog(string.Format(CultureInfo.CurrentCulture, T("EditEntityTitleTemplate"), EntityDisplayName), GetName(item));
        return name is null ? null : new ReferenceItemInput(name);
    }

    protected string EntityDisplayName => T(EntityDisplayNameKey);
    public string Title => EntityDisplayName;

    protected override void OnCultureChanged()
    {
        OnPropertyChanged(nameof(Title));
    }

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
          MessageBox.Show(ex.Message, string.Format(CultureInfo.CurrentCulture, T("LoadErrorTemplate"), EntityDisplayName), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsLoading = false; }
    }

    private async Task AddAsync()
    {
        var input = ShowAddDialog();
        if (input is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await AddCoreAsync(scope, input.Name.Trim(), input.ColorCode, CancellationToken.None);
            await LoadAsync();
        }
        catch (Exception ex)
        {
           var message = ex is DuplicateReferenceException duplicate
               ? string.Format(CultureInfo.CurrentCulture, T("DuplicateReferenceErrorTemplate"), duplicate.Name)
               : ex.Message;
           MessageBox.Show(message, string.Format(CultureInfo.CurrentCulture, T("AddErrorTemplate"), EntityDisplayName), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task EditAsync()
    {
        if (SelectedItem is null) return;
        var input = ShowEditDialog(SelectedItem);
        if (input is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await UpdateCoreAsync(scope, SelectedItem, input.Name.Trim(), input.ColorCode, CancellationToken.None);
            await LoadAsync();
        }
        catch (Exception ex)
        {
         MessageBox.Show(ex.Message, string.Format(CultureInfo.CurrentCulture, T("UpdateErrorTemplate"), EntityDisplayName), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedItem is null) return;
        var confirm = DialogService.ConfirmDelete(
            string.Format(CultureInfo.CurrentCulture, T("DeleteEntityTitleTemplate"), EntityDisplayName),
            string.Format(CultureInfo.CurrentCulture, T("DeleteConfirmTemplate"), GetName(SelectedItem)));
        if (!confirm) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await DeleteCoreAsync(scope, SelectedItem, CancellationToken.None);
            await LoadAsync();
        }
        catch (ReferenceEntityInUseException)
        {
            MessageBox.Show(
                string.Format(CultureInfo.CurrentCulture, T("DeleteInUseErrorTemplate"), GetName(SelectedItem)),
                string.Format(CultureInfo.CurrentCulture, T("DeleteEntityTitleTemplate"), EntityDisplayName),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
         MessageBox.Show(
             string.Format(CultureInfo.CurrentCulture, T("DeleteErrorTemplate"), EntityDisplayName),
             T("ErrorTitle"),
             MessageBoxButton.OK,
             MessageBoxImage.Error);
        }
    }
}
