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
            .AddScoped<ListTransactions>().AddScoped<GetSummary>().AddScoped<VerifyAccountingPeriod>().BuildServiceProvider();

    private static async Task WaitForLoadAsync(TransactionListViewModel viewModel)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (viewModel.IsLoading) await Task.Delay(5, timeout.Token);
    }

    private static void Run(Func<Task> test)
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
        public int ReadThread { get; private set; }
        public TaskCompletionSource ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<AccountingPeriod?> GetByMonthAsync(int year, int month, CancellationToken ct = default) => inner.GetByMonthAsync(year, month, ct);
        public Task SetClosedAsync(CalendarMonth month, bool isClosed, CancellationToken ct = default) => inner.SetClosedAsync(month, isClosed, ct);
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
        public void ShowMessage(string title, string message, MessageDialogKind kind) => Items.Add((title, message, kind));
        public bool? ShowAddEditTransaction(AddEditTransactionViewModel viewModel) => throw new NotSupportedException();
        public string? ShowNameDialog(string title, string? initialValue = null) => throw new NotSupportedException();
        public ReferenceColorDialogResult? ShowReferenceColorDialog(string title, string? initialName = null, string? initialColorCode = null,
            bool allowIcons = false, string? initialIconKey = null) => throw new NotSupportedException();
        public bool ConfirmDelete(string title, string message) => throw new NotSupportedException();
        public UnsavedChangesChoice ConfirmUnsavedChanges(string title, string message) => throw new NotSupportedException();
        public string? ShowBackupSaveDialog(string title, string suggestedFileName, string filter) => throw new NotSupportedException();
    }
}
