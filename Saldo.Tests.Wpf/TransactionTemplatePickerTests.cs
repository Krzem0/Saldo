using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Enums;

namespace Saldo.Tests.Wpf;

public sealed class TransactionTemplatePickerTests
{
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
        vm.TemplateSearch = "  ZYWNOSC  ";
        Assert.Equal(id, Assert.Single(vm.FilteredTemplates).Id);
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

    private static async Task<int> Add(TransactionTemplatesTests.Harness h, string name, decimal? amount = null)
    {
        using var scope = h.Scopes.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<AddTransactionTemplate>().ExecuteAsync(new SaveTransactionTemplateCommand(
            name, TransactionType.Income, name == "Zakupy" ? "Zakupy do pracy" : name, h.Category.Id, h.Party.Id, h.Party.Id, h.Location.Id, amount, [h.Tag.Id]));
        Assert.True(result.IsSuccess);
        return result.Value.Id;
    }
}
