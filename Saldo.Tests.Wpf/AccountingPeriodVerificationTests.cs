using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Domain.ValueObjects;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Wpf;

public sealed class AccountingPeriodVerificationTests
{
    [Theory]
    [InlineData("pl-PL", "mismatch", "AccountingPeriod_VerificationMismatch")]
    [InlineData("en-US", "mismatch", "AccountingPeriod_VerificationMismatch")]
    [InlineData("pl-PL", "missing", "AccountingPeriod_VerificationMissing")]
    [InlineData("en-US", "unsupported", "AccountingPeriod_VerificationUnsupported")]
    [InlineData("en-US", "failure", "AccountingPeriod.VerificationFailed")]
    public void MonthLoad_ShowsLocalizedIntegrityWarningOrReadError(string culture, string scenario, string messageKey) => Run(async () =>
    {
        var transactions = new FakeTransactionRepository();
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Today), Type = TransactionType.Expense,
            Amount = 10m, CategoryId = 1, Description = "Original"
        };
        await transactions.AddAsync(transaction);
        var periods = new FakeAccountingPeriodRepository();
        periods.Transactions.Add(transaction);
        await new CloseAccountingPeriod(periods).ExecuteAsync(transaction.Date.Year, transaction.Date.Month);
        var period = (await periods.GetByMonthAsync(transaction.Date.Year, transaction.Date.Month))!;
        if (scenario == "mismatch") transaction.Description = "External change";
        if (scenario == "missing") period.ClosedDataHash = null;
        if (scenario == "unsupported") period.HashVersion = 999;
        var repository = new ControlledReadRepository(periods) { FailRead = scenario == "failure" };
        var dialogs = new Messages();
        var localization = new LocalizationService { CurrentCulture = CultureInfo.GetCultureInfo(culture) };
        using var provider = Provider(transactions, repository);
        var viewModel = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), dialogs, localization);

        viewModel.LoadCommand.Execute(null);
        await WaitForLoadAsync(viewModel);

        var message = Assert.Single(dialogs.Items);
        Assert.Equal(localization["AccountingPeriod_VerificationTitle"], message.Title);
        Assert.StartsWith(localization[messageKey], message.Message);
        Assert.Equal(scenario == "failure" ? MessageDialogKind.Error : MessageDialogKind.Warning, message.Kind);
        if (scenario == "mismatch")
            Assert.Contains(localization["AccountingPeriod_Difference_DataHash"], message.Message);
        Assert.Single(viewModel.Transactions);
        Assert.Equal(scenario == "failure" ? null : true, viewModel.IsPeriodClosed);
        Assert.False(viewModel.EditCommand.CanExecute(null));
        Assert.False(viewModel.DeleteCommand.CanExecute(null));
        Assert.Equal(scenario != "failure", viewModel.ChangePeriodCommand.CanExecute(null));
        Assert.NotEmpty(viewModel.PeriodIntegrityText);
        Assert.True(viewModel.HasPeriodIssue);
    });

    [Theory]
    [InlineData("pl-PL")]
    [InlineData("en-US")]
    public void ConfirmedCloseAndReopen_UpdateStateIntegrityAndCommands(string culture) => Run(async () =>
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var transactions = new FakeTransactionRepository();
        var transaction = new Transaction { Date = today, Type = TransactionType.Expense, Amount = 10m, CategoryId = 1, Description = "Original" };
        await transactions.AddAsync(transaction);
        var periods = new FakeAccountingPeriodRepository();
        periods.Transactions.Add(transaction);
        var dialogs = new Messages { ConfirmChange = true };
        var localization = new LocalizationService { CurrentCulture = CultureInfo.GetCultureInfo(culture) };
        var backupService = new AutomaticBackupTests.Backups();
        var automaticBackups = new AutomaticBackupViewModel(backupService, localization);
        await automaticBackups.InitializeAsync();
        using var provider = Provider(transactions, periods);
        var viewModel = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), dialogs, localization, automaticBackups);
        Assert.Null(viewModel.IsPeriodClosed);
        Assert.False(viewModel.ChangePeriodCommand.CanExecute(null));
        viewModel.LoadCommand.Execute(null);
        await WaitForLoadAsync(viewModel);
        viewModel.SelectedTransaction = Assert.Single(viewModel.Transactions);
        Assert.True(viewModel.EditCommand.CanExecute(null));
        Assert.True(viewModel.DeleteCommand.CanExecute(null));

        viewModel.ChangePeriodCommand.Execute(null);
        await WaitForIdleAsync(viewModel);

        Assert.True(viewModel.IsPeriodClosed);
        Assert.Equal(localization["AccountingPeriod_Closed"], viewModel.PeriodStateText);
        Assert.Equal(localization["AccountingPeriod_Reopen"], viewModel.PeriodActionText);
        Assert.Empty(viewModel.PeriodIntegrityText);
        Assert.False(viewModel.HasPeriodIssue);
        Assert.False(viewModel.EditCommand.CanExecute(null));
        Assert.False(viewModel.DeleteCommand.CanExecute(null));
        Assert.True(viewModel.AddCommand.CanExecute(null));
        Assert.True(viewModel.AddSimilarCommand.CanExecute(viewModel.Transactions[0]));
        Assert.True(viewModel.OpenTemplatePickerCommand.CanExecute(null));
        var snapshot = (await periods.GetByMonthAsync(today.Year, today.Month))!.ClosedDataHash;
        Assert.Equal((today.Year, today.Month), Assert.Single(backupService.ClosedMonths));

        viewModel.ChangePeriodCommand.Execute(null);
        await WaitForIdleAsync(viewModel);

        Assert.False(viewModel.IsPeriodClosed);
        Assert.Equal(localization["AccountingPeriod_Open"], viewModel.PeriodStateText);
        Assert.Equal(localization["AccountingPeriod_Close"], viewModel.PeriodActionText);
        Assert.Empty(viewModel.PeriodIntegrityText);
        viewModel.SelectedTransaction = viewModel.Transactions[0];
        Assert.True(viewModel.EditCommand.CanExecute(null));
        Assert.True(viewModel.DeleteCommand.CanExecute(null));
        Assert.Equal(snapshot, (await periods.GetByMonthAsync(today.Year, today.Month))!.ClosedDataHash);
        Assert.Equal(2, dialogs.Confirmations.Count);
        Assert.Single(backupService.ClosedMonths); // Reopening does not request another backup.
        Assert.All(dialogs.Confirmations, confirmation => Assert.Contains(viewModel.MonthLabel, confirmation.Message));
        Assert.Empty(dialogs.Items);
    });

    [Fact]
    public void FailedAfterClosingBackup_DoesNotUndoClosedState() => Run(async () =>
    {
        var periods = new FakeAccountingPeriodRepository();
        var service = new AutomaticBackupTests.Backups { FailAfterClose = true };
        var localization = new LocalizationService();
        var automaticBackups = new AutomaticBackupViewModel(service, localization);
        await automaticBackups.InitializeAsync();
        var dialogs = new Messages { ConfirmChange = true };
        using var provider = Provider(new FakeTransactionRepository(), periods);
        var viewModel = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), dialogs, localization, automaticBackups);
        viewModel.LoadCommand.Execute(null);
        await WaitForLoadAsync(viewModel);
        viewModel.ChangePeriodCommand.Execute(null);
        await WaitForIdleAsync(viewModel);
        Assert.True(viewModel.IsPeriodClosed);
        Assert.True((await periods.GetByMonthAsync(viewModel.Year, viewModel.Month))!.IsClosed);
        Assert.True(automaticBackups.HasError);
        Assert.True(automaticBackups.CanWrite);
        Assert.Empty(dialogs.Items);
        Assert.Single(service.ClosedMonths);
    });

    [Fact]
    public void StartupGate_DisablesCopyContextCommandUntilBackupCompletes() => Run(async () =>
    {
        var service = new AutomaticBackupTests.Backups { PauseStartup = true };
        var localization = new LocalizationService();
        var backups = new AutomaticBackupViewModel(service, localization);
        var transactions = new FakeTransactionRepository();
        await transactions.AddAsync(new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Today), Amount = 10m, CategoryId = 1,
            Type = TransactionType.Expense, Description = "Original"
        });
        using var provider = Provider(transactions, new FakeAccountingPeriodRepository());
        var viewModel = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), new Messages(), localization, backups);
        viewModel.LoadCommand.Execute(null);
        await WaitForLoadAsync(viewModel);
        var row = Assert.Single(viewModel.Transactions);
        var startup = backups.InitializeAsync();
        Assert.False(viewModel.AddSimilarCommand.CanExecute(row));
        Assert.False(viewModel.OpenTemplatePickerCommand.CanExecute(null));
        Assert.False(viewModel.ChangePeriodCommand.CanExecute(null));
        service.ReleaseStartup.TrySetResult();
        await startup;
        Assert.True(viewModel.AddSimilarCommand.CanExecute(row));
        Assert.True(viewModel.OpenTemplatePickerCommand.CanExecute(null));
        Assert.True(viewModel.ChangePeriodCommand.CanExecute(null));
    });

    [Fact]
    public void ShutdownWait_HandlesCloseThatHasNotEnqueuedItsBackupYet() => Run(async () =>
    {
        var service = new AutomaticBackupTests.Backups { PauseAfterClose = true };
        var localization = new LocalizationService();
        var backups = new AutomaticBackupViewModel(service, localization);
        await backups.InitializeAsync();
        var periods = new ControlledReadRepository(new FakeAccountingPeriodRepository()) { PauseChange = true };
        using var provider = Provider(new FakeTransactionRepository(), periods);
        var viewModel = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(),
            new Messages { ConfirmChange = true }, localization, backups);
        viewModel.LoadCommand.Execute(null);
        await WaitForLoadAsync(viewModel);
        viewModel.ChangePeriodCommand.Execute(null);
        await periods.ChangeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(backups.IsBusy);
        async Task WaitForShutdownAsync()
        {
            await viewModel.WaitForPeriodChangeAsync();
            await backups.WaitForIdleAsync();
        }
        var shutdown = WaitForShutdownAsync();
        Assert.False(shutdown.IsCompleted);
        periods.ReleaseChange.TrySetResult();
        await service.AfterCloseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(shutdown.IsCompleted);
        service.ReleaseAfterClose.TrySetResult();
        await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(viewModel.IsPeriodClosed);
        Assert.False(backups.IsBusy);
        Assert.Single(service.ClosedMonths);
    });

    [Fact]
    public void CancelledClose_DoesNotCreatePeriod() => Run(async () =>
    {
        var periods = new FakeAccountingPeriodRepository();
        var repository = new ControlledReadRepository(periods);
        var dialogs = new Messages();
        using var provider = Provider(new FakeTransactionRepository(), repository);
        var viewModel = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), dialogs, new LocalizationService());
        viewModel.LoadCommand.Execute(null);
        await WaitForLoadAsync(viewModel);

        viewModel.ChangePeriodCommand.Execute(null);
        await WaitForIdleAsync(viewModel);

        Assert.Single(dialogs.Confirmations);
        Assert.Equal(0, repository.ChangeCount);
        Assert.Null(await periods.GetByMonthAsync(viewModel.Year, viewModel.Month));
        Assert.False(viewModel.IsPeriodClosed);
        Assert.True(viewModel.ChangePeriodCommand.CanExecute(null));
    });

    [Theory]
    [InlineData("new")]
    [InlineData("copy")]
    [InlineData("edit")]
    public void FormOpenedBeforeClosing_RejectsClosedDateAndChecksSourceOnMove(string kind) => Run(async () =>
    {
        using var harness = new TransactionTemplatesTests.Harness("pl-PL");
        var today = DateOnly.FromDateTime(DateTime.Today);
        using var scope = harness.Scopes.CreateScope();
        var transactions = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        await transactions.AddAsync(new Transaction
        {
            Date = today, Type = TransactionType.Expense, Amount = 10m,
            CategoryId = harness.Category.Id, Description = "Original"
        });
        var source = Assert.Single(await scope.ServiceProvider.GetRequiredService<ListTransactions>()
            .ExecuteAsync(new ListTransactionsQuery(today.Year, today.Month)));
        var form = new AddEditTransactionViewModel(harness.Scopes, harness.Dialogs, harness.Localization,
            [harness.Category], [harness.Party], [harness.Location], [harness.Tag],
            defaults: new NewTransactionDefaultsDto(today, TransactionType.Expense, null),
            existing: kind == "edit" ? source : null, copyFrom: kind == "copy" ? source : null)
        {
            Date = today.ToDateTime(TimeOnly.MinValue), SelectedCategory = harness.Category,
            AmountText = "20", Description = "Changed"
        };
        bool? saved = null;
        form.RequestClose += result => saved = result;
        await new CloseAccountingPeriod(scope.ServiceProvider.GetRequiredService<IAccountingPeriodRepository>())
            .ExecuteAsync(today.Year, today.Month);

        form.SaveCommand.Execute(null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!form.SaveCommand.CanExecute(null)) await Task.Delay(5, timeout.Token);

        Assert.Null(saved);
        Assert.Equal(harness.Localization["AccountingPeriod.Closed"], form.ValidationSummary);
        Assert.Equal("Original", (await transactions.GetByIdAsync(source.Id))!.Description);
        form.Date = today.AddMonths(1).ToDateTime(TimeOnly.MinValue);
        form.SaveCommand.Execute(null);
        while (!form.SaveCommand.CanExecute(null)) await Task.Delay(5, timeout.Token);
        if (kind == "edit")
        {
            Assert.Null(saved);
            Assert.Equal(harness.Localization["AccountingPeriod.Closed"], form.ValidationSummary);
        }
        else
        {
            Assert.True(saved);
            Assert.Single(await transactions.GetByMonthAsync(form.Date.Year, form.Date.Month));
        }
        Assert.Equal("Original", (await transactions.GetByIdAsync(source.Id))!.Description);
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Confirmation_PreservesCurrentLockStateAndDisablesAction(bool initiallyClosed, bool confirmed) => Run(async () =>
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var repository = new FakeAccountingPeriodRepository();
        if (initiallyClosed) await new CloseAccountingPeriod(repository).ExecuteAsync(today.Year, today.Month);
        TransactionListViewModel viewModel = null!;
        var confirmationShown = false;
        var dialogs = new Messages
        {
            ConfirmChange = confirmed,
            OnConfirm = () =>
            {
                confirmationShown = true;
                Assert.True(viewModel.IsChangingPeriod);
                Assert.Equal(initiallyClosed, viewModel.IsPeriodClosed);
                Assert.False(viewModel.ChangePeriodCommand.CanExecute(null));
            }
        };
        using var provider = Provider(new FakeTransactionRepository(), repository);
        viewModel = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), dialogs, new LocalizationService());
        viewModel.LoadCommand.Execute(null);
        await WaitForLoadAsync(viewModel);

        viewModel.ChangePeriodCommand.Execute(null);
        await WaitForIdleAsync(viewModel);

        Assert.True(confirmationShown);
        Assert.Empty(dialogs.Items);
        Assert.Equal(confirmed ? !initiallyClosed : initiallyClosed, viewModel.IsPeriodClosed);
        Assert.True(viewModel.ChangePeriodCommand.CanExecute(null));
    });

    [Fact]
    public void ChangingState_DisablesWritesTemplatesNavigationAndDuplicateRequests() => Run(async () =>
    {
        var repository = new ControlledReadRepository(new FakeAccountingPeriodRepository()) { PauseChange = true };
        var dialogs = new Messages { ConfirmChange = true };
        using var provider = Provider(new FakeTransactionRepository(), repository);
        var viewModel = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), dialogs, new LocalizationService());
        viewModel.LoadCommand.Execute(null);
        await WaitForLoadAsync(viewModel);
        var month = viewModel.Month;

        viewModel.ChangePeriodCommand.Execute(null);
        try
        {
            await repository.ChangeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(viewModel.IsChangingPeriod);
            Assert.False(viewModel.ChangePeriodCommand.CanExecute(null));
            Assert.False(viewModel.AddCommand.CanExecute(null));
            Assert.False(viewModel.OpenTemplatePickerCommand.CanExecute(null));
            Assert.False(viewModel.PreviousMonthCommand.CanExecute(null));
            Assert.False(viewModel.NextMonthCommand.CanExecute(null));
            viewModel.NextMonthCommand.Execute(null);
            viewModel.ChangePeriodCommand.Execute(null);
            Assert.Equal(month, viewModel.Month);
            Assert.Single(dialogs.Confirmations);
        }
        finally { repository.ReleaseChange.TrySetResult(); }
        await WaitForIdleAsync(viewModel);
        Assert.True(viewModel.IsPeriodClosed);
        Assert.Equal(1, repository.ChangeCount);
    });

    [Fact]
    public void FailedStateChange_ShowsUnknownStateUntilSuccessfulRefresh() => Run(async () =>
    {
        var repository = new ControlledReadRepository(new FakeAccountingPeriodRepository()) { FailChange = true };
        var dialogs = new Messages { ConfirmChange = true };
        var localization = new LocalizationService();
        using var provider = Provider(new FakeTransactionRepository(), repository);
        var viewModel = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), dialogs, localization);
        viewModel.LoadCommand.Execute(null);
        await WaitForLoadAsync(viewModel);

        viewModel.ChangePeriodCommand.Execute(null);
        await WaitForIdleAsync(viewModel);

        Assert.Null(viewModel.IsPeriodClosed);
        Assert.False(viewModel.ChangePeriodCommand.CanExecute(null));
        Assert.Equal(localization["AccountingPeriod_Unknown"], viewModel.PeriodStateText);
        Assert.Equal(MessageDialogKind.Error, Assert.Single(dialogs.Items).Kind);
        viewModel.LoadCommand.Execute(null);
        await WaitForLoadAsync(viewModel);
        Assert.False(viewModel.IsPeriodClosed);
        Assert.True(viewModel.ChangePeriodCommand.CanExecute(null));
    });

    [Fact]
    public void EnteringClosedMonth_DoesNotBrieflyPresentOpenStateOrCloseAction() => Run(async () =>
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var target = today.AddMonths(1);
        var periods = new FakeAccountingPeriodRepository();
        await new CloseAccountingPeriod(periods).ExecuteAsync(target.Year, target.Month);
        var repository = new ControlledReadRepository(periods) { PauseRead = true };
        var localization = new LocalizationService();
        using var provider = Provider(new FakeTransactionRepository(), repository);
        var viewModel = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), new Messages(), localization);
        viewModel.LoadCommand.Execute(null);
        await WaitForLoadAsync(viewModel);
        Assert.False(viewModel.IsPeriodClosed);
        List<(bool? Closed, string Action)> loadingStates = [];
        viewModel.PropertyChanged += (_, args) =>
        {
            if (viewModel.IsLoading && args.PropertyName == nameof(TransactionListViewModel.IsPeriodClosed))
                loadingStates.Add((viewModel.IsPeriodClosed, viewModel.PeriodActionText));
        };

        viewModel.NextMonthCommand.Execute(null);
        try
        {
            await repository.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(viewModel.IsPeriodClosed);
            Assert.Equal(localization["AccountingPeriod_Loading"], viewModel.PeriodStateText);
            Assert.Equal("…", viewModel.PeriodActionText);
            Assert.False(viewModel.ChangePeriodCommand.CanExecute(null));
            Assert.NotEmpty(loadingStates);
            Assert.All(loadingStates, state =>
            {
                Assert.Null(state.Closed);
                Assert.Equal("…", state.Action);
            });
        }
        finally { repository.ReleaseRead.TrySetResult(); }
        await WaitForLoadAsync(viewModel);
        Assert.True(viewModel.IsPeriodClosed);
        Assert.Equal(localization["AccountingPeriod_Reopen"], viewModel.PeriodActionText);
    });

    [Fact]
    public void NavigationDuringVerification_LoadsNewMonthAndIgnoresOldWarning() => Run(async () =>
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var periods = new FakeAccountingPeriodRepository();
        await new CloseAccountingPeriod(periods).ExecuteAsync(today.Year, today.Month);
        (await periods.GetByMonthAsync(today.Year, today.Month))!.ClosedDataHash = "Changed";
        var repository = new ControlledReadRepository(periods) { PauseRead = true };
        var dialogs = new Messages();
        using var provider = Provider(new FakeTransactionRepository(), repository);
        var viewModel = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), dialogs, new LocalizationService());
        viewModel.LoadCommand.Execute(null);
        try
        {
            await repository.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            viewModel.NextMonthCommand.Execute(null);
            await WaitForLoadAsync(viewModel);
            Assert.Equal(today.AddMonths(1).Month, viewModel.Month);
            Assert.False(viewModel.IsPeriodClosed);
        }
        finally { repository.ReleaseRead.TrySetResult(); }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!viewModel.LoadCommand.CanExecute(null)) await Task.Delay(5, timeout.Token);
        Assert.False(viewModel.IsPeriodClosed);
        Assert.Empty(viewModel.PeriodIntegrityText);
        Assert.Empty(dialogs.Items);
    });

    [Fact]
    public void ClosedMonthRead_RunsOffDispatcherAndLeavesItResponsive() => Run(async () =>
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var periods = new FakeAccountingPeriodRepository();
        await new CloseAccountingPeriod(periods).ExecuteAsync(today.Year, today.Month);
        var repository = new ControlledReadRepository(periods) { PauseRead = true };
        var dialogs = new Messages();
        using var provider = Provider(new FakeTransactionRepository(), repository);
        var viewModel = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), dialogs, new LocalizationService());
        var dispatcherThread = Environment.CurrentManagedThreadId;

        viewModel.LoadCommand.Execute(null);
        try
        {
            await repository.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(viewModel.IsLoading);
            Assert.NotEqual(dispatcherThread, repository.ReadThread);
            // This continuation can run on the dispatcher while the read is paused.
            await Dispatcher.Yield(DispatcherPriority.Background);
            Assert.Equal(dispatcherThread, Environment.CurrentManagedThreadId);
        }
        finally { repository.ReleaseRead.TrySetResult(); }
        await WaitForLoadAsync(viewModel);
        Assert.Empty(dialogs.Items);
    });

    private static ServiceProvider Provider(ITransactionRepository transactions, IAccountingPeriodRepository periods) =>
        new ServiceCollection().AddSingleton(transactions).AddSingleton(periods)
            .AddScoped<ListTransactions>().AddScoped<GetSummary>().AddScoped<VerifyAccountingPeriod>()
            .AddScoped<CloseAccountingPeriod>().AddScoped<ReopenAccountingPeriod>().BuildServiceProvider();

    private static async Task WaitForIdleAsync(TransactionListViewModel viewModel)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (viewModel.IsLoading || viewModel.IsChangingPeriod) await Task.Delay(5, timeout.Token);
    }

    private static async Task WaitForLoadAsync(TransactionListViewModel viewModel)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (viewModel.IsLoading) await Task.Delay(5, timeout.Token);
    }

    internal static void Run(Func<Task> test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await test(); }
                catch (Exception ex) { failure = ex; }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Dispatcher test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class ControlledReadRepository(IAccountingPeriodRepository inner) : IAccountingPeriodRepository
    {
        public bool FailRead { get; init; }
        public bool PauseRead { get; init; }
        public bool PauseChange { get; init; }
        public bool FailChange { get; init; }
        public int ChangeCount { get; private set; }
        public TaskCompletionSource ChangeEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseChange { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ReadThread { get; private set; }
        public TaskCompletionSource ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<AccountingPeriod?> GetByMonthAsync(int year, int month, CancellationToken ct = default) => inner.GetByMonthAsync(year, month, ct);
        public async Task SetClosedAsync(CalendarMonth month, bool isClosed, CancellationToken ct = default)
        {
            ChangeCount++;
            ChangeEntered.TrySetResult();
            if (PauseChange) await ReleaseChange.Task.WaitAsync(ct);
            if (FailChange) throw new IOException("Simulated change failure.");
            await inner.SetClosedAsync(month, isClosed, ct);
        }
        public async Task<AccountingPeriodVerificationData> ReadVerificationDataAsync(CalendarMonth month, CancellationToken ct = default)
        {
            ReadThread = Environment.CurrentManagedThreadId;
            ReadEntered.TrySetResult();
            if (PauseRead) await ReleaseRead.Task.WaitAsync(ct);
            if (FailRead) throw new IOException("Simulated read failure.");
            return await inner.ReadVerificationDataAsync(month, ct);
        }
    }

    private sealed class Messages : IDialogService
    {
        public List<(string Title, string Message, MessageDialogKind Kind)> Items { get; } = [];
        public List<(string Title, string Message, string Action)> Confirmations { get; } = [];
        public bool ConfirmChange { get; init; }
        public Action? OnConfirm { get; init; }
        public bool ConfirmAccountingPeriodChange(string title, string message, string actionLabel)
        {
            Confirmations.Add((title, message, actionLabel));
            OnConfirm?.Invoke();
            return ConfirmChange;
        }
        public void ShowMessage(string title, string message, MessageDialogKind kind) => Items.Add((title, message, kind));
        public bool? ShowAddEditTransaction(AddEditTransactionViewModel viewModel) => throw new NotSupportedException();
        public string? ShowNameDialog(string title, string? initialValue = null, IReadOnlyList<string>? existingNames = null) => throw new NotSupportedException();
        public ReferenceColorDialogResult? ShowReferenceColorDialog(string title, string? initialName = null, string? initialColorCode = null,
            bool allowIcons = false, string? initialIconKey = null, IReadOnlyList<string>? existingNames = null) => throw new NotSupportedException();
        public bool ConfirmDelete(string title, string message) => throw new NotSupportedException();
        public UnsavedChangesChoice ConfirmUnsavedChanges(string title, string message) => throw new NotSupportedException();
        public string? ShowBackupSaveDialog(string title, string suggestedFileName, string filter) => throw new NotSupportedException();
    }
}
