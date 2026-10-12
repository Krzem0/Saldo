using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Tests.Wpf;

public sealed class SaveTransactionAsTemplateTests
{
    [Theory]
    [InlineData("pl-PL", "12,5", 12.5)]
    [InlineData("en-US", "12.5", 12.5)]
    [InlineData("pl-PL", "", null)]
    public async Task SavesCurrentFieldsWithoutSavingTransactionOrClosingForm(string culture, string amount, double? expected)
    {
        using var h = new TransactionTemplatesTests.Harness(culture);
        var vm = Form(h);
        vm.AmountText = amount;
        vm.IsIncome = true;
        vm.Date = new DateTime(2018, 5, 6);
        vm.SelectedPayer = h.Party;
        vm.CounterpartyText = h.Party.Name;
        vm.LocationText = h.Location.Name;
        vm.Tags[0].IsSelected = true;
        var closed = false;
        vm.RequestClose += _ => closed = true;
        h.Dialogs.NameResult = "  Zakupy w Społem  ";
        var draft = vm.CreateDraft();
        await vm.SaveAsTemplateAsync();
        var saved = Assert.Single(await h.List());
        Assert.Equal("Zakupy w Społem", saved.Name);
        Assert.Equal(vm.SelectedType.Value, saved.TransactionType);
        Assert.Equal("Zakupy do pracy", saved.Description);
        Assert.Equal(expected.HasValue ? (decimal?)expected.Value : null, saved.Amount);
        Assert.Equal(h.Category.Id, saved.CategoryId);
        Assert.Equal(h.Party.Id, saved.PayerId);
        Assert.Equal(h.Party.Id, saved.CounterpartyId);
        Assert.Equal(h.Location.Id, saved.LocationId);
        Assert.Equal(h.Tag.Id, Assert.Single(saved.TagIds));
        Assert.False(closed);
        Assert.True(vm.HasTemplateSaveMessage);
        Assert.Equal(draft.Date, vm.Date);
        Assert.Equal(draft.AmountText, vm.AmountText);
        Assert.Equal(draft.Description, vm.Description);
        using var scope = h.Scopes.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ITransactionRepository>().GetByMonthAsync(2018, 5));
    }

    [Fact]
    public async Task CancellationAndDuplicateNamesDoNotCreateExtraTemplates()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var vm = Form(h);
        await vm.SaveAsTemplateAsync();
        Assert.Empty(await h.List());
        Assert.False(vm.HasTemplateSaveMessage);
        h.Dialogs.NameResult = "Zakupy";
        await vm.SaveAsTemplateAsync();
        h.Dialogs.NameResult = "zakupy";
        await vm.SaveAsTemplateAsync();
        Assert.Single(await h.List());
        Assert.Equal(h.Localization["Template_NameDuplicate"], vm.ValidationSummary);
        Assert.False(vm.HasTemplateSaveMessage);
        h.Dialogs.NameResult = "Inne zakupy";
        vm.AmountText = "10";
        await vm.SaveAsTemplateAsync();
        Assert.Equal(2, (await h.List()).Count);
        Assert.Empty(vm.ValidationSummary);
    }

    [Fact]
    public async Task InvalidAmountFieldsAndPartialReferencesAreRejected()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var vm = Form(h);
        h.Dialogs.NameResult = "Zakupy";
        vm.AmountText = "abc";
        await vm.SaveAsTemplateAsync();
        Assert.True(vm.HasAmountError);
        vm.AmountText = "0";
        await vm.SaveAsTemplateAsync();
        Assert.Equal(h.Localization["Validation_AmountMustBePositive"], vm.AmountError);
        vm.AmountText = "";
        vm.CounterpartyText = "Spo";
        await vm.SaveAsTemplateAsync();
        Assert.Equal(h.Localization["Validation_ReferenceSelectionRequired"], vm.CounterpartyError);
        vm.CounterpartyText = "";
        vm.SelectedCategory = null;
        vm.Description = "";
        await vm.SaveAsTemplateAsync();
        Assert.False(vm.HasCategoryError);
        Assert.False(vm.HasDescriptionError);
        Assert.Equal(h.Localization["Template_ContentRequired"], vm.ValidationSummary);
        Assert.Empty(await h.List());
    }

    private static AddEditTransactionViewModel Form(TransactionTemplatesTests.Harness h) => new(
        h.Scopes, h.Dialogs, h.Localization, [h.Category], [h.Party], [h.Location], [h.Tag])
        { SelectedCategory = h.Category, Description = "Zakupy do pracy" };
}
