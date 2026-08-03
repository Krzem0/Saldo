using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class TransactionListViewModel : LocalizedViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDialogService _dialogService;

    private int _year;
    private int _month;
    private bool _isMonthPickerOpen;
    private int _pickerYear;
    private string _pickerYearText = string.Empty;
    private ObservableCollection<TransactionDto> _transactions = [];
    private MonthlySummaryDto? _summary;
    private TransactionDto? _selectedTransaction;
    private bool _isLoading;
    private TransactionDraft? _newTransactionDraft;

    public sealed class MonthItem : ViewModelBase
    {
        private readonly ILocalizationService _localization;
        private string _label;

        public int Number { get; }

        public string Label
        {
            get => _label;
            private set => SetField(ref _label, value);
        }

        public MonthItem(int number, ILocalizationService localization)
        {
            Number = number;
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

        private string GetLabel() => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(Number);
    }

    public int Year  { get => _year;  private set => SetField(ref _year,  value); }
    public int Month { get => _month; private set => SetField(ref _month, value); }
    public string MonthLabel => new DateOnly(Year, Month, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    public bool IsCurrentMonth => Year == DateTime.Today.Year && Month == DateTime.Today.Month;

    public bool IsMonthPickerOpen
    {
        get => _isMonthPickerOpen;
        set => SetField(ref _isMonthPickerOpen, value);
    }

    public int PickerYear
    {
        get => _pickerYear;
        private set
        {
            if (SetField(ref _pickerYear, value))
            {
                _pickerYearText = value.ToString(CultureInfo.CurrentCulture);
                OnPropertyChanged(nameof(PickerYearText));
            }
        }
    }

    public string PickerYearText
    {
        get => _pickerYearText;
        set
        {
            if (!SetField(ref _pickerYearText, value))
            {
                return;
            }

            if (int.TryParse(value, out var year) && year > 0 && year != _pickerYear)
            {
                _pickerYear = year;
                OnPropertyChanged(nameof(PickerYear));
            }
        }
    }

    public IReadOnlyList<MonthItem> PickerMonths { get; }

    public ObservableCollection<TransactionDto> Transactions
    {
        get => _transactions;
        private set => SetField(ref _transactions, value);
    }

    public MonthlySummaryDto? Summary
    {
        get => _summary;
        private set => SetField(ref _summary, value);
    }

    public TransactionDto? SelectedTransaction
    {
        get => _selectedTransaction;
        set
        {
            SetField(ref _selectedTransaction, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsLoading { get => _isLoading; private set => SetField(ref _isLoading, value); }

    public ICommand LoadCommand              { get; }
    public ICommand PreviousMonthCommand     { get; }
    public ICommand NextMonthCommand         { get; }
    public ICommand CurrentMonthCommand      { get; }
    public ICommand OpenMonthPickerCommand   { get; }
    public ICommand PreviousPickerYearCommand { get; }
    public ICommand NextPickerYearCommand    { get; }
    public ICommand SelectMonthCommand       { get; }
    public ICommand AddCommand               { get; }
    public ICommand EditCommand              { get; }
    public ICommand DeleteCommand            { get; }

    public TransactionListViewModel(IServiceScopeFactory scopeFactory, IDialogService dialogService, ILocalizationService localization)
        : base(localization)
    {
        _scopeFactory = scopeFactory;
        _dialogService = dialogService;
        _year       = DateTime.Today.Year;
        _month      = DateTime.Today.Month;
        _pickerYear = _year;

        PickerMonths = Enumerable.Range(1, 12)
            .Select(m => new MonthItem(m, localization))
            .ToList();

        LoadCommand               = new AsyncRelayCommand(LoadAsync);
        PreviousMonthCommand      = new RelayCommand(PreviousMonth);
        NextMonthCommand          = new RelayCommand(NextMonth);
        CurrentMonthCommand       = new RelayCommand(GoToCurrentMonth, () => !IsCurrentMonth);
        OpenMonthPickerCommand    = new RelayCommand(OpenMonthPicker);
        PreviousPickerYearCommand = new RelayCommand(() => PickerYear--);
        NextPickerYearCommand     = new RelayCommand(() => PickerYear++);
        SelectMonthCommand        = new RelayCommand<int>(SelectMonth);
        AddCommand                = new AsyncRelayCommand(AddAsync);
        EditCommand               = new AsyncRelayCommand(EditAsync,   () => SelectedTransaction is not null);
        DeleteCommand             = new AsyncRelayCommand(DeleteAsync, () => SelectedTransaction is not null);
    }

    private void OpenMonthPicker()
    {
        PickerYear = Year;
        PickerYearText = PickerYear.ToString(CultureInfo.CurrentCulture);
        IsMonthPickerOpen = true;
    }

    private void SelectMonth(int month)
    {
        if (int.TryParse(PickerYearText, out var year) && year > 0)
        {
            PickerYear = year;
        }

        Month = month;
        Year  = PickerYear;
        IsMonthPickerOpen = false;
        RefreshSelectedMonth();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var query = new ListTransactionsQuery(Year, Month);

            var transactions = await scope.ServiceProvider.GetRequiredService<ListTransactions>().ExecuteAsync(query);
            var summary = await scope.ServiceProvider.GetRequiredService<GetSummary>().ExecuteAsync(query);

            Transactions = new ObservableCollection<TransactionDto>(transactions);
            Summary = summary;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, T("TransactionsLoadError"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void PreviousMonth()
    {
        var d = new DateOnly(Year, Month, 1).AddMonths(-1);
        Year = d.Year;
        Month = d.Month;
        RefreshSelectedMonth();
    }

    private void NextMonth()
    {
        var d = new DateOnly(Year, Month, 1).AddMonths(1);
        Year = d.Year;
        Month = d.Month;
        RefreshSelectedMonth();
    }

    private void GoToCurrentMonth()
    {
        Year = DateTime.Today.Year;
        Month = DateTime.Today.Month;
        RefreshSelectedMonth();
    }

    private void RefreshSelectedMonth()
    {
        OnPropertyChanged(nameof(MonthLabel));
        OnPropertyChanged(nameof(IsCurrentMonth));
        CommandManager.InvalidateRequerySuggested();
        LoadCommand.Execute(null);
    }

    private async Task AddAsync()
    {
        var (categories, parties, locations, defaults) = await LoadNewTransactionDataAsync();
        var dialogVm = new AddEditTransactionViewModel(
            _scopeFactory, _dialogService, Localization, categories, parties, locations, defaults,
            draft: _newTransactionDraft);

        if (_dialogService.ShowAddEditTransaction(dialogVm) == true)
        {
            _newTransactionDraft = null;
            await LoadAsync();
        }
        else
        {
            _newTransactionDraft = dialogVm.CreateDraft();
        }
    }

    private async Task EditAsync()
    {
        if (SelectedTransaction is null) return;

        var (categories, parties, locations) = await LoadReferenceDataAsync();
        var dialogVm = new AddEditTransactionViewModel(
            _scopeFactory, _dialogService, Localization, categories, parties, locations,
            existing: SelectedTransaction);

        if (_dialogService.ShowAddEditTransaction(dialogVm) == true)
        {
            await LoadAsync();
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedTransaction is null) return;

        var confirm = MessageBox.Show(
            string.Format(CultureInfo.CurrentCulture, T("DeleteConfirmTemplate"), $"{SelectedTransaction.Date:dd.MM.yyyy}, {SelectedTransaction.Amount:N2}"),
            T("DeleteTransactionTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<DeleteTransaction>().ExecuteAsync(SelectedTransaction.Id);
            if (result.IsFailed)
            {
                MessageBox.Show(
                    string.Join(Environment.NewLine, result.Errors.Select(e => T(e.Message))),
                    T("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, T("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<(IReadOnlyList<Domain.Entities.Category>, IReadOnlyList<Domain.Entities.Party>, IReadOnlyList<Domain.Entities.Location>)> LoadReferenceDataAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var categoriesTask = scope.ServiceProvider.GetRequiredService<ICategoryRepository>().GetAllAsync();
        var partiesTask = scope.ServiceProvider.GetRequiredService<IPartyRepository>().GetAllAsync();
        var locationsTask = scope.ServiceProvider.GetRequiredService<ILocationRepository>().GetAllAsync();
        await Task.WhenAll(categoriesTask, partiesTask, locationsTask);
        return (await categoriesTask, await partiesTask, await locationsTask);
    }

    private async Task<(IReadOnlyList<Domain.Entities.Category>, IReadOnlyList<Domain.Entities.Party>, IReadOnlyList<Domain.Entities.Location>, NewTransactionDefaultsDto)> LoadNewTransactionDataAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var categoriesTask = scope.ServiceProvider.GetRequiredService<ICategoryRepository>().GetAllAsync();
        var partiesTask = scope.ServiceProvider.GetRequiredService<IPartyRepository>().GetAllAsync();
        var locationsTask = scope.ServiceProvider.GetRequiredService<ILocationRepository>().GetAllAsync();
        var defaultsTask = scope.ServiceProvider.GetRequiredService<GetNewTransactionDefaults>().ExecuteAsync();

        await Task.WhenAll(categoriesTask, partiesTask, locationsTask, defaultsTask);

        return (await categoriesTask, await partiesTask, await locationsTask, await defaultsTask);
    }

    protected override void OnCultureChanged()
    {
        OnPropertyChanged(nameof(MonthLabel));
        LoadCommand.Execute(null);
    }
}
