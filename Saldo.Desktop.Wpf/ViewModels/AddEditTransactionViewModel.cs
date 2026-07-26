using System.Globalization;
using System.ComponentModel;
using System.Windows.Input;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class AddEditTransactionViewModel : LocalizedViewModelBase
{
    private const string AmountRequiredError = "Validation_AmountRequired";
    private const string AmountMustBeNumberError = "Validation_AmountMustBeNumber";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyList<TypeItem> _types;

    private DateTime _date = DateTime.Today;
    private TypeItem _selectedType;
    private string _amountText = string.Empty;
    private Category? _selectedCategory;
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
    public string Title => TransactionId.HasValue ? T("Transaction_EditTitle") : T("Transaction_AddTitle");

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
    public Location? SelectedLocation { get => _selectedLocation; set => SetField(ref _selectedLocation, value); }

    public string? Description { get => _description; set => SetField(ref _description, value); }
    public string LocationText { get => _locationText; set => SetField(ref _locationText, value); }

    public IReadOnlyList<Category> Categories { get; }
    public IReadOnlyList<Party> Parties { get; }
    public IReadOnlyList<Location> Locations { get; }

    public string AmountError => GetErrorText(nameof(AmountText));
    public string CategoryError => GetErrorText(nameof(SelectedCategory));
    public string PayerError => GetErrorText(nameof(PayerText));
    public string CounterpartyError => GetErrorText(nameof(CounterpartyText));
    public string ValidationSummary => GetErrorText(string.Empty);

    public bool HasAmountError => HasError(nameof(AmountText));
    public bool HasCategoryError => HasError(nameof(SelectedCategory));
    public bool HasPayerError => HasError(nameof(PayerText));
    public bool HasCounterpartyError => HasError(nameof(CounterpartyText));
    public bool HasValidationSummary => HasError(string.Empty);

    public event Action<bool>? RequestClose;

    public ICommand SaveCommand { get; }

    public AddEditTransactionViewModel(
        IServiceScopeFactory scopeFactory,
        ILocalizationService localization,
        IReadOnlyList<Category> categories,
        IReadOnlyList<Party> parties,
        IReadOnlyList<Location> locations,
        NewTransactionDefaultsDto? defaults = null,
        TransactionDto? existing = null)
        : base(localization)
    {
        _scopeFactory = scopeFactory;
        Categories = categories;
        Parties = parties;
        Locations = locations;
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
        }

        SaveCommand = new AsyncRelayCommand(SaveAsync);
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
            var viewModelProperty = MapCommandProperty(propertyName);

            if (!_validationErrorCodes.TryGetValue(viewModelProperty, out var codes))
            {
                codes = [];
                _validationErrorCodes[viewModelProperty] = codes;
            }

            codes.Add(error.Message);
        }

        NotifyAllErrorProperties();
    }

    private static string MapCommandProperty(string? propertyName) => propertyName switch
    {
        nameof(ITransactionCommand.Amount) => nameof(AmountText),
        nameof(ITransactionCommand.CategoryId) => nameof(SelectedCategory),
        nameof(ITransactionCommand.PayerName) => nameof(PayerText),
        nameof(ITransactionCommand.CounterpartyName) => nameof(CounterpartyText),
        _ => string.Empty
    };

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
