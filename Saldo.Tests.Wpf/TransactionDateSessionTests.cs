using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Entities;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Wpf;

public sealed class TransactionDateSessionTests
{
    [Fact]
    public async Task NewForms_KeepSuccessfulDateWhileCancellationFailedSaveAndEditDoNotChangeIt()
    {
        var transactions = new FakeTransactionRepository();
        var parties = new FakePartyRepository();
        var locations = new FakeLocationRepository();
        using var provider = CreateProvider(transactions, parties, locations);
        var dialogs = new Dialogs();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var vm = new TransactionListViewModel(scopeFactory, dialogs, new LocalizationService());
        var historicalDate = new DateTime(2019, 5, 20);
        dialogs.Show = form =>
        {
            Assert.Equal(DateTime.Today, form.Date);
            form.Date = historicalDate;
            form.AmountText = "10";
            form.SelectedCategory = form.Categories[0];
            form.Description = "Historical transaction";
            bool? result = null;
            form.RequestClose += closed => result = closed;
            form.SaveCommand.Execute(null);
            Assert.True(result);
            return result;
        };
        vm.AddCommand.Execute(null);
        Assert.Equal(historicalDate, (await transactions.GetByIdAsync(1))!.Date.ToDateTime(TimeOnly.MinValue));

        dialogs.Show = form =>
        {
            Assert.Equal(historicalDate, form.Date);
            form.Date = historicalDate.AddDays(1);
            form.AmountText = "10";
            form.SelectedCategory = form.Categories[0];
            form.SaveCommand.Execute(null);
            Assert.True(form.HasDescriptionError);
            form.ClearCommand.Execute(null);
            Assert.Equal(historicalDate.AddDays(1), form.Date);
            Assert.False(form.HasDraftContent);
            return false;
        };
        vm.AddCommand.Execute(null);
        Assert.Null(await transactions.GetByIdAsync(2));

        dialogs.Show = form =>
        {
            Assert.Equal(historicalDate, form.Date);
            return false;
        };
        vm.AddCommand.Execute(null);
        vm.SelectedTransaction = new Saldo.Application.DTOs.TransactionDto(1,
            DateOnly.FromDateTime(historicalDate), Saldo.Domain.Enums.TransactionType.Expense, 10m,
            1, "Food", null, null, null, null, null, null, null, null, [], []);
        dialogs.Show = form =>
        {
            Assert.False(form.IsNewTransaction);
            form.Date = historicalDate.AddMonths(1);
            form.Description = "Edited transaction";
            bool? result = null;
            form.RequestClose += closed => result = closed;
            form.SaveCommand.Execute(null);
            Assert.True(result);
            return result;
        };
        vm.EditCommand.Execute(null);
        dialogs.Show = form => { Assert.Equal(historicalDate, form.Date); return false; };
        vm.AddCommand.Execute(null);

        var restarted = new TransactionListViewModel(scopeFactory, dialogs, new LocalizationService());
        dialogs.Show = form => { Assert.Equal(DateTime.Today, form.Date); return false; };
        restarted.AddCommand.Execute(null);
    }

    [Fact]
    public void ChangingPeriod_UpdatesNewFormDateThroughPickerArrowsAndCurrentMonth()
    {
        using var provider = CreateProvider();
        var dialogs = new Dialogs();
        var vm = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), dialogs, new LocalizationService());

        void AssertDate(DateTime expected)
        {
            var shown = false;
            dialogs.Show = form => { shown = true; Assert.Equal(expected, form.Date); return false; };
            vm.AddCommand.Execute(null);
            Assert.True(shown);
        }

        AssertDate(DateTime.Today);
        vm.OpenMonthPickerCommand.Execute(null);
        vm.PickerYearText = "2019";
        vm.SelectMonthCommand.Execute(12);
        AssertDate(new DateTime(2019, 12, 1));
        vm.NextMonthCommand.Execute(null);
        AssertDate(new DateTime(2020, 1, 1));
        vm.PreviousMonthCommand.Execute(null);
        AssertDate(new DateTime(2019, 12, 1));
        vm.OpenMonthPickerCommand.Execute(null);
        vm.PickerYearText = "2018";
        vm.SelectMonthCommand.Execute(12);
        AssertDate(new DateTime(2018, 12, 1));
        vm.CurrentMonthCommand.Execute(null);
        AssertDate(DateTime.Today);
    }

    [Fact]
    public async Task ChangingPeriod_UpdatesDraftDateAndKeepsSavedDateUntilPeriodChanges()
    {
        var transactions = new FakeTransactionRepository();
        using var provider = CreateProvider(transactions);
        var dialogs = new Dialogs();
        var vm = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), dialogs, new LocalizationService());
        dialogs.Show = form =>
        {
            form.Date = new DateTime(2019, 5, 20);
            form.AmountText = "10";
            form.SelectedCategory = form.Categories[0];
            form.Description = "Unfinished transaction";
            return false;
        };
        vm.AddCommand.Execute(null);
        vm.OpenMonthPickerCommand.Execute(null);
        vm.PickerYearText = "2019";
        vm.SelectMonthCommand.Execute(9);

        dialogs.Show = form =>
        {
            Assert.Equal(new DateTime(2019, 9, 1), form.Date);
            Assert.Equal("10", form.AmountText);
            Assert.Equal(1, form.SelectedCategory!.Id);
            Assert.Equal("Unfinished transaction", form.Description);
            form.Date = new DateTime(2019, 9, 12);
            bool? result = null;
            form.RequestClose += closed => result = closed;
            form.SaveCommand.Execute(null);
            Assert.True(result);
            return result;
        };
        vm.AddCommand.Execute(null);
        Assert.Equal(new DateOnly(2019, 9, 12), (await transactions.GetByIdAsync(1))!.Date);

        var shown = 0;
        dialogs.Show = form => { shown++; Assert.Equal(new DateTime(2019, 9, 12), form.Date); return false; };
        vm.AddCommand.Execute(null);
        vm.LoadCommand.Execute(null);
        vm.OpenMonthPickerCommand.Execute(null);
        vm.SelectMonthCommand.Execute(9);
        vm.AddCommand.Execute(null);
        Assert.Equal(2, shown);

        vm.NextMonthCommand.Execute(null);
        dialogs.Show = form => { shown++; Assert.Equal(new DateTime(2019, 10, 1), form.Date); return false; };
        vm.AddCommand.Execute(null);
        Assert.Equal(3, shown);
    }

    private static ServiceProvider CreateProvider(FakeTransactionRepository? transactions = null,
        FakePartyRepository? parties = null, FakeLocationRepository? locations = null)
    {
        transactions ??= new FakeTransactionRepository();
        parties ??= new FakePartyRepository();
        locations ??= new FakeLocationRepository();
        return new ServiceCollection()
            .AddSingleton<ITransactionRepository>(transactions)
            .AddSingleton<IPartyRepository>(parties)
            .AddSingleton<ILocationRepository>(locations)
            .AddSingleton<ICategoryRepository>(new Categories())
            .AddSingleton<ITagRepository>(new Tags())
            .AddSingleton<ITransactionSettingsRepository>(new Settings())
            .AddSingleton(new AddTransaction(transactions, parties, locations))
            .AddSingleton(new EditTransaction(transactions, parties, locations))
            .AddScoped<GetNewTransactionDefaults>().AddScoped<ListTransactions>().AddScoped<GetSummary>()
            .BuildServiceProvider();
    }

    private sealed class Settings : ITransactionSettingsRepository
    {
        public Task<TransactionSettings> GetAsync(CancellationToken ct = default) => Task.FromResult(new TransactionSettings());
        public Task SaveAsync(TransactionSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Categories : ICategoryRepository
    {
        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Category>>([new() { Id = 1, Name = "Food" }]);
        public Task<Category?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Category category, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Category category, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Tags : ITagRepository
    {
        public Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tag>>([]);
        public Task<Tag?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Tag tag, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Tag tag, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Dialogs : IDialogService
    {
        public Func<AddEditTransactionViewModel, bool?> Show { get; set; } = null!;
        public bool? ShowAddEditTransaction(AddEditTransactionViewModel viewModel) => Show(viewModel);
        public string? ShowNameDialog(string title, string? initialValue = null) => throw new NotSupportedException();
        public ReferenceColorDialogResult? ShowReferenceColorDialog(string title, string? initialName = null, string? initialColorCode = null, bool allowIcons = false, string? initialIconKey = null) => throw new NotSupportedException();
        public bool ConfirmDelete(string title, string message) => throw new NotSupportedException();
        public UnsavedChangesChoice ConfirmUnsavedChanges(string title, string message) => throw new NotSupportedException();
        public string? ShowBackupSaveDialog(string title, string suggestedFileName, string filter) => throw new NotSupportedException();
    }
}
