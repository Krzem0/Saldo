using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Enums;

namespace Saldo.Tests.Wpf;

public sealed class InFormTemplatePickerTests
{
    [Fact]
    public async Task PartialTemplate_PreservesTypedReferenceText_AndNonemptyTagsReplaceSelection()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var vm = new AddEditTransactionViewModel(h.Scopes, h.Dialogs, h.Localization,
            [h.Category], [h.Party], [h.Location], [h.Tag])
        {
            Description = "Ręczny opis", AmountText = "25", CategoryText = "Jedze",
            PayerText = "Mar", CounterpartyText = "Spo", LocationText = "Ols"
        };
        vm.Tags[0].IsSelected = true;
        using var scope = h.Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Saldo.Infrastructure.Sqlite.Persistence.SaldoDbContext>();
        var tag = new Saldo.Domain.Entities.Tag { Name = "Inny tag" };
        db.Add(tag);
        await db.SaveChangesAsync();
        var saved = await scope.ServiceProvider.GetRequiredService<AddTransactionTemplate>().ExecuteAsync(
            new SaveTransactionTemplateCommand("Tylko tag", TransactionType.Expense, TagIds: [tag.Id]));
        await vm.LoadTemplateAsync(saved.Value.Id);
        Assert.Equal("Ręczny opis", vm.Description);
        Assert.Equal("25", vm.AmountText);
        Assert.Equal("Jedze", vm.CategoryText); Assert.Null(vm.SelectedCategory);
        Assert.Equal("Mar", vm.PayerText); Assert.Null(vm.SelectedPayer);
        Assert.Equal("Spo", vm.CounterpartyText); Assert.Null(vm.SelectedCounterparty);
        Assert.Equal("Ols", vm.LocationText); Assert.Null(vm.SelectedLocation);
        Assert.Equal(tag.Id, Assert.Single(vm.Tags, t => t.IsSelected).Id);
    }

    [Fact]
    public async Task LoadInExistingForm_OverridesPopulatedFieldsPreservesMissingValues_AndKeepsDate()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var vm = new AddEditTransactionViewModel(h.Scopes, h.Dialogs, h.Localization,
            [h.Category], [h.Party], [h.Location], [h.Tag])
        {
            Date = new DateTime(2019, 5, 20), Description = "Old", AmountText = "100",
            SelectedCategory = h.Category, CategoryText = h.Category.Name, SelectedPayer = h.Party, PayerText = h.Party.Name,
            SelectedCounterparty = h.Party, CounterpartyText = h.Party.Name,
            SelectedLocation = h.Location, LocationText = h.Location.Name
        };
        vm.Tags[0].IsSelected = true;
        var closed = false;
        vm.RequestClose += _ => closed = true;
        using var scope = h.Scopes.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AddTransactionTemplate>().ExecuteAsync(
            new SaveTransactionTemplateCommand("Przychód", TransactionType.Income, Description: "Salary"));
        Assert.True(saved.IsSuccess);
        await vm.TemplatePicker.OpenTemplatePickerAsync();
        vm.TemplatePicker.TemplateSearch = "przychod";
        Assert.Equal(saved.Value.Id, Assert.Single(vm.TemplatePicker.FilteredTemplates).Id);
        await vm.LoadTemplateAsync(saved.Value.Id);
        Assert.Equal(new DateTime(2019, 5, 20), vm.Date);
        Assert.True(vm.IsIncome);
        Assert.Equal("Salary", vm.Description);
        Assert.Equal("100", vm.AmountText);
        Assert.Equal(h.Category.Id, vm.SelectedCategory!.Id); Assert.Equal(h.Category.Name, vm.CategoryText);
        Assert.Equal(h.Party.Id, vm.SelectedPayer!.Id); Assert.Equal(h.Party.Name, vm.PayerText);
        Assert.Equal(h.Party.Id, vm.SelectedCounterparty!.Id); Assert.Equal(h.Party.Name, vm.CounterpartyText);
        Assert.Equal(h.Location.Id, vm.SelectedLocation!.Id); Assert.Equal(h.Location.Name, vm.LocationText);
        Assert.True(Assert.Single(vm.Tags).IsSelected);
        Assert.Empty(vm.ValidationSummary);
        Assert.False(vm.TemplatePicker.IsTemplatePickerOpen);
        Assert.False(closed);
        Assert.True(vm.HasDraftContent);
    }

    [Fact]
    public async Task LoadingTemplate_UsesFreshReferences_AndDeletedTemplateLeavesFormUntouched()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var vm = new AddEditTransactionViewModel(h.Scopes, h.Dialogs, h.Localization,
            [h.Category], [h.Party], [h.Location], [h.Tag]) { AmountText = "100", Description = "Old" };
        using var scope = h.Scopes.CreateScope();
        var add = scope.ServiceProvider.GetRequiredService<AddTransactionTemplate>();
        var saved = await add.ExecuteAsync(new("Zakupy", TransactionType.Expense, CategoryId: h.Category.Id,
            CounterpartyId: h.Party.Id, LocationId: h.Location.Id, Amount: 12.5m, TagIds: [h.Tag.Id]));
        var db = scope.ServiceProvider.GetRequiredService<Saldo.Infrastructure.Sqlite.Persistence.SaldoDbContext>();
        var party = await db.Parties.FindAsync(h.Party.Id);
        party!.Name = "Nowa nazwa";
        await db.SaveChangesAsync();
        await vm.LoadTemplateAsync(saved.Value.Id);
        Assert.Equal("Nowa nazwa", vm.CounterpartyText);
        Assert.Equal("12,5", vm.AmountText);
        Assert.Equal("Old", vm.Description);
        Assert.Equal(h.Category.Id, vm.SelectedCategory!.Id);
        Assert.Equal(h.Location.Id, vm.SelectedLocation!.Id);
        Assert.True(Assert.Single(vm.Tags).IsSelected);
        await scope.ServiceProvider.GetRequiredService<DeleteTransactionTemplate>().ExecuteAsync(saved.Value.Id);
        await vm.LoadTemplateAsync(saved.Value.Id);
        Assert.Equal(h.Localization["Template_NotFound"], vm.TemplatePicker.TemplatePickerStatus);
        Assert.Equal("12,5", vm.AmountText);
        Assert.Equal("Nowa nazwa", vm.CounterpartyText);
    }
}
