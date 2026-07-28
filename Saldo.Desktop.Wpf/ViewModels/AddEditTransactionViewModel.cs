using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Services;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class AddEditTransactionViewModel : LocalizedViewModelBase
{
    private const string AmountRequiredError = "Validation_AmountRequired";
    private const string AmountMustBeNumberError = "Validation_AmountMustBeNumber";
    private const string ReferenceSelectionRequiredError = "Validation_ReferenceSelectionRequired";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDialogService _dialogService;
    private readonly IReadOnlyList<TypeItem> _types;
    private bool _isRestoredDraft;
    private TransactionDraft? _initialDraft;

    private DateTime _date = DateTime.Today;
    private TypeItem _selectedType;
    private string _amountText = string.Empty;
    private Category? _selectedCategory;
    private string _categoryText = string.Empty;
    private Party? _selectedPayer;
    private string _payerText = string.Empty;
    private Party? _selectedCounterparty;
    private string _counterpartyText = string.Empty;
    private Location? _selectedLocation;
    private string _locationText = string.Empty;
    private string? _description;
    private readonly Dictionary<string, List<string>> _validationErrorCodes = [];

    public sealed class TypeItem : ViewModelBase
    {
        private readonly ILocalizationService _localization;
        private string _label;

        public TransactionType Value { get; }

        public string Label
        {
            get => _label;
            private set => SetField(ref _label, value);
        }

        public TypeItem(TransactionType value, ILocalizationService localization)
        {
            Value = value;
            _localization = localization;
            _label = GetLabel();
            _localization.PropertyChanged += OnLocalizationChanged;
        }

        private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ILocalizationService.CurrentCulture) || e.PropertyName == "Item[]")
            {
                Label = GetLabel();
            }
        }

        private string GetLabel() => Value switch
        {
            TransactionType.Expense => _localization["Type_Expense"],
            TransactionType.Income => _localization["Type_Income"],
            _ => Value.ToString()
        };
    }

    public IReadOnlyList<TypeItem> Types => _types;

    public int? TransactionId { get; private set; }
    public string Title => _isRestoredDraft
        ? T("Transaction_AddDraftTitle")
        : TransactionId.HasValue ? T("Transaction_EditTitle") : T("Transaction_AddTitle");

    public DateTime Date { get => _date; set => SetField(ref _date, value); }

    public TypeItem SelectedType
    {
        get => _selectedType;
        set => SetField(ref _selectedType, value);
    }

    public string AmountText
    {
        get => _amountText;
        set
        {
            if (SetField(ref _amountText, value)) ClearFieldError(nameof(AmountText));
        }
    }

    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetField(ref _selectedCategory, value)) ClearFieldError(nameof(SelectedCategory));
        }
    }
    public string CategoryText
    {
        get => _categoryText;
        set
        {
            if (SetField(ref _categoryText, value)) ClearFieldError(nameof(SelectedCategory));
        }
    }
    public Party? SelectedPayer
    {
        get => _selectedPayer;
        set
        {
            if (SetField(ref _selectedPayer, value)) ClearFieldError(nameof(PayerText));
        }
    }
    public string PayerText
    {
        get => _payerText;
        set
        {
            if (SetField(ref _payerText, value)) ClearFieldError(nameof(PayerText));
        }
    }
    public Party? SelectedCounterparty
    {
        get => _selectedCounterparty;
        set
        {
            if (SetField(ref _selectedCounterparty, value)) ClearFieldError(nameof(CounterpartyText));
        }
    }
    public string CounterpartyText
    {
        get => _counterpartyText;
        set
        {
            if (SetField(ref _counterpartyText, value)) ClearFieldError(nameof(CounterpartyText));
        }
    }
    public Location? SelectedLocation
    {
        get => _selectedLocation;
        set
        {
            if (SetField(ref _selectedLocation, value)) ClearFieldError(nameof(LocationText));
        }
    }

    public string? Description { get => _description; set => SetField(ref _description, value); }
    public string LocationText
    {
        get => _locationText;
        set
        {
            if (SetField(ref _locationText, value)) ClearFieldError(nameof(LocationText));
        }
    }

    public ObservableCollection<Category> Categories { get; private set; }
    public ObservableCollection<Party> Parties { get; private set; }
    public ObservableCollection<Location> Locations { get; private set; }

    public string AmountError => GetErrorText(nameof(AmountText));
    public string CategoryError => GetErrorText(nameof(SelectedCategory));
    public string PayerError => GetErrorText(nameof(PayerText));
    public string CounterpartyError => GetErrorText(nameof(CounterpartyText));
    public string LocationError => GetErrorText(nameof(LocationText));
    public string ValidationSummary => GetErrorText(string.Empty);

    public bool HasAmountError => HasError(nameof(AmountText));
    public bool HasCategoryError => HasError(nameof(SelectedCategory));
    public bool HasPayerError => HasError(nameof(PayerText));
    public bool HasCounterpartyError => HasError(nameof(CounterpartyText));
    public bool HasLocationError => HasError(nameof(LocationText));
    public bool HasValidationSummary => HasError(string.Empty);

    public event Action<bool>? RequestClose;

    public ICommand SaveCommand { get; }
    public ICommand AddCategoryCommand { get; }
    public ICommand AddPayerCommand { get; }
    public ICommand AddCounterpartyCommand { get; }
    public ICommand AddLocationCommand { get; }

    public AddEditTransactionViewModel(
        IServiceScopeFactory scopeFactory,
        IDialogService dialogService,
        ILocalizationService localization,
        IReadOnlyList<Category> categories,
        IReadOnlyList<Party> parties,
        IReadOnlyList<Location> locations,
        NewTransactionDefaultsDto? defaults = null,
        TransactionDto? existing = null,
        TransactionDraft? draft = null)
        : base(localization)
    {
        _scopeFactory = scopeFactory;
        _dialogService = dialogService;
        Categories = new ObservableCollection<Category>(categories);
        Parties = new ObservableCollection<Party>(parties);
        Locations = new ObservableCollection<Location>(locations);
        _types =
        [
            new TypeItem(TransactionType.Expense, localization),
            new TypeItem(TransactionType.Income, localization)
        ];
        _selectedType = _types[0];

        if (defaults is not null)
        {
            ApplyDefaults(defaults);
        }

        if (existing is not null)
        {
            PopulateFrom(existing);
            _initialDraft = CreateDraft();
        }

        if (draft is not null)
        {
            _isRestoredDraft = true;
            ApplyDraft(draft);
        }

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        AddCategoryCommand = new AsyncRelayCommand(AddCategoryAsync);
        AddPayerCommand = new AsyncRelayCommand(AddPayerAsync);
        AddCounterpartyCommand = new AsyncRelayCommand(AddCounterpartyAsync);
        AddLocationCommand = new AsyncRelayCommand(AddLocationAsync);
    }

    private Task AddPayerAsync() => AddPartyAsync(true);

    private Task AddCounterpartyAsync() => AddPartyAsync(false);

    private async Task AddCategoryAsync()
    {
        var name = _dialogService.ShowNameDialog(
            string.Format(CultureInfo.CurrentCulture, T("AddEntityTitleTemplate"), T("Entity_Category")));
        if (name is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var category = await scope.ServiceProvider.GetRequiredService<AddCategory>().ExecuteAsync(name);
            Categories.Add(category);
            SelectedCategory = category;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, T("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task AddPartyAsync(bool payer)
    {
        var name = _dialogService.ShowNameDialog(
            string.Format(CultureInfo.CurrentCulture, T("AddEntityTitleTemplate"), T("Entity_Party")));
        if (name is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var party = await scope.ServiceProvider.GetRequiredService<AddParty>().ExecuteAsync(name);
            Parties.Add(party);

            if (payer)
            {
                SelectedPayer = party;
                PayerText = party.Name;
            }
            else
            {
                SelectedCounterparty = party;
                CounterpartyText = party.Name;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, T("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task AddLocationAsync()
    {
        var name = _dialogService.ShowNameDialog(
            string.Format(CultureInfo.CurrentCulture, T("AddEntityTitleTemplate"), T("Entity_Location")));
        if (name is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var location = await scope.ServiceProvider.GetRequiredService<AddLocation>().ExecuteAsync(name);
            Locations.Add(location);
            SelectedLocation = location;
            LocationText = location.Name;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, T("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyDefaults(NewTransactionDefaultsDto defaults)
    {
        _date = defaults.Date.ToDateTime(TimeOnly.MinValue);
        _selectedType = Types.FirstOrDefault(d => d.Value == defaults.Type) ?? Types[0];
        _selectedPayer = defaults.PayerId.HasValue
            ? Parties.FirstOrDefault(p => p.Id == defaults.PayerId.Value)
            : null;
        _payerText = _selectedPayer?.Name ?? string.Empty;
    }

    private void PopulateFrom(TransactionDto t)
    {
        TransactionId = t.Id;
        _date = t.Date.ToDateTime(TimeOnly.MinValue);
        _selectedType = Types.FirstOrDefault(d => d.Value == t.Type) ?? Types[0];
        _amountText = t.Amount.ToString("N2", CultureInfo.CurrentCulture);
        _selectedCategory = Categories.FirstOrDefault(c => c.Id == t.CategoryId);
        _categoryText = _selectedCategory?.Name ?? string.Empty;
        _selectedPayer = Parties.FirstOrDefault(p => p.Id == t.PayerId);
        _payerText = t.PayerName;
        _selectedCounterparty = Parties.FirstOrDefault(p => p.Id == t.CounterpartyId);
        _counterpartyText = t.CounterpartyName;
        _selectedLocation = t.LocationId.HasValue
            ? Locations.FirstOrDefault(location => location.Id == t.LocationId.Value)
            : null;
        _description = t.Description;
        _locationText = t.Location ?? string.Empty;
    }

    public TransactionDraft CreateDraft() => new()
    {
        TransactionId = TransactionId,
        Date = Date,
        Type = SelectedType.Value,
        AmountText = AmountText,
        CategoryId = SelectedCategory?.Id,
        CategoryText = CategoryText,
        PayerId = SelectedPayer?.Id,
        PayerText = PayerText,
        CounterpartyId = SelectedCounterparty?.Id,
        CounterpartyText = CounterpartyText,
        LocationId = SelectedLocation?.Id,
        LocationText = LocationText,
        Description = Description
    };

    public bool HasUnsavedChanges => _initialDraft is not null && !DraftsMatch(_initialDraft, CreateDraft());

    private static bool DraftsMatch(TransactionDraft left, TransactionDraft right) =>
        left.TransactionId == right.TransactionId
        && left.Date == right.Date
        && left.Type == right.Type
        && left.AmountText == right.AmountText
        && left.CategoryId == right.CategoryId
        && left.CategoryText == right.CategoryText
        && left.PayerId == right.PayerId
        && left.PayerText == right.PayerText
        && left.CounterpartyId == right.CounterpartyId
        && left.CounterpartyText == right.CounterpartyText
        && left.LocationId == right.LocationId
        && left.LocationText == right.LocationText
        && left.Description == right.Description;

    private void ApplyDraft(TransactionDraft draft)
    {
        TransactionId = draft.TransactionId;
        _date = draft.Date;
        _selectedType = Types.FirstOrDefault(type => type.Value == draft.Type) ?? Types[0];
        _amountText = draft.AmountText;
        _selectedCategory = draft.CategoryId.HasValue
            ? Categories.FirstOrDefault(category => category.Id == draft.CategoryId.Value)
            : null;
        _categoryText = draft.CategoryText;
        _selectedPayer = draft.PayerId.HasValue
            ? Parties.FirstOrDefault(party => party.Id == draft.PayerId.Value)
            : null;
        _payerText = draft.PayerText;
        _selectedCounterparty = draft.CounterpartyId.HasValue
            ? Parties.FirstOrDefault(party => party.Id == draft.CounterpartyId.Value)
            : null;
        _counterpartyText = draft.CounterpartyText;
        _selectedLocation = draft.LocationId.HasValue
            ? Locations.FirstOrDefault(location => location.Id == draft.LocationId.Value)
            : null;
        _locationText = draft.LocationText;
        _description = draft.Description;
    }

    private async Task SaveAsync()
    {
        string? amountInputError = null;
        decimal amount;

        if (string.IsNullOrWhiteSpace(AmountText))
        {
            amount = 0;
            amountInputError = AmountRequiredError;
        }
        else if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
        {
            amount = 0;
            amountInputError = AmountMustBeNumberError;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();

        if (TransactionId.HasValue)
        {
            var cmd = new EditTransactionCommand(
                TransactionId.Value,
                DateOnly.FromDateTime(Date),
                SelectedType.Value,
                amount,
                SelectedCategory?.Id ?? 0,
                SelectedPayer?.Id,
                ResolvePartyName(SelectedPayer, PayerText),
                SelectedCounterparty?.Id,
                ResolvePartyName(SelectedCounterparty, CounterpartyText),
                Description,
                ResolveLocationName(),
                []);

            var result = await scope.ServiceProvider.GetRequiredService<EditTransaction>().ExecuteAsync(cmd);
            if (result.IsFailed)
            {
                ApplyValidationErrors(result.Errors);
                if (amountInputError is not null)
                    SetFieldError(nameof(AmountText), amountInputError);
                return;
            }
        }
        else
        {
            var cmd = new AddTransactionCommand(
                DateOnly.FromDateTime(Date),
                SelectedType.Value,
                amount,
                SelectedCategory?.Id ?? 0,
                SelectedPayer?.Id,
                ResolvePartyName(SelectedPayer, PayerText),
                SelectedCounterparty?.Id,
                ResolvePartyName(SelectedCounterparty, CounterpartyText),
                Description,
                ResolveLocationName(),
                []);

            var result = await scope.ServiceProvider.GetRequiredService<AddTransaction>().ExecuteAsync(cmd);
            if (result.IsFailed)
            {
                ApplyValidationErrors(result.Errors);
                if (amountInputError is not null)
                    SetFieldError(nameof(AmountText), amountInputError);
                return;
            }
        }

        RequestClose?.Invoke(true);
    }

    protected override void OnCultureChanged()
    {
        OnPropertyChanged(nameof(Title));
        NotifyAllErrorProperties();
    }

    private void ApplyValidationErrors(IEnumerable<IError> errors)
    {
        _validationErrorCodes.Clear();

        foreach (var error in errors)
        {
            var propertyName = error.Metadata.TryGetValue("PropertyName", out var value)
                ? value?.ToString()
                : null;
            var viewModelProperty = MapCommandProperty(propertyName, error.Message);
            var errorCode = GetDisplayErrorCode(viewModelProperty, error.Message);

            if (!_validationErrorCodes.TryGetValue(viewModelProperty, out var codes))
            {
                codes = [];
                _validationErrorCodes[viewModelProperty] = codes;
            }

            codes.Add(errorCode);
        }

        NotifyAllErrorProperties();
    }

    private static string MapCommandProperty(string? propertyName, string errorCode) => propertyName switch
    {
        nameof(ITransactionCommand.Amount) => nameof(AmountText),
        nameof(ITransactionCommand.CategoryId) => nameof(SelectedCategory),
        nameof(ITransactionCommand.PayerName) => nameof(PayerText),
        nameof(ITransactionCommand.CounterpartyName) => nameof(CounterpartyText),
        _ => errorCode switch
        {
            ErrorCodes.Transaction.PayerRequired => nameof(PayerText),
            ErrorCodes.Transaction.CounterpartyRequired => nameof(CounterpartyText),
            ErrorCodes.Transaction.LocationInvalid => nameof(LocationText),
            _ => string.Empty
        }
    };

    private string GetDisplayErrorCode(string propertyName, string errorCode)
    {
        if (errorCode is ErrorCodes.Transaction.CategoryRequired
            or ErrorCodes.Transaction.PayerRequired
            or ErrorCodes.Transaction.CounterpartyRequired)
        {
            var hasTypedValue = propertyName switch
            {
                nameof(SelectedCategory) => !string.IsNullOrWhiteSpace(CategoryText),
                nameof(PayerText) => !string.IsNullOrWhiteSpace(PayerText),
                nameof(CounterpartyText) => !string.IsNullOrWhiteSpace(CounterpartyText),
                _ => false
            };

            if (hasTypedValue)
            {
                return ReferenceSelectionRequiredError;
            }
        }

        return errorCode;
    }

    private bool HasError(string propertyName) =>
        _validationErrorCodes.TryGetValue(propertyName, out var errors) && errors.Count > 0;

    private string GetErrorText(string propertyName) =>
        _validationErrorCodes.TryGetValue(propertyName, out var errors)
            ? string.Join(Environment.NewLine, errors.Select(LocalizeError))
            : string.Empty;

    private string LocalizeError(string errorCode) => errorCode switch
    {
        AmountRequiredError => T(AmountRequiredError),
        AmountMustBeNumberError => T(AmountMustBeNumberError),
        ErrorCodes.Transaction.IdMustBePositive => T("Validation_TransactionIdMustBePositive"),
        ErrorCodes.Transaction.AmountMustBePositive => T("Validation_AmountMustBePositive"),
        ErrorCodes.Transaction.CategoryRequired => T("Validation_CategoryRequired"),
        ErrorCodes.Transaction.PayerRequired => T("Validation_PayerRequired"),
        ErrorCodes.Transaction.CounterpartyRequired => T("Validation_CounterpartyRequired"),
        ErrorCodes.Transaction.LocationInvalid => T("Validation_LocationInvalid"),
        ReferenceSelectionRequiredError => T(ReferenceSelectionRequiredError),
        ErrorCodes.Transaction.NotFound => T("Validation_TransactionNotFound"),
        _ => errorCode
    };

    private void SetFieldError(string propertyName, string errorCode)
    {
        _validationErrorCodes[propertyName] = [errorCode];
        NotifyErrorProperties(propertyName);
    }

    private void ClearFieldError(string propertyName)
    {
        if (_validationErrorCodes.Remove(propertyName))
        {
            NotifyErrorProperties(propertyName);
        }
    }

    private void NotifyAllErrorProperties()
    {
        NotifyErrorProperties(nameof(AmountText));
        NotifyErrorProperties(nameof(SelectedCategory));
        NotifyErrorProperties(nameof(PayerText));
        NotifyErrorProperties(nameof(CounterpartyText));
        NotifyErrorProperties(nameof(LocationText));
        NotifyErrorProperties(string.Empty);
    }

    private void NotifyErrorProperties(string propertyName)
    {
        var (textProperty, visibilityProperty) = propertyName switch
        {
            nameof(AmountText) => (nameof(AmountError), nameof(HasAmountError)),
            nameof(SelectedCategory) => (nameof(CategoryError), nameof(HasCategoryError)),
            nameof(PayerText) => (nameof(PayerError), nameof(HasPayerError)),
            nameof(CounterpartyText) => (nameof(CounterpartyError), nameof(HasCounterpartyError)),
            nameof(LocationText) => (nameof(LocationError), nameof(HasLocationError)),
            _ => (nameof(ValidationSummary), nameof(HasValidationSummary))
        };

        OnPropertyChanged(textProperty);
        OnPropertyChanged(visibilityProperty);
    }

    private string? ResolveLocationName()
    {
        if (SelectedLocation is not null)
        {
            return SelectedLocation.Name;
        }

        return string.IsNullOrWhiteSpace(LocationText) ? null : LocationText.Trim();
    }

    private static string? ResolvePartyName(Party? selectedParty, string? text)
    {
        if (selectedParty is not null)
        {
            return selectedParty.Name;
        }

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
