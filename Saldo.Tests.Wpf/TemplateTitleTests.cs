using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Enums;

namespace Saldo.Tests.Wpf;

public sealed class TemplateTitleTests
{
    [Theory]
    [InlineData("pl-PL")]
    [InlineData("en-US")]
    public async Task OpenedFromTemplate_TitleAndNameSuggestionSurviveEditsAndDraft_AndClearRemovesThem(string culture)
    {
        using var h = new TransactionTemplatesTests.Harness(culture);
        var template = new TransactionTemplateDto(1, "Rata kredytu", TransactionType.Expense, null, null, null, null, null, 100m, []);
        var vm = new AddEditTransactionViewModel(h.Scopes, h.Dialogs, h.Localization,
            [h.Category], [h.Party], [h.Location], [h.Tag], template: template);
        var suffix = string.Format(h.Localization.CurrentCulture, h.Localization["Transaction_TemplateTitleSuffix"], template.Name);
        Assert.Equal(h.Localization["Transaction_AddTitle"] + suffix, vm.Title);
        vm.AmountText = "120";
        await vm.SaveAsTemplateAsync();
        Assert.Equal(template.Name, h.Dialogs.NameInitialValue);
        Assert.Equal(h.Localization["Transaction_AddTitle"] + suffix, vm.Title);
        var restored = new AddEditTransactionViewModel(h.Scopes, h.Dialogs, h.Localization,
            [h.Category], [h.Party], [h.Location], [h.Tag], draft: vm.CreateDraft());
        Assert.Equal(h.Localization["Transaction_AddDraftTitle"] + suffix, restored.Title);
        await restored.SaveAsTemplateAsync();
        Assert.Equal(template.Name, h.Dialogs.NameInitialValue);
        restored.ClearCommand.Execute(null);
        Assert.Equal(h.Localization["Transaction_AddTitle"], restored.Title);
        await restored.SaveAsTemplateAsync();
        Assert.Null(h.Dialogs.NameInitialValue);
    }

    [Fact]
    public async Task LoadingAnotherTemplate_UpdatesTitleAndSuggestedName()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        using var scope = h.Scopes.CreateScope();
        var add = scope.ServiceProvider.GetRequiredService<AddTransactionTemplate>();
        var first = (await add.ExecuteAsync(new("Pierwszy", TransactionType.Expense, Amount: 10m))).Value;
        var second = (await add.ExecuteAsync(new("Drugi", TransactionType.Income, Amount: 20m))).Value;
        var vm = new AddEditTransactionViewModel(h.Scopes, h.Dialogs, h.Localization,
            [h.Category], [h.Party], [h.Location], [h.Tag]);
        var titles = new List<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Title)) titles.Add(vm.Title); };
        await vm.LoadTemplateAsync(first.Id);
        Assert.Contains("Pierwszy", vm.Title);
        await vm.LoadTemplateAsync(second.Id);
        Assert.Contains("Drugi", vm.Title);
        Assert.DoesNotContain("Pierwszy", vm.Title);
        Assert.Contains(titles, title => title.Contains("Drugi"));
        vm.Description = "Dopisany opis";
        await vm.SaveAsTemplateAsync();
        Assert.Equal("Drugi", h.Dialogs.NameInitialValue);
    }
}
