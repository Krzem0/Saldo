using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class TransactionListViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDialogService _dialogService;

    private int _year;
    private int _month;
    private ObservableCollection<TransactionDto> _transactions = [];
    private MonthlySummaryDto? _summary;
    private TransactionDto? _selectedTransaction;
    private bool _isLoading;

    public int Year { get => _year; private set => SetField(ref _year, value); }
    public int Month { get => _month; private set => SetField(ref _month, value); }
    public string MonthLabel => new DateOnly(Year, Month, 1).ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);

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

    public ICommand LoadCommand { get; }
    public ICommand PreviousMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }

    public TransactionListViewModel(IServiceScopeFactory scopeFactory, IDialogService dialogService)
    {
        _scopeFactory = scopeFactory;
        _dialogService = dialogService;
        _year = DateTime.Today.Year;
        _month = DateTime.Today.Month;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        PreviousMonthCommand = new RelayCommand(PreviousMonth);
        NextMonthCommand = new RelayCommand(NextMonth);
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedTransaction is not null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedTransaction is not null);
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
            MessageBox.Show($"Failed to load transactions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        OnPropertyChanged(nameof(MonthLabel));
        LoadCommand.Execute(null);
    }

    private void NextMonth()
    {
        var d = new DateOnly(Year, Month, 1).AddMonths(1);
        Year = d.Year;
        Month = d.Month;
        OnPropertyChanged(nameof(MonthLabel));
        LoadCommand.Execute(null);
    }

    private async Task AddAsync()
    {
        var (categories, members, counterparties) = await LoadReferenceDataAsync();
        var dialogVm = new AddEditTransactionViewModel(_scopeFactory, categories, members, counterparties);

        if (_dialogService.ShowAddEditTransaction(dialogVm) == true)
            await LoadAsync();
    }

    private async Task EditAsync()
    {
        if (SelectedTransaction is null) return;

        var (categories, members, counterparties) = await LoadReferenceDataAsync();
        var dialogVm = new AddEditTransactionViewModel(_scopeFactory, categories, members, counterparties, SelectedTransaction);

        if (_dialogService.ShowAddEditTransaction(dialogVm) == true)
            await LoadAsync();
    }

    private async Task DeleteAsync()
    {
        if (SelectedTransaction is null) return;

        var confirm = MessageBox.Show(
            $"Delete this transaction ({SelectedTransaction.Date:dd.MM.yyyy}, {SelectedTransaction.Amount:N2})?",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DeleteTransaction>().ExecuteAsync(SelectedTransaction.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to delete: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<(IReadOnlyList<Domain.Entities.Category>, IReadOnlyList<Domain.Entities.Member>, IReadOnlyList<Domain.Entities.Counterparty>)> LoadReferenceDataAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var categories = await scope.ServiceProvider.GetRequiredService<ICategoryRepository>().GetAllAsync();
        var members = await scope.ServiceProvider.GetRequiredService<IMemberRepository>().GetAllAsync();
        var counterparties = await scope.ServiceProvider.GetRequiredService<ICounterpartyRepository>().GetAllAsync();
        return (categories, members, counterparties);
    }
}
