using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class TransactionTemplateFormViewModel : LocalizedViewModelBase
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IDialogService _dialogs;
    private readonly int? _id;
    private readonly Dictionary<string, string> _errors = [];
    private readonly FormState _initial;
    private string _name = "", _description = "", _amountText = "";
    private string _categoryText = "", _payerText = "", _counterpartyText = "", _locationText = "";
    private string _referenceError = "";
    private Category? _category;
    private Party? _payer, _counterparty;
    private Location? _location;
    private AddEditTransactionViewModel.TypeItem _type;
    private bool _busy;

    public string Title => T(_id.HasValue ? "Template_EditTitle" : "Template_AddTitle");
    public string Name { get => _name; set { if (SetField(ref _name, value)) ClearError(nameof(Name)); } }
    public string Description { get => _description; set { if (SetField(ref _description, value)) ClearError(nameof(Description)); } }
    public string AmountText { get => _amountText; set { if (SetField(ref _amountText, value)) ClearError(nameof(AmountText)); } }
    public Category? SelectedCategory { get => _category; set { if (SetField(ref _category, value)) ClearError(nameof(SelectedCategory)); } }
    public Party? SelectedPayer { get => _payer; set { if (SetField(ref _payer, value)) ClearError(nameof(SelectedPayer)); } }
    public Party? SelectedCounterparty { get => _counterparty; set { if (SetField(ref _counterparty, value)) ClearError(nameof(SelectedCounterparty)); } }
    public Location? SelectedLocation { get => _location; set { if (SetField(ref _location, value)) ClearError(nameof(SelectedLocation)); } }
    public string CategoryText { get => _categoryText; set { if (SetField(ref _categoryText, value)) ClearError(nameof(SelectedCategory)); } }
    public string PayerText { get => _payerText; set { if (SetField(ref _payerText, value)) ClearError(nameof(SelectedPayer)); } }
    public string CounterpartyText { get => _counterpartyText; set { if (SetField(ref _counterpartyText, value)) ClearError(nameof(SelectedCounterparty)); } }
    public string LocationText { get => _locationText; set { if (SetField(ref _locationText, value)) ClearError(nameof(SelectedLocation)); } }
    public AddEditTransactionViewModel.TypeItem SelectedType
    {
        get => _type;
        set { if (SetField(ref _type, value)) { OnPropertyChanged(nameof(IsExpense)); OnPropertyChanged(nameof(IsIncome)); } }
    }
    public bool IsExpense { get => SelectedType.Value == TransactionType.Expense; set { if (value) SelectedType = Types[0]; } }
    public bool IsIncome { get => SelectedType.Value == TransactionType.Income; set { if (value) SelectedType = Types[1]; } }
    public bool IsBusy { get => _busy; private set { SetField(ref _busy, value); OnPropertyChanged(nameof(IsEditable)); CommandManager.InvalidateRequerySuggested(); } }
    public bool IsEditable => !IsBusy;
    public ObservableCollection<Category> Categories { get; }
    public ObservableCollection<Party> Parties { get; }
    public ObservableCollection<Location> Locations { get; }
    public IReadOnlyList<AddEditTransactionViewModel.TypeItem> Types { get; }
    public ObservableCollection<SelectableTag> Tags { get; }
    public string NameError => Error(nameof(Name));
    public string DescriptionError => Error(nameof(Description));
    public string AmountError => Error(nameof(AmountText));
    public string CategoryError => Error(nameof(SelectedCategory));
    public string PayerError => Error(nameof(SelectedPayer));
    public string CounterpartyError => Error(nameof(SelectedCounterparty));
    public string LocationError => Error(nameof(SelectedLocation));
    public string SummaryError => _referenceError.Length > 0 ? _referenceError : Error("");
    public bool HasNameError => NameError.Length > 0;
    public bool HasDescriptionError => DescriptionError.Length > 0;
    public bool HasAmountError => AmountError.Length > 0;
    public bool HasCategoryError => CategoryError.Length > 0;
    public bool HasPayerError => PayerError.Length > 0;
    public bool HasCounterpartyError => CounterpartyError.Length > 0;
    public bool HasLocationError => LocationError.Length > 0;
    public ICommand SaveCommand { get; }
    public ICommand ClearPayerCommand { get; }
    public ICommand ClearCounterpartyCommand { get; }
    public ICommand ClearLocationCommand { get; }
    public ICommand AddCategoryCommand { get; }
    public ICommand AddPayerCommand { get; }
    public ICommand AddCounterpartyCommand { get; }
    public ICommand AddLocationCommand { get; }
    public ICommand AddTagCommand { get; }
    public event Action<bool>? RequestClose;

    public TransactionTemplateFormViewModel(IServiceScopeFactory scopes, IDialogService dialogs,
        ILocalizationService localization, IReadOnlyList<Category> categories, IReadOnlyList<Party> parties,
        IReadOnlyList<Location> locations, IReadOnlyList<Tag> tags, TransactionTemplateDto? existing = null) : base(localization)
    {
        _scopes = scopes;
        _dialogs = dialogs;
        _id = existing?.Id;
        Categories = new(categories); Parties = new(parties); Locations = new(locations);
        Types = [new(TransactionType.Expense, localization), new(TransactionType.Income, localization)];
        _type = Types.First(t => t.Value == (existing?.TransactionType ?? TransactionType.Expense));
        Tags = new(tags.Select(t => new SelectableTag(t.Id, t.Name, t.ColorCode, t.IconKey)
            { IsSelected = existing?.TagIds.Contains(t.Id) == true }));
        if (existing is not null)
        {
            _name = existing.Name; _description = existing.Description ?? string.Empty;
            _amountText = existing.Amount?.ToString("N2", localization.CurrentCulture) ?? "";
            _category = categories.FirstOrDefault(c => c.Id == existing.CategoryId);
            _payer = parties.FirstOrDefault(p => p.Id == existing.PayerId);
            _counterparty = parties.FirstOrDefault(p => p.Id == existing.CounterpartyId);
            _location = locations.FirstOrDefault(l => l.Id == existing.LocationId);
            _categoryText = _category?.Name ?? ""; _payerText = _payer?.Name ?? "";
            _counterpartyText = _counterparty?.Name ?? ""; _locationText = _location?.Name ?? "";
        }
        _initial = State();
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        ClearPayerCommand = new RelayCommand(() => { SelectedPayer = null; PayerText = ""; }, () => !IsBusy);
        ClearCounterpartyCommand = new RelayCommand(() => { SelectedCounterparty = null; CounterpartyText = ""; }, () => !IsBusy);
        ClearLocationCommand = new RelayCommand(() => { SelectedLocation = null; LocationText = ""; }, () => !IsBusy);
        AddCategoryCommand = new AsyncRelayCommand(AddCategoryAsync, () => !IsBusy);
        AddPayerCommand = new AsyncRelayCommand(() => AddPartyAsync(true), () => !IsBusy);
        AddCounterpartyCommand = new AsyncRelayCommand(() => AddPartyAsync(false), () => !IsBusy);
        AddLocationCommand = new AsyncRelayCommand(AddLocationAsync, () => !IsBusy);
        AddTagCommand = new AsyncRelayCommand(AddTagAsync, () => !IsBusy);
    }

    public bool HasUnsavedChanges => State() != _initial;
    public bool CanClose()
    {
        if (IsBusy) return false;
        if (!HasUnsavedChanges) return true;
        var choice = _dialogs.ConfirmUnsavedChanges(T("Transaction_UnsavedChangesTitle"), T("Template_UnsavedChanges"));
        if (choice == UnsavedChangesChoice.Save) SaveCommand.Execute(null);
        return choice == UnsavedChangesChoice.Discard;
    }

    public async Task SaveAsync()
    {
        if (IsBusy) return;
        _errors.Clear();
        _referenceError = "";
        decimal? amount = null;
        if (!string.IsNullOrWhiteSpace(AmountText))
        {
            if (!decimal.TryParse(AmountText, NumberStyles.Number, Localization.CurrentCulture, out var parsed))
            {
                _errors[nameof(AmountText)] = "Validation_AmountMustBeNumber";
                NotifyErrors();
                return;
            }
            amount = parsed;
        }
        var category = Resolve(SelectedCategory, CategoryText, Categories, c => c.Name);
        var payer = Resolve(SelectedPayer, PayerText, Parties, p => p.Name);
        var counterparty = Resolve(SelectedCounterparty, CounterpartyText, Parties, p => p.Name);
        var location = Resolve(SelectedLocation, LocationText, Locations, l => l.Name);
        void Check(object? selected, string text, string field)
        {
            if (selected is null && !string.IsNullOrWhiteSpace(text)) _errors[field] = "Validation_ReferenceSelectionRequired";
        }
        Check(category, CategoryText, nameof(SelectedCategory)); Check(payer, PayerText, nameof(SelectedPayer));
        Check(counterparty, CounterpartyText, nameof(SelectedCounterparty)); Check(location, LocationText, nameof(SelectedLocation));
        if (_errors.Count > 0) { NotifyErrors(); return; }
        var command = new SaveTransactionTemplateCommand(Name, SelectedType.Value, Description,
            category?.Id, payer?.Id, counterparty?.Id, location?.Id,
            amount, Tags.Where(t => t.IsSelected).Select(t => t.Id).ToArray());
        IsBusy = true;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var result = _id is int id
                ? await scope.ServiceProvider.GetRequiredService<EditTransactionTemplate>().ExecuteAsync(id, command)
                : await scope.ServiceProvider.GetRequiredService<AddTransactionTemplate>().ExecuteAsync(command);
            if (result.IsSuccess)
            {
                _dialogs.Notify("Template.Saved", T("Notifications_TemplateSaved"), Name);
                RequestClose?.Invoke(true);
            }
            else foreach (var error in result.Errors) AddError(error);
        }
        catch (Exception)
        {
            _errors[""] = "Template_SaveFailed";
        }
        finally { IsBusy = false; NotifyErrors(); }
    }

    private void AddError(IError error)
    {
        var field = error.Metadata.TryGetValue("PropertyName", out var property) ? property as string : null;
        field = field switch
        {
            "Name" => nameof(Name), "Description" => nameof(Description), "Amount" => nameof(AmountText),
            "CategoryId" => nameof(SelectedCategory), "PayerId" => nameof(SelectedPayer),
            "CounterpartyId" => nameof(SelectedCounterparty), "LocationId" => nameof(SelectedLocation), _ => ""
        };
        _errors[field] = error.Message switch
        {
            "TransactionTemplate.NameRequired" => "Validation_NameRequired",
            "TransactionTemplate.NameTooLong" => "Template_NameTooLong",
            "TransactionTemplate.NameDuplicate" => "Template_NameDuplicate",
            "TransactionTemplate.ContentRequired" => "Template_ContentRequired",
            "TransactionTemplate.ContentDuplicate" => "Template_ContentDuplicate",
            "TransactionTemplate.DescriptionTooLong" => "Validation_DescriptionTooLong",
            "TransactionTemplate.AmountMustBePositive" => "Validation_AmountMustBePositive",
            "TransactionTemplate.CategoryInvalid" => "Template_CategoryInvalid",
            "TransactionTemplate.NotFound" => "Template_NotFound",
            _ => "Template_InvalidReference"
        };
    }

    private string Error(string field) => _errors.TryGetValue(field, out var key) ? T(key) : "";
    private void ClearError(string field) { if (_errors.Remove(field)) NotifyErrors(); }
    private static TItem? Resolve<TItem>(TItem? selected, string text, IEnumerable<TItem> items, Func<TItem, string> name) where TItem : class =>
        selected ?? items.FirstOrDefault(item => string.Equals(name(item), text.Trim(), StringComparison.OrdinalIgnoreCase));
    private void NotifyErrors()
    {
        foreach (var property in new[] { nameof(NameError), nameof(DescriptionError), nameof(AmountError),
            nameof(CategoryError), nameof(PayerError), nameof(CounterpartyError), nameof(LocationError), nameof(SummaryError),
            nameof(HasNameError), nameof(HasDescriptionError), nameof(HasAmountError), nameof(HasCategoryError),
            nameof(HasPayerError), nameof(HasCounterpartyError), nameof(HasLocationError) })
            OnPropertyChanged(property);
    }
    protected override void OnCultureChanged() { OnPropertyChanged(nameof(Title)); NotifyErrors(); }
    private FormState State() => new(Name, Description, AmountText, SelectedType.Value, SelectedCategory?.Id,
        SelectedPayer?.Id, SelectedCounterparty?.Id, SelectedLocation?.Id,
        string.Join(",", Tags.Where(t => t.IsSelected).Select(t => t.Id).Order()), CategoryText, PayerText, CounterpartyText, LocationText);
    private sealed record FormState(string Name, string Description, string Amount, TransactionType Type,
        int? Category, int? Payer, int? Counterparty, int? Location, string Tags,
        string CategoryText, string PayerText, string CounterpartyText, string LocationText);

    private async Task AddCategoryAsync()
    {
        var input = _dialogs.ShowReferenceColorDialog(string.Format(Localization.CurrentCulture, T("AddEntityTitleTemplate"), T("Entity_Category")), allowIcons: true);
        if (input is null) return;
        await AddReferenceAsync(async services =>
        {
            var category = await services.GetRequiredService<AddCategory>().ExecuteAsync(input.Name, input.ColorCode, iconKey: input.IconKey);
            Categories.Add(category); SelectedCategory = category; CategoryText = category.Name;
            NotifyReference("Entity_Category", category.Name);
        });
    }
    private async Task AddPartyAsync(bool payer)
    {
        var name = _dialogs.ShowNameDialog(string.Format(Localization.CurrentCulture, T("AddEntityTitleTemplate"), T("Entity_Party")));
        if (name is null) return;
        await AddReferenceAsync(async services =>
        {
            var party = await services.GetRequiredService<AddParty>().ExecuteAsync(name);
            Parties.Add(party);
            NotifyReference("Entity_Party", party.Name);
            if (payer) { SelectedPayer = party; PayerText = party.Name; }
            else { SelectedCounterparty = party; CounterpartyText = party.Name; }
        });
    }
    private async Task AddLocationAsync()
    {
        var name = _dialogs.ShowNameDialog(string.Format(Localization.CurrentCulture, T("AddEntityTitleTemplate"), T("Entity_Location")));
        if (name is null) return;
        await AddReferenceAsync(async services =>
        {
            var location = await services.GetRequiredService<AddLocation>().ExecuteAsync(name);
            Locations.Add(location); SelectedLocation = location; LocationText = location.Name;
            NotifyReference("Entity_Location", location.Name);
        });
    }
    private async Task AddTagAsync()
    {
        var input = _dialogs.ShowReferenceColorDialog(string.Format(Localization.CurrentCulture, T("AddEntityTitleTemplate"), T("Entity_Tag")), allowIcons: true);
        if (input is null) return;
        await AddReferenceAsync(async services =>
        {
            var tag = await services.GetRequiredService<AddTag>().ExecuteAsync(input.Name, input.ColorCode, iconKey: input.IconKey);
            Tags.Add(new(tag.Id, tag.Name, tag.ColorCode, tag.IconKey) { IsSelected = true });
            NotifyReference("Entity_Tag", tag.Name);
        });
    }
    private async Task AddReferenceAsync(Func<IServiceProvider, Task> add)
    {
        IsBusy = true; _referenceError = "";
        try { await using var scope = _scopes.CreateAsyncScope(); await add(scope.ServiceProvider); }
        catch (Exception error)
        {
            _referenceError = error is DuplicateReferenceException duplicate
                ? string.Format(Localization.CurrentCulture, T("DuplicateReferenceErrorTemplate"), duplicate.Name)
                : error is InvalidTagNameException ? T("Validation_TagNameInvalid") : T("Template_SaveFailed");
        }
        finally { IsBusy = false; OnPropertyChanged(nameof(SummaryError)); }
    }
    private void NotifyReference(string entityKey, string name) => _dialogs.Notify("Reference.Added",
        string.Format(Localization.CurrentCulture, T("Notifications_ReferenceAdded"), T(entityKey), name));
}
