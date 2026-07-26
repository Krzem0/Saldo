using System.Globalization;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class AddEditTransactionViewModel : LocalizedViewModelBase
{
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

    public string AmountText { get => _amountText; set => SetField(ref _amountText, value); }

    public Category? SelectedCategory { get => _selectedCategory; set => SetField(ref _selectedCategory, value); }
    public Party? SelectedPayer { get => _selectedPayer; set => SetField(ref _selectedPayer, value); }
    public string PayerText { get => _payerText; set => SetField(ref _payerText, value); }
    public Party? SelectedCounterparty { get => _selectedCounterparty; set => SetField(ref _selectedCounterparty, value); }
    public string CounterpartyText { get => _counterpartyText; set => SetField(ref _counterpartyText, value); }
    public Location? SelectedLocation { get => _selectedLocation; set => SetField(ref _selectedLocation, value); }

    public string? Description { get => _description; set => SetField(ref _description, value); }
    public string LocationText { get => _locationText; set => SetField(ref _locationText, value); }

    public IReadOnlyList<Category> Categories { get; }
    public IReadOnlyList<Party> Parties { get; }
    public IReadOnlyList<Location> Locations { get; }

    public bool IsValid =>
        decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) && amount > 0
        && SelectedCategory is not null
        && (SelectedPayer is not null || !string.IsNullOrWhiteSpace(PayerText))
        && (SelectedCounterparty is not null || !string.IsNullOrWhiteSpace(CounterpartyText));

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

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsValid);
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
        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
            return;

        await using var scope = _scopeFactory.CreateAsyncScope();

        if (TransactionId.HasValue)
        {
            var cmd = new EditTransactionCommand(
                TransactionId.Value,
                DateOnly.FromDateTime(Date),
                SelectedType.Value,
                amount,
                SelectedCategory!.Id,
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
                MessageBox.Show(
                    string.Join(Environment.NewLine, result.Errors.Select(e => T(e.Message))),
                    T("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
        }
        else
        {
            var cmd = new AddTransactionCommand(
                DateOnly.FromDateTime(Date),
                SelectedType.Value,
                amount,
                SelectedCategory!.Id,
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
                MessageBox.Show(
                    string.Join(Environment.NewLine, result.Errors.Select(e => T(e.Message))),
                    T("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
        }

        RequestClose?.Invoke(true);
    }

    protected override void OnCultureChanged()
    {
        OnPropertyChanged(nameof(Title));
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
