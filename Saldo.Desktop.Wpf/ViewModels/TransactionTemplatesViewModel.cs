using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Domain.Enums;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class TransactionTemplatesViewModel : LocalizedViewModelBase
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IDialogService _dialogs;
    private TemplateRow? _selected;
    private bool _busy;
    private string _errorKey = "";
    private IReadOnlyList<TemplateRow> _allItems = [];
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { if (SetField(ref _searchText, value)) FilterItems(SelectedItem?.Template.Id); }
    }
    public string EmptyMessage => T(_allItems.Count == 0 ? "Template_Empty" : "TemplatePicker_NoMatches");
    public string Title => T("Templates");
    public string ErrorMessage => _errorKey.Length == 0 ? "" : T(_errorKey);
    public ObservableCollection<TemplateRow> Items { get; } = [];
    public TemplateRow? SelectedItem { get => _selected; set { SetField(ref _selected, value); CommandManager.InvalidateRequerySuggested(); } }
    public bool IsBusy { get => _busy; private set { SetField(ref _busy, value); CommandManager.InvalidateRequerySuggested(); } }
    public ICommand LoadCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public TransactionTemplatesViewModel(IServiceScopeFactory scopes, IDialogService dialogs, ILocalizationService localization) : base(localization)
    {
        _scopes = scopes; _dialogs = dialogs;
        LoadCommand = new AsyncRelayCommand(() => LoadAsync(), () => !IsBusy);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => SearchText.Length > 0);
        AddCommand = new AsyncRelayCommand(() => OpenAsync(null), () => !IsBusy);
        EditCommand = new AsyncRelayCommand(() => OpenAsync(SelectedItem?.Template.Id), () => !IsBusy && SelectedItem is not null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !IsBusy && SelectedItem is not null);
    }

    public async Task LoadAsync(int? selectedId = null)
    {
        IsBusy = true; SetError("");
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var templates = await scope.ServiceProvider.GetRequiredService<ListTransactionTemplates>().ExecuteAsync();
            var categories = await scope.ServiceProvider.GetRequiredService<ICategoryRepository>().GetAllAsync();
            selectedId ??= SelectedItem?.Template.Id;
            _allItems = templates.Select(template => new TemplateRow(template,
                categories.FirstOrDefault(c => c.Id == template.CategoryId)?.Name ?? "", Localization)).ToArray();
            FilterItems(selectedId);
        }
        catch (Exception) { SetError("Template_LoadFailed"); }
        finally { IsBusy = false; }
    }

    private async Task OpenAsync(int? id)
    {
        IsBusy = true; SetError("");
        var saved = false;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var services = scope.ServiceProvider;
            TransactionTemplateDto? existing = null;
            if (id is int templateId)
            {
                var result = await services.GetRequiredService<GetTransactionTemplate>().ExecuteAsync(templateId);
                if (result.IsFailed) { SetError("Template_NotFound"); return; }
                existing = result.Value;
            }
            var categories = await services.GetRequiredService<ICategoryRepository>().GetAllAsync();
            var parties = await services.GetRequiredService<IPartyRepository>().GetAllAsync();
            var locations = await services.GetRequiredService<ILocationRepository>().GetAllAsync();
            var tags = await services.GetRequiredService<ITagRepository>().GetAllAsync();
            var form = new TransactionTemplateFormViewModel(_scopes, _dialogs, Localization, categories, parties, locations, tags, existing);
            saved = _dialogs.ShowTransactionTemplate(form) == true;
        }
        catch (Exception) { SetError("Template_LoadFailed"); }
        finally { IsBusy = false; }
        if (saved) await LoadAsync(id);
    }

    public async Task DeleteAsync()
    {
        var selected = SelectedItem;
        if (selected is null || IsBusy) return;
        if (!_dialogs.ConfirmDelete(T("Template_DeleteTitle"), string.Format(Localization.CurrentCulture, T("DeleteConfirmTemplate"), selected.Template.Name))) return;
        IsBusy = true; SetError("");
        var deleted = false;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<DeleteTransactionTemplate>().ExecuteAsync(selected.Template.Id);
            deleted = result.IsSuccess;
            if (!deleted) SetError("Template_NotFound");
        }
        catch (Exception) { SetError("Template_DeleteFailed"); }
        finally { IsBusy = false; }
        if (deleted)
        {
            _dialogs.Notify("Template.Deleted", T("Notifications_TemplateDeleted"), selected.Template.Name);
            await LoadAsync();
        }
    }
    private void SetError(string key)
    {
        _errorKey = key; OnPropertyChanged(nameof(ErrorMessage));
        if (key.Length > 0)
        {
            _dialogs.Notify(key, T(key), severity: NotificationSeverity.Error);
        }
    }
    private void FilterItems(int? selectedId)
    {
        var query = SearchText.Trim();
        SelectedItem = null;
        Items.Clear();
        foreach (var row in _allItems.Where(row => row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
            Items.Add(row);
        SelectedItem = Items.FirstOrDefault(row => row.Template.Id == selectedId) ?? Items.FirstOrDefault();
        OnPropertyChanged(nameof(EmptyMessage));
    }
    protected override void OnCultureChanged()
    {
        OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(EmptyMessage));
        foreach (var row in _allItems) row.Refresh();
    }

    public sealed class TemplateRow(TransactionTemplateDto template, string categoryName, ILocalizationService localization) : ViewModelBase
    {
        public TransactionTemplateDto Template { get; } = template;
        public string Name => Template.Name;
        public string Description => Template.Description ?? string.Empty;
        public string CategoryName { get; } = categoryName;
        public string TypeLabel => localization[Template.TransactionType == TransactionType.Expense ? "Type_Expense" : "Type_Income"];
        public string AmountLabel => Template.Amount?.ToString("N2", localization.CurrentCulture) ?? "—";
        public void Refresh() { OnPropertyChanged(nameof(TypeLabel)); OnPropertyChanged(nameof(AmountLabel)); }
    }
}
