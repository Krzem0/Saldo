using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Infrastructure.Sqlite.Persistence;
using Saldo.Infrastructure.Sqlite.Repositories;

namespace Saldo.Tests.Wpf;

public sealed class TransactionTemplatesTests
{
    [Fact]
    public async Task TypedReferences_ResolveWholeNames_RejectUnselectedPartialNamesWithoutCreatingEntries()
    {
        using var harness = new Harness("pl-PL");
        var form = harness.Form();
        form.Name = "Shopping"; form.Description = "Groceries";
        form.CategoryText = "jedzenie do pracy"; form.CounterpartyText = "Spo";
        await form.SaveAsync();
        Assert.Equal(harness.Localization["Validation_ReferenceSelectionRequired"], form.CounterpartyError);
        Assert.Empty(await harness.List());
        form.CounterpartyText = "społem"; form.LocationText = "olsztyn";
        Assert.Empty(form.CounterpartyError);
        await form.SaveAsync();
        var saved = Assert.Single(await harness.List());
        Assert.Equal(harness.Category.Id, saved.CategoryId);
        Assert.Equal(harness.Party.Id, saved.CounterpartyId);
        Assert.Equal(harness.Location.Id, saved.LocationId);
    }

    [Fact]
    public async Task QuickAdd_SelectsNewReferencesAndPreservesColorsIconsAndTags()
    {
        using var harness = new Harness("pl-PL");
        var form = harness.Form();
        harness.Dialogs.ReferenceResult = new("Transport", "#3366CC", "mdi:Car");
        form.AddCategoryCommand.Execute(null);
        await WaitFor(form, form.AddCategoryCommand);
        Assert.Equal("Transport", form.SelectedCategory!.Name);
        Assert.Equal("#3366CC", form.SelectedCategory.ColorCode);
        Assert.Equal("mdi:Car", form.SelectedCategory.IconKey);
        Assert.Equal("Transport", form.CategoryText);
        harness.Dialogs.NameResult = "Marcin";
        form.AddPayerCommand.Execute(null);
        await WaitFor(form, form.AddPayerCommand);
        Assert.Equal("Marcin", form.SelectedPayer!.Name);
        harness.Dialogs.NameResult = "Shop";
        form.AddCounterpartyCommand.Execute(null);
        await WaitFor(form, form.AddCounterpartyCommand);
        Assert.Equal("Shop", form.SelectedCounterparty!.Name);
        harness.Dialogs.NameResult = "Warszawa";
        form.AddLocationCommand.Execute(null);
        await WaitFor(form, form.AddLocationCommand);
        Assert.Equal("Warszawa", form.LocationText);
        form.Tags[0].IsSelected = true;
        harness.Dialogs.ReferenceResult = new("Holiday", "#123456", "mdi:Airplane");
        form.AddTagCommand.Execute(null);
        await WaitFor(form, form.AddTagCommand);
        Assert.Equal(2, form.Tags.Count(t => t.IsSelected));
        Assert.Equal("mdi:Airplane", form.Tags.Last().IconKey);
        Assert.Equal("#123456", form.Tags.Last().ColorCode);
        form.Name = "Travel"; form.Description = "Tickets";
        await form.SaveAsync();
        Assert.Equal(2, Assert.Single(await harness.List()).TagIds.Count);
    }

    private static async Task WaitFor(TransactionTemplateFormViewModel form, System.Windows.Input.ICommand command)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (form.IsBusy || !command.CanExecute(null)) await Task.Delay(10, timeout.Token);
        Assert.Empty(form.SummaryError);
    }

    [Theory]
    [InlineData("pl-PL", "123,45")]
    [InlineData("en-US", "123.45")]
    public async Task Form_AddEdit_ClearAmountReferencesAndTags_PersistsThroughCore(string culture, string amount)
    {
        using var harness = new Harness(culture);
        var form = harness.Form();
        form.Name = " Work groceries "; form.Description = " Shopping ";
        form.SelectedCategory = harness.Category;
        form.SelectedPayer = form.SelectedCounterparty = harness.Party;
        form.SelectedLocation = harness.Location;
        form.AmountText = amount;
        form.Tags[0].IsSelected = true;
        var closed = false;
        form.RequestClose += result => closed = result;
        await form.SaveAsync();
        Assert.True(closed);
        var row = Assert.Single(await harness.List());
        Assert.Equal("Work groceries", row.Name);
        Assert.Equal(123.45m, row.Amount);
        Assert.Single(row.TagIds);
        var edit = harness.Form(row);
        Assert.False(edit.HasUnsavedChanges);
        Assert.Equal(harness.Party.Id, edit.SelectedPayer!.Id);
        Assert.True(edit.Tags[0].IsSelected);
        edit.Name = "Updated";
        edit.AmountText = "";
        edit.ClearPayerCommand.Execute(null);
        edit.ClearCounterpartyCommand.Execute(null);
        edit.ClearLocationCommand.Execute(null);
        edit.Tags[0].IsSelected = false;
        await edit.SaveAsync();
        row = Assert.Single(await harness.List());
        Assert.Equal("Updated", row.Name);
        Assert.Null(row.Amount); Assert.Null(row.PayerId); Assert.Null(row.CounterpartyId); Assert.Null(row.LocationId);
        Assert.Empty(row.TagIds);
    }

    [Fact]
    public async Task Form_InvalidInputAndDuplicateName_StayOpenWithLocalizedErrors()
    {
        using var harness = new Harness("pl-PL");
        var form = harness.Form();
        var closed = false;
        form.RequestClose += _ => closed = true;
        await form.SaveAsync();
        Assert.NotEmpty(form.NameError); Assert.Empty(form.DescriptionError); Assert.Empty(form.CategoryError);
        Assert.Equal(harness.Localization["Template_ContentRequired"], form.SummaryError);
        Assert.False(closed); Assert.Empty(await harness.List());
        form.Name = "Shopping"; form.Description = "Groceries"; form.SelectedCategory = harness.Category;
        form.AmountText = "abc";
        await form.SaveAsync();
        Assert.Equal(harness.Localization["Validation_AmountMustBeNumber"], form.AmountError);
        form.AmountText = "0";
        await form.SaveAsync();
        Assert.Equal(harness.Localization["Validation_AmountMustBePositive"], form.AmountError);
        form.AmountText = "";
        await form.SaveAsync();
        var duplicate = harness.Form();
        duplicate.Name = " shopping "; duplicate.Description = "Groceries"; duplicate.SelectedCategory = harness.Category;
        await duplicate.SaveAsync();
        Assert.Equal(harness.Localization["Template_NameDuplicate"], duplicate.NameError);
        Assert.Single(await harness.List());
    }

    [Fact]
    public async Task List_ReloadRestoresSelectionAndDeleteRequiresConfirmation()
    {
        using var harness = new Harness("pl-PL");
        foreach (var name in new[] { "A", "B" })
        {
            var form = harness.Form(); form.Name = name; form.Description = "Shopping " + name; form.SelectedCategory = harness.Category;
            await form.SaveAsync();
        }
        var page = new TransactionTemplatesViewModel(harness.Scopes, harness.Dialogs, harness.Localization);
        await page.LoadAsync();
        page.SelectedItem = page.Items[1];
        var id = page.SelectedItem.Template.Id;
        await page.LoadAsync();
        Assert.Equal(id, page.SelectedItem!.Template.Id);
        Assert.Contains(page.SelectedItem, page.Items);
        harness.Dialogs.DeleteConfirmed = false;
        await page.DeleteAsync();
        Assert.Equal(2, page.Items.Count);
        harness.Dialogs.DeleteConfirmed = true;
        await page.DeleteAsync();
        Assert.Single(page.Items); Assert.NotEqual(id, page.SelectedItem!.Template.Id);
        await page.DeleteAsync();
        Assert.Empty(page.Items); Assert.Null(page.SelectedItem);
        Assert.False(page.EditCommand.CanExecute(null)); Assert.False(page.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public void Form_CloseChecksTagsAndOptionalFieldsWithoutSavingOnDiscard()
    {
        using var harness = new Harness("pl-PL");
        var form = harness.Form();
        Assert.True(form.CanClose());
        form.Tags[0].IsSelected = true;
        Assert.True(form.HasUnsavedChanges);
        harness.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;
        Assert.False(form.CanClose());
        harness.Dialogs.UnsavedChoice = UnsavedChangesChoice.Discard;
        Assert.True(form.CanClose());
        form.Tags[0].IsSelected = false;
        Assert.False(form.HasUnsavedChanges);
    }

    internal sealed class Harness : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly ServiceProvider _services;
        public LocalizationService Localization { get; }
        public TestDialogs Dialogs { get; } = new();
        public IServiceScopeFactory Scopes => _services.GetRequiredService<IServiceScopeFactory>();
        public Category Category { get; } = new() { Name = "Jedzenie do pracy", ColorCode = "#3366CC", IconKey = "mdi:Food" };
        public Party Party { get; } = new() { Name = "Społem" };
        public Location Location { get; } = new() { Name = "Olsztyn" };
        public Tag Tag { get; } = new() { Name = "Do pracy", ColorCode = "#3366CC", IconKey = "mdi:Food" };
        public Harness(string culture)
        {
            Localization = new LocalizationService { CurrentCulture = new CultureInfo(culture) };
            _connection.Open();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<SaldoDbContext>(options => options.UseSqlite(_connection));
            services.AddScoped<ITransactionTemplateRepository, TransactionTemplateRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IPartyRepository, PartyRepository>();
            services.AddScoped<ILocationRepository, LocationRepository>();
            services.AddScoped<ITagRepository, TagRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
            services.AddScoped<ITransactionSettingsRepository, TransactionSettingsRepository>();
            services.AddScoped<GetNewTransactionDefaults>();
            services.AddScoped<ListTransactions>(); services.AddScoped<GetSummary>();
            services.AddScoped<VerifyAccountingPeriod>();
            services.AddScoped<AddTransaction>(); services.AddScoped<EditTransaction>();
            services.AddScoped<TransactionTemplateInput>(); services.AddScoped<AddTransactionTemplate>();
            services.AddScoped<EditTransactionTemplate>(); services.AddScoped<GetTransactionTemplate>();
            services.AddScoped<ListTransactionTemplates>(); services.AddScoped<DeleteTransactionTemplate>();
            services.AddScoped<AddCategory>(); services.AddScoped<AddParty>(); services.AddScoped<AddLocation>(); services.AddScoped<AddTag>();
            _services = services.BuildServiceProvider();
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SaldoDbContext>();
            db.Database.EnsureCreated(); db.AddRange(Category, Party, Location, Tag); db.SaveChanges();
        }
        public TransactionTemplateFormViewModel Form(TransactionTemplateDto? existing = null) => new(
            Scopes, Dialogs, Localization, [Category], [Party], [Location], [Tag], existing);
        public async Task<IReadOnlyList<TransactionTemplateDto>> List()
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ListTransactionTemplates>().ExecuteAsync();
        }
        public void Dispose() { _services.Dispose(); _connection.Dispose(); }
    }
    internal sealed class TestDialogs : IDialogService
    {
        public string? NameResult { get; set; }
        public string? NameInitialValue { get; private set; }
        public ReferenceColorDialogResult? ReferenceResult { get; set; }
        public bool DeleteConfirmed { get; set; }
        public UnsavedChangesChoice UnsavedChoice { get; set; } = UnsavedChangesChoice.Cancel;
        public Func<AddEditTransactionViewModel, bool?>? TransactionCallback { get; set; }
        public string? BackupPath { get; set; }
        public List<(string Title, string Message, MessageDialogKind? Kind)> Messages { get; } = [];
        public void ShowMessage(string title, string message) => Messages.Add((title, message, null));
        public void ShowMessage(string title, string message, MessageDialogKind kind) => Messages.Add((title, message, kind));
        public bool? ShowAddEditTransaction(AddEditTransactionViewModel viewModel) => TransactionCallback?.Invoke(viewModel);
        public string? ShowNameDialog(string title, string? initialValue = null)
        {
            NameInitialValue = initialValue;
            return NameResult;
        }
        public ReferenceColorDialogResult? ShowReferenceColorDialog(string title, string? initialName = null,
            string? initialColorCode = null, bool allowIcons = false, string? initialIconKey = null) => ReferenceResult;
        public bool ConfirmDelete(string title, string message) => DeleteConfirmed;
        public UnsavedChangesChoice ConfirmUnsavedChanges(string title, string message) => UnsavedChoice;
        public string? ShowBackupSaveDialog(string title, string suggestedFileName, string filter) => BackupPath;
    }
}
