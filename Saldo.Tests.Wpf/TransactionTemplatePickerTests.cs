using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Enums;

namespace Saldo.Tests.Wpf;

public sealed class TransactionTemplatePickerTests
{
    [Fact]
    public async Task Picker_LoadsCategoryColorsAndLeavesTemplatesWithoutCategoryUncolored()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        await Add(h, "Z kategorią");
        var uncolored = h.Form();
        uncolored.Name = "Bez kategorii";
        uncolored.AmountText = "25";
        await uncolored.SaveAsync();
        var vm = new TransactionListViewModel(h.Scopes, h.Dialogs, h.Localization);

        await vm.OpenTemplatePickerAsync();

        Assert.Equal(2, vm.FilteredTemplates.Count);
        Assert.Equal(h.Category.ColorCode, vm.FilteredTemplates.Single(t => t.Name == "Z kategorią").CategoryColorCode);
        Assert.Null(vm.FilteredTemplates.Single(t => t.Name == "Bez kategorii").CategoryColorCode);
    }

    [Fact]
    public async Task Picker_SearchesNamesIgnoringCaseAndAccents_AndReloadsOnOpening()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var id = await Add(h, "Żywność do pracy");
        await Add(h, "Rata kredytu");
        var vm = new TransactionListViewModel(h.Scopes, h.Dialogs, h.Localization);
        await vm.OpenTemplatePickerAsync();
        Assert.True(vm.IsTemplatePickerOpen);
        Assert.Equal(2, vm.FilteredTemplates.Count);
        Assert.Null(vm.SelectedTemplate);
        Assert.False(vm.UseTemplateCommand.CanExecute(null));
        vm.SelectedTemplate = vm.FilteredTemplates[0];
        Assert.True(vm.UseTemplateCommand.CanExecute(null));
        vm.TemplateSearch = "  ZYWNOSC  ";
        Assert.Equal(id, Assert.Single(vm.FilteredTemplates).Id);
        Assert.Null(vm.SelectedTemplate);
        Assert.False(vm.UseTemplateCommand.CanExecute(null));
        vm.TemplateSearch = "nie ma";
        Assert.Empty(vm.FilteredTemplates);
        Assert.Null(vm.SelectedTemplate);
        Assert.Equal(h.Localization["TemplatePicker_NoMatches"], vm.TemplatePickerStatus);
        await vm.OpenTemplatePickerAsync();
        using (var scope = h.Scopes.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DeleteTransactionTemplate>().ExecuteAsync(id);
        await vm.OpenTemplatePickerAsync();
        Assert.Empty(vm.TemplateSearch);
        Assert.Equal("Rata kredytu", Assert.Single(vm.FilteredTemplates).Name);
        Assert.Null(vm.SelectedTemplate);
    }

    [Theory]
    [InlineData("pl-PL", "12,5")]
    [InlineData("en-US", "12.5")]
    public async Task ChoosingTemplate_PopulatesNewFormWithCurrentReferencesAndTags_AndKeepsDate(string culture, string amount)
    {
        using var h = new TransactionTemplatesTests.Harness(culture);
        var id = await Add(h, "Zakupy", 12.5m);
        AddEditTransactionViewModel? shown = null;
        h.Dialogs.TransactionCallback = form => { shown = form; return false; };
        var vm = new TransactionListViewModel(h.Scopes, h.Dialogs, h.Localization);
        await vm.AddFromTemplateAsync(id);
        Assert.NotNull(shown);
        Assert.True(shown.IsNewTransaction);
        Assert.False(shown.IsRestoredDraft);
        Assert.False(shown.ShowRestoredDraftNotice);
        Assert.Equal(DateTime.Today, shown.Date);
        Assert.Equal(amount, shown.AmountText);
        Assert.True(shown.IsIncome);
        Assert.Equal("Zakupy do pracy", shown.Description);
        Assert.Equal(h.Category.Id, shown.SelectedCategory!.Id);
        Assert.Equal(h.Category.Name, shown.CategoryText);
        Assert.Equal(h.Party.Id, shown.SelectedPayer!.Id);
        Assert.Equal(h.Party.Name, shown.CounterpartyText);
        Assert.Equal(h.Location.Name, shown.LocationText);
        Assert.True(Assert.Single(shown.Tags).IsSelected);
        shown.ClearCommand.Execute(null);
        Assert.Empty(shown.AmountText);
        Assert.Null(shown.SelectedCategory);
        Assert.False(Assert.Single(shown.Tags).IsSelected);
    }

    [Fact]
    public async Task TemplateDoesNotOverwriteExistingDraft_AndMissingTemplateDoesNotOpenDialog()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var id = await Add(h, "Zakupy");
        var vm = new TransactionListViewModel(h.Scopes, h.Dialogs, h.Localization);
        h.Dialogs.TransactionCallback = form => { form.Description = "Niedokończona transakcja"; return false; };
        vm.AddCommand.Execute(null);
        AddEditTransactionViewModel? shown = null;
        h.Dialogs.TransactionCallback = form => { shown = form; return false; };
        await vm.AddFromTemplateAsync(id);
        Assert.NotNull(shown);
        Assert.Empty(shown.AmountText);
        Assert.Equal("Zakupy do pracy", shown.Description);
        vm.AddCommand.Execute(null);
        Assert.True(shown.IsRestoredDraft);
        Assert.Equal("Niedokończona transakcja", shown.Description);
        shown = null;
        using (var scope = h.Scopes.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DeleteTransactionTemplate>().ExecuteAsync(id);
        await vm.AddFromTemplateAsync(id);
        Assert.Null(shown);
        Assert.Equal(h.Localization["Template_NotFound"], vm.TemplatePickerStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavingFromTemplate_ClearsPreviousDraftAndKeepsSavedDate(bool draftFromTemplate)
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var id = await Add(h, "Zakupy", 12.5m);
        var vm = new TransactionListViewModel(h.Scopes, h.Dialogs, h.Localization);
        h.Dialogs.TransactionCallback = form =>
        {
            form.Description = "Poprzedni szkic";
            return false;
        };
        if (draftFromTemplate) await vm.AddFromTemplateAsync(id);
        else vm.AddCommand.Execute(null);

        var savedDate = DateTime.Today.AddDays(-2);
        h.Dialogs.TransactionCallback = form =>
        {
            Assert.False(form.IsRestoredDraft);
            form.Date = savedDate;
            bool? saved = null;
            form.RequestClose += result => saved = result;
            form.SaveCommand.Execute(null);
            Assert.True(saved);
            return saved;
        };
        await vm.AddFromTemplateAsync(id);

        var opened = false;
        h.Dialogs.TransactionCallback = form =>
        {
            opened = true;
            Assert.False(form.IsRestoredDraft);
            Assert.False(form.ShowRestoredDraftNotice);
            Assert.False(form.HasDraftContent);
            Assert.Empty(form.AmountText);
            Assert.True(string.IsNullOrEmpty(form.Description));
            Assert.Null(form.SelectedCategory);
            Assert.All(form.Tags, tag => Assert.False(tag.IsSelected));
            Assert.Equal(savedDate, form.Date);
            return false;
        };
        vm.AddCommand.Execute(null);
        Assert.True(opened);
    }

    private static async Task<int> Add(TransactionTemplatesTests.Harness h, string name, decimal? amount = null)
    {
        using var scope = h.Scopes.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<AddTransactionTemplate>().ExecuteAsync(new SaveTransactionTemplateCommand(
            name, TransactionType.Income, name == "Zakupy" ? "Zakupy do pracy" : name, h.Category.Id, h.Party.Id, h.Party.Id, h.Location.Id, amount, [h.Tag.Id]));
        Assert.True(result.IsSuccess);
        return result.Value.Id;
    }
}
