using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Data;
using Saldo.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class TransactionListViewModel : LocalizedViewModelBase, ITransactionTemplatePickerHost
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDialogService _dialogService;
    private readonly AutomaticBackupViewModel? _automaticBackups;

    private int _year;
    private int _month;
    private bool _isMonthPickerOpen;
    private int _pickerYear;
    private string _pickerYearText = string.Empty;
    private MonthlySummaryDto? _summary;
    private TransactionDto? _selectedTransaction;
    private bool _isLoading;
    private int _loadVersion;
    private bool _isChangingPeriod;
    private bool? _isPeriodClosed;
    private bool _periodLoadFailed;
    private AccountingPeriodVerificationDto? _periodVerification;
    private TaskCompletionSource? _periodChangeFinished;
    private TransactionType? _typeFilter;
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                ApplyFilters();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
    public ICommand ClearSearchCommand { get; }
    private TransactionDraft? _newTransactionDraft;
    private DateOnly _lastNewTransactionDate = DateOnly.FromDateTime(DateTime.Today);
    public TransactionTemplatePickerViewModel TemplatePicker { get; }
    public bool IsTemplatePickerOpen { get => TemplatePicker.IsTemplatePickerOpen; set => TemplatePicker.IsTemplatePickerOpen = value; }
    public bool IsLoadingTemplates => TemplatePicker.IsLoadingTemplates;
    public string TemplatePickerError => TemplatePicker.TemplatePickerError;
    public ObservableCollection<TransactionTemplateDto> FilteredTemplates => TemplatePicker.FilteredTemplates;
    public TransactionTemplateDto? SelectedTemplate { get => TemplatePicker.SelectedTemplate; set => TemplatePicker.SelectedTemplate = value; }
    public string TemplateSearch { get => TemplatePicker.TemplateSearch; set => TemplatePicker.TemplateSearch = value; }
    public string TemplatePickerStatus => TemplatePicker.TemplatePickerStatus;
    public ICommand OpenTemplatePickerCommand => TemplatePicker.OpenTemplatePickerCommand;
    public ICommand UseTemplateCommand => TemplatePicker.UseTemplateCommand;
    public Task OpenTemplatePickerAsync() => TemplatePicker.OpenTemplatePickerAsync();
    public async Task AddFromTemplateAsync(int id)
    {
        if (!CanCreateTransaction) return;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<GetTransactionTemplate>().ExecuteAsync(id);
            if (result.IsFailed)
            {
                TemplatePicker.ReportError("Template_NotFound");
                return;
            }
            var (categories, parties, locations, tags, defaults) = await LoadNewTransactionDataAsync();
            defaults = defaults with { Date = _lastNewTransactionDate };
            var form = new AddEditTransactionViewModel(_scopeFactory, _dialogService, Localization,
                categories, parties, locations, tags, defaults, template: result.Value);
            IsTemplatePickerOpen = false;
            if (_dialogService.ShowAddEditTransaction(form) == true)
            {
                _lastNewTransactionDate = DateOnly.FromDateTime(form.Date);
                await LoadAsync();
            }
            else if (_newTransactionDraft is null && form.HasDraftContent)
            {
                _newTransactionDraft = form.CreateDraft();
            }
        }
        catch (Exception)
        {
            TemplatePicker.ReportError("TemplatePicker_LoadFailed");
        }
    }

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
    public string MonthLabel
    {
        get
        {
            var culture = CultureInfo.CurrentCulture;
            var label = new DateOnly(Year, Month, 1).ToString("MMMM yyyy", culture).ToLower(culture);
            return char.ToUpper(label[0], culture) + label[1..];
        }
    }
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

    public ObservableCollection<TransactionDto> Transactions { get; } = [];

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

    public bool IsLoading
    {
        get => _isLoading;
        private set { if (SetField(ref _isLoading, value)) NotifyPeriodStateChanged(); }
    }
    public bool IsChangingPeriod
    {
        get => _isChangingPeriod;
        private set { if (SetField(ref _isChangingPeriod, value)) NotifyPeriodStateChanged(); }
    }
    public bool? IsPeriodClosed => _isPeriodClosed;
    public bool CanCreateTransaction => !IsLoading && !IsChangingPeriod && (_automaticBackups?.CanWrite ?? true);
    public Task WaitForPeriodChangeAsync() => _periodChangeFinished?.Task ?? Task.CompletedTask;
    public string PeriodStateText => IsChangingPeriod ? T("AccountingPeriod_Changing")
        : IsLoading ? T("AccountingPeriod_Loading")
        : _isPeriodClosed switch
        {
            true => T("AccountingPeriod_Closed"),
            false => T("AccountingPeriod_Open"),
            null => T("AccountingPeriod_Unknown")
        };
    public string PeriodActionText => T(_isPeriodClosed == true ? "AccountingPeriod_Reopen" : "AccountingPeriod_Close");
    public string PeriodIntegrityText => IsLoading || IsChangingPeriod ? string.Empty
        : _periodLoadFailed ? T("AccountingPeriod.VerificationFailed")
        : _periodVerification?.Status switch
        {
            AccountingPeriodVerificationStatus.Mismatch => T("AccountingPeriod_IntegrityMismatch") + " "
                + string.Join(", ", _periodVerification.Differences.Select(difference => T($"AccountingPeriod_Difference_{difference}"))),
            AccountingPeriodVerificationStatus.MissingSnapshot => T("AccountingPeriod_IntegrityMissing"),
            AccountingPeriodVerificationStatus.UnsupportedHashVersion => T("AccountingPeriod_IntegrityUnsupported"),
            _ => string.Empty
        };
    public bool HasPeriodIssue => !IsLoading && !IsChangingPeriod && !string.IsNullOrEmpty(PeriodIntegrityText);

    private void SetPeriodVerification(AccountingPeriodVerificationDto? verification, bool failed = false)
    {
        _periodVerification = verification;
        _periodLoadFailed = failed;
        _isPeriodClosed = verification is null ? null : verification.Status != AccountingPeriodVerificationStatus.Open;
        NotifyPeriodStateChanged();
    }

    private void NotifyPeriodStateChanged()
    {
        OnPropertyChanged(nameof(IsPeriodClosed));
        OnPropertyChanged(nameof(CanCreateTransaction));
        OnPropertyChanged(nameof(PeriodStateText));
        OnPropertyChanged(nameof(PeriodActionText));
        OnPropertyChanged(nameof(PeriodIntegrityText));
        OnPropertyChanged(nameof(HasPeriodIssue));
        CommandManager.InvalidateRequerySuggested();
    }

    public bool IsIncomeFilterActive => _typeFilter == TransactionType.Income;
    public bool IsExpenseFilterActive => _typeFilter == TransactionType.Expense;
    public bool IsAllFilterActive => _typeFilter is null;
    public ICommand ShowIncomeCommand { get; }
    public ICommand ShowExpensesCommand { get; }
    public ICommand ShowAllCommand { get; }

    private void SetTypeFilter(TransactionType? type)
    {
        _typeFilter = type;
        ApplyFilters();
        OnPropertyChanged(nameof(IsIncomeFilterActive));
        OnPropertyChanged(nameof(IsExpenseFilterActive));
        OnPropertyChanged(nameof(IsAllFilterActive));
    }

    private void ApplyFilters()
    {
        var query = SearchText.Trim();
        var view = CollectionViewSource.GetDefaultView(Transactions);
        view.Filter = item => item is TransactionDto transaction
            && (_typeFilter is null || transaction.Type == _typeFilter)
            && (query.Length == 0 || MatchesSearch(transaction, query));
        if (SelectedTransaction is not null && !view.Contains(SelectedTransaction))
            SelectedTransaction = null;
    }

    private static bool MatchesSearch(TransactionDto transaction, string query) =>
        new[] { transaction.Description, transaction.CategoryName, transaction.CounterpartyName,
            transaction.PayerName, transaction.Location }
            .Any(text => text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
        || transaction.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase));

    public ICommand LoadCommand              { get; }
    public ICommand PreviousMonthCommand     { get; }
    public ICommand NextMonthCommand         { get; }
    public ICommand CurrentMonthCommand      { get; }
    public ICommand OpenMonthPickerCommand   { get; }
    public ICommand PreviousPickerYearCommand { get; }
    public ICommand NextPickerYearCommand    { get; }
    public ICommand SelectMonthCommand       { get; }
    public ICommand AddCommand               { get; }
    public ICommand AddSimilarCommand        { get; }
    public ICommand EditCommand              { get; }
    public ICommand DeleteCommand            { get; }
    public ICommand ChangePeriodCommand      { get; }

    public TransactionListViewModel(IServiceScopeFactory scopeFactory, IDialogService dialogService, ILocalizationService localization, AutomaticBackupViewModel? automaticBackups = null)
        : base(localization)
    {
        _scopeFactory = scopeFactory;
        _dialogService = dialogService;
        _automaticBackups = automaticBackups;
        if (_automaticBackups is not null)
            _automaticBackups.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AutomaticBackupViewModel.CanWrite)) NotifyPeriodStateChanged();
            };
        _year       = DateTime.Today.Year;
        _month      = DateTime.Today.Month;
        _pickerYear = _year;

        ShowIncomeCommand = new RelayCommand(() => SetTypeFilter(TransactionType.Income));
        ShowExpensesCommand = new RelayCommand(() => SetTypeFilter(TransactionType.Expense));
        ShowAllCommand = new RelayCommand(() => SetTypeFilter(null));
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => SearchText.Length > 0);

        PickerMonths = Enumerable.Range(1, 12)
            .Select(m => new MonthItem(m, localization))
            .ToList();

        LoadCommand               = new AsyncRelayCommand(LoadAsync, () => !IsChangingPeriod);
        PreviousMonthCommand      = new RelayCommand(PreviousMonth, () => !IsChangingPeriod);
        NextMonthCommand          = new RelayCommand(NextMonth, () => !IsChangingPeriod);
        CurrentMonthCommand       = new RelayCommand(GoToCurrentMonth, () => !IsCurrentMonth && !IsChangingPeriod);
        OpenMonthPickerCommand    = new RelayCommand(OpenMonthPicker, () => !IsChangingPeriod);
        PreviousPickerYearCommand = new RelayCommand(() => PickerYear--);
        NextPickerYearCommand     = new RelayCommand(() => PickerYear++);
        SelectMonthCommand        = new RelayCommand<int>(SelectMonth);
        AddCommand                = new AsyncRelayCommand(AddAsync, () => CanCreateTransaction);
        AddSimilarCommand         = new AsyncRelayCommand<TransactionDto>(AddSimilarAsync, _ => CanCreateTransaction);
        TemplatePicker = new TransactionTemplatePickerViewModel(scopeFactory, localization, AddFromTemplateAsync, () => CanCreateTransaction);
        EditCommand               = new AsyncRelayCommand(EditAsync, () => SelectedTransaction is not null && _isPeriodClosed == false && CanCreateTransaction);
        DeleteCommand             = new AsyncRelayCommand(DeleteAsync, () => SelectedTransaction is not null && _isPeriodClosed == false && CanCreateTransaction);
        ChangePeriodCommand       = new AsyncRelayCommand(ChangePeriodAsync, () => _isPeriodClosed.HasValue && CanCreateTransaction);
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

        IsMonthPickerOpen = false;
        SetSelectedMonth(PickerYear, month);
    }

    private async Task LoadAsync()
    {
        var loadVersion = ++_loadVersion;
        var year = Year;
        var month = Month;
        IsLoading = true;
        SetPeriodVerification(null);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var query = new ListTransactionsQuery(year, month);

            var transactions = await scope.ServiceProvider.GetRequiredService<ListTransactions>().ExecuteAsync(query);
            var summary = await scope.ServiceProvider.GetRequiredService<GetSummary>().ExecuteAsync(query);
            var verification = await scope.ServiceProvider.GetRequiredService<VerifyAccountingPeriod>()
                .ExecuteAsync(year, month);
            // A slower verification for a previous selection must not replace the
            // current month or display a warning for a month the user has left.
            if (loadVersion != _loadVersion || year != Year || month != Month) return;

            // Keep the collection view so refreshing data preserves the user's sorting.
            Transactions.Clear();
            foreach (var transaction in transactions) Transactions.Add(transaction);
            ApplyFilters();
            Summary = summary;
            SetPeriodVerification(verification.IsSuccess ? verification.Value : null, verification.IsFailed);
            if (verification.IsFailed)
            {
                _dialogService.ShowMessage(T("AccountingPeriod_VerificationTitle"),
                    T("AccountingPeriod.VerificationFailed"), MessageDialogKind.Error);
            }
            else
            {
                ShowVerificationWarning(verification.Value);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "User operation failed.");
            if (loadVersion == _loadVersion)
            {
                SetPeriodVerification(null, failed: true);
                _dialogService.ShowMessage(T("TransactionsLoadError"), T("Notifications_OperationFailed"), MessageDialogKind.Error);
            }
        }
        finally
        {
            if (loadVersion == _loadVersion) IsLoading = false;
        }
    }

    private async Task ChangePeriodAsync()
    {
        if (!_isPeriodClosed.HasValue || !CanCreateTransaction) return;
        var reopen = _isPeriodClosed.Value;
        var year = Year;
        var month = Month;
        var title = T(reopen ? "AccountingPeriod_Reopen" : "AccountingPeriod_Close");
        _periodChangeFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IsChangingPeriod = true;
        IsMonthPickerOpen = false;
        IsTemplatePickerOpen = false;
        try
        {
            var message = string.Format(Localization.CurrentCulture,
                T(reopen ? "AccountingPeriod_ReopenConfirm" : "AccountingPeriod_CloseConfirm"), MonthLabel);
            if (!_dialogService.ConfirmAccountingPeriodChange(title, message, title)) return;

            SetPeriodVerification(null);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = reopen
                ? await scope.ServiceProvider.GetRequiredService<ReopenAccountingPeriod>().ExecuteAsync(year, month)
                : await scope.ServiceProvider.GetRequiredService<CloseAccountingPeriod>().ExecuteAsync(year, month);
            if (result.IsFailed)
                _dialogService.ShowMessage(title, string.Join(Environment.NewLine, result.Errors.Select(error => T(error.Message))), MessageDialogKind.Warning);
            await LoadAsync();
            if (result.IsSuccess)
                _dialogService.Notify(reopen ? "Period.Reopened" : "Period.Closed",
                    string.Format(Localization.CurrentCulture, T(reopen ? "Notifications_PeriodReopened" : "Notifications_PeriodClosed"), MonthLabel));
            // The backup service verifies the closed month again before replacing
            // today's file; failures are reported independently of the saved close.
            if (!reopen && result.IsSuccess && _automaticBackups is not null)
                _ = _automaticBackups.BackupAfterClosingAsync(year, month);
        }
        catch (Exception)
        {
            SetPeriodVerification(null, failed: true);
            _dialogService.ShowMessage(title, T("AccountingPeriod_ChangeFailed"), MessageDialogKind.Error);
        }
        finally
        {
            IsChangingPeriod = false;
            _periodChangeFinished.TrySetResult();
        }
    }

    private void ShowVerificationWarning(AccountingPeriodVerificationDto verification)
    {
        var message = verification.Status switch
        {
            AccountingPeriodVerificationStatus.Mismatch => T("AccountingPeriod_VerificationMismatch")
                + Environment.NewLine + string.Join(", ", verification.Differences.Select(
                    difference => T($"AccountingPeriod_Difference_{difference}"))),
            AccountingPeriodVerificationStatus.MissingSnapshot => T("AccountingPeriod_VerificationMissing"),
            AccountingPeriodVerificationStatus.UnsupportedHashVersion => T("AccountingPeriod_VerificationUnsupported"),
            _ => null
        };
        if (message is not null)
            _dialogService.ShowMessage(T("AccountingPeriod_VerificationTitle"), message, MessageDialogKind.Warning);
    }

    private void PreviousMonth()
    {
        if (IsChangingPeriod) return;
        var d = new DateOnly(Year, Month, 1).AddMonths(-1);
        SetSelectedMonth(d.Year, d.Month);
    }

    private void NextMonth()
    {
        if (IsChangingPeriod) return;
        var d = new DateOnly(Year, Month, 1).AddMonths(1);
        SetSelectedMonth(d.Year, d.Month);
    }

    private void GoToCurrentMonth()
    {
        var today = DateTime.Today;
        SetSelectedMonth(today.Year, today.Month);
    }

    private void SetSelectedMonth(int year, int month)
    {
        if (IsChangingPeriod) return;
        if (Year == year && Month == month) return;

        Year = year;
        Month = month;
        var today = DateOnly.FromDateTime(DateTime.Today);
        _lastNewTransactionDate = year == today.Year && month == today.Month
            ? today
            : new DateOnly(year, month, 1);
        if (_newTransactionDraft is not null)
        {
            _newTransactionDraft.Date = _lastNewTransactionDate.ToDateTime(TimeOnly.MinValue);
        }

        RefreshSelectedMonth();
    }

    private void RefreshSelectedMonth()
    {
        OnPropertyChanged(nameof(MonthLabel));
        OnPropertyChanged(nameof(IsCurrentMonth));
        CommandManager.InvalidateRequerySuggested();
        // Navigation must start its own load even if a prior LoadCommand is busy.
        _ = LoadAsync();
    }

    private async Task AddAsync()
    {
        IsTemplatePickerOpen = false;
        var (categories, parties, locations, tags, defaults) = await LoadNewTransactionDataAsync();
        defaults = defaults with { Date = _lastNewTransactionDate };
        var dialogVm = new AddEditTransactionViewModel(
            _scopeFactory, _dialogService, Localization, categories, parties, locations, tags, defaults,
            draft: _newTransactionDraft);

        if (_dialogService.ShowAddEditTransaction(dialogVm) == true)
        {
            _lastNewTransactionDate = DateOnly.FromDateTime(dialogVm.Date);
            _newTransactionDraft = null;
            await LoadAsync();
        }
        else
        {
            _newTransactionDraft = dialogVm.HasDraftContent ? dialogVm.CreateDraft() : null;
        }
    }

    private async Task AddSimilarAsync(TransactionDto source)
    {
        try
        {
            IsTemplatePickerOpen = false;
            var (categories, parties, locations, tags, defaults) = await LoadNewTransactionDataAsync();
            defaults = defaults with { Date = _lastNewTransactionDate };
            var form = new AddEditTransactionViewModel(
                _scopeFactory, _dialogService, Localization, categories, parties, locations, tags, defaults,
                copyFrom: source);

            if (_dialogService.ShowAddEditTransaction(form) == true)
            {
                _lastNewTransactionDate = DateOnly.FromDateTime(form.Date);
                await LoadAsync();
            }
            else if (_newTransactionDraft is null && form.HasDraftContent)
            {
                _newTransactionDraft = form.CreateDraft();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "User operation failed.");
            _dialogService.ShowMessage(T("ErrorTitle"), T("Notifications_OperationFailed"), MessageDialogKind.Error);
        }
    }

    private async Task EditAsync()
    {
        if (SelectedTransaction is null) return;

        var (categories, parties, locations, tags) = await LoadReferenceDataAsync();
        var dialogVm = new AddEditTransactionViewModel(
            _scopeFactory, _dialogService, Localization, categories, parties, locations, tags,
            existing: SelectedTransaction);

        _dialogService.ShowAddEditTransaction(dialogVm);
        // A form may have been rejected because another app instance closed its
        // period. Refresh availability even when the user ultimately cancels.
        await LoadAsync();
    }

    private async Task DeleteAsync()
    {
        if (SelectedTransaction is null) return;

        var confirm = _dialogService.ConfirmDelete(
            T("DeleteTransactionTitle"),
            string.Format(CultureInfo.CurrentCulture, T("DeleteConfirmTemplate"), $"{SelectedTransaction.Date:dd.MM.yyyy}, {SelectedTransaction.Amount:N2}"));

        if (!confirm) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<DeleteTransaction>().ExecuteAsync(SelectedTransaction.Id);
            if (result.IsFailed)
            {
                _dialogService.ShowMessage(
                    T("Message_Warning"),
                    string.Join(Environment.NewLine, result.Errors.Select(e => T(e.Message))), MessageDialogKind.Warning);
                await LoadAsync();
                return;
            }

            _dialogService.Notify("Transaction.Deleted", T("Notifications_TransactionDeleted"));
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "User operation failed.");
            _dialogService.ShowMessage(T("ErrorTitle"), T("Notifications_OperationFailed"), MessageDialogKind.Error);
        }
    }

    private async Task<(IReadOnlyList<Domain.Entities.Category>, IReadOnlyList<Domain.Entities.Party>, IReadOnlyList<Domain.Entities.Location>, IReadOnlyList<Domain.Entities.Tag>)> LoadReferenceDataAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        // Repositories in one scope share a DbContext, so load them sequentially.
        var categories = await scope.ServiceProvider.GetRequiredService<ICategoryRepository>().GetAllAsync();
        var parties = await scope.ServiceProvider.GetRequiredService<IPartyRepository>().GetAllAsync();
        var locations = await scope.ServiceProvider.GetRequiredService<ILocationRepository>().GetAllAsync();
        var tags = await scope.ServiceProvider.GetRequiredService<ITagRepository>().GetAllAsync();
        return (categories, parties, locations, tags);
    }

    private async Task<(IReadOnlyList<Domain.Entities.Category>, IReadOnlyList<Domain.Entities.Party>, IReadOnlyList<Domain.Entities.Location>, IReadOnlyList<Domain.Entities.Tag>, NewTransactionDefaultsDto)> LoadNewTransactionDataAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var (categories, parties, locations, tags) = await LoadReferenceDataAsync();
        var defaults = await scope.ServiceProvider.GetRequiredService<GetNewTransactionDefaults>().ExecuteAsync();
        return (categories, parties, locations, tags, defaults);
    }

    protected override void OnCultureChanged()
    {
        OnPropertyChanged(nameof(MonthLabel));
        NotifyPeriodStateChanged();
    }
}
