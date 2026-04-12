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
    private readonly IReadOnlyList<DirectionItem> _directions;

    private DateTime _date = DateTime.Today;
    private DirectionItem _selectedDirection;
    private string _amountText = string.Empty;
    private Category? _selectedCategory;
    private Party? _selectedPayer;
    private Party? _selectedCounterparty;
    private string? _description;
    private string? _location;

    public sealed class DirectionItem : ViewModelBase
    {
        private readonly ILocalizationService _localization;
        private string _label;

        public TransactionDirection Value { get; }

        public string Label
        {
            get => _label;
            private set => SetField(ref _label, value);
        }

        public DirectionItem(TransactionDirection value, ILocalizationService localization)
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
            TransactionDirection.Expense => _localization["Direction_Expense"],
            TransactionDirection.Income => _localization["Direction_Income"],
            _ => Value.ToString()
        };
    }

    public IReadOnlyList<DirectionItem> Directions => _directions;

    public int? TransactionId { get; private set; }
    public string Title => TransactionId.HasValue ? T("Transaction_EditTitle") : T("Transaction_AddTitle");

    public DateTime Date { get => _date; set => SetField(ref _date, value); }

    public DirectionItem SelectedDirection
    {
        get => _selectedDirection;
        set => SetField(ref _selectedDirection, value);
    }

    public string AmountText { get => _amountText; set => SetField(ref _amountText, value); }

    public Category? SelectedCategory { get => _selectedCategory; set => SetField(ref _selectedCategory, value); }
    public Party? SelectedPayer { get => _selectedPayer; set => SetField(ref _selectedPayer, value); }
    public Party? SelectedCounterparty { get => _selectedCounterparty; set => SetField(ref _selectedCounterparty, value); }

    public string? Description { get => _description; set => SetField(ref _description, value); }
    public string? Location { get => _location; set => SetField(ref _location, value); }

    public IReadOnlyList<Category> Categories { get; }
    public IReadOnlyList<Party> Parties { get; }

    public bool IsValid =>
        decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) && amount > 0
        && SelectedCategory is not null
        && SelectedPayer is not null
        && SelectedCounterparty is not null;

    public event Action<bool>? RequestClose;

    public ICommand SaveCommand { get; }

    public AddEditTransactionViewModel(
        IServiceScopeFactory scopeFactory,
        ILocalizationService localization,
        IReadOnlyList<Category> categories,
        IReadOnlyList<Party> parties,
        TransactionDto? existing = null)
        : base(localization)
    {
        _scopeFactory = scopeFactory;
        Categories = categories;
        Parties = parties;
        _directions =
        [
            new DirectionItem(TransactionDirection.Expense, localization),
            new DirectionItem(TransactionDirection.Income, localization)
        ];
        _selectedDirection = _directions[0]; // default: Expense

        if (existing is not null)
            PopulateFrom(existing);

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsValid);
    }

    private void PopulateFrom(TransactionDto t)
    {
        TransactionId = t.Id;
        _date = t.Date.ToDateTime(TimeOnly.MinValue);
        _selectedDirection = Directions.FirstOrDefault(d => d.Value == t.Direction) ?? Directions[0];
        _amountText = t.Amount.ToString("N2", CultureInfo.CurrentCulture);
        _selectedCategory = Categories.FirstOrDefault(c => c.Id == t.CategoryId);
        _selectedPayer = Parties.FirstOrDefault(p => p.Id == t.PayerId);
        _selectedCounterparty = Parties.FirstOrDefault(p => p.Id == t.CounterpartyId);
        _description = t.Description;
        _location = t.Location;
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
                SelectedDirection.Value,
                amount,
                SelectedCategory!.Id,
                SelectedPayer!.Id,
                SelectedCounterparty!.Id,
                Description,
                Location,
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
                SelectedDirection.Value,
                amount,
                SelectedCategory!.Id,
                SelectedPayer!.Id,
                SelectedCounterparty!.Id,
                Description,
                Location,
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
}
