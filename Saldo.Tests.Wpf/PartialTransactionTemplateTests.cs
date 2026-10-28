using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Tests.Wpf;

public sealed class PartialTransactionTemplateTests
{
    [Fact]
    public async Task LoadingTemplateWithoutTags_ClearsPreviousTemplateTagsAndPreservesOtherUnspecifiedFields()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var tagged = h.Form();
        tagged.Name = "Z tagiem";
        tagged.Description = "Poprzedni opis";
        tagged.SelectedCategory = h.Category;
        tagged.SelectedPayer = tagged.SelectedCounterparty = h.Party;
        tagged.SelectedLocation = h.Location;
        tagged.Tags[0].IsSelected = true;
        await tagged.SaveAsync();
        var untagged = h.Form();
        untagged.Name = "Bez tagów";
        untagged.AmountText = "25";
        await untagged.SaveAsync();
        var templates = await h.List();
        Assert.Equal(2, templates.Count);
        var form = new AddEditTransactionViewModel(h.Scopes, h.Dialogs, h.Localization,
            [h.Category], [h.Party], [h.Location], [h.Tag]);

        await form.LoadTemplateAsync(templates.Single(t => t.Name == "Z tagiem").Id);
        Assert.Equal(h.Tag.Id, Assert.Single(form.CreateDraft().TagIds));
        var previous = form.CreateDraft();

        await form.LoadTemplateAsync(templates.Single(t => t.Name == "Bez tagów").Id);

        var current = form.CreateDraft();
        Assert.Empty(current.TagIds);
        Assert.All(form.Tags, tag => Assert.False(tag.IsSelected));
        Assert.Equal("25", form.AmountText);
        Assert.Equal(previous.Description, current.Description);
        Assert.Equal(previous.CategoryId, current.CategoryId);
        Assert.Equal(previous.PayerId, current.PayerId);
        Assert.Equal(previous.CounterpartyId, current.CounterpartyId);
        Assert.Equal(previous.LocationId, current.LocationId);
        Assert.Equal(previous.Date, current.Date);
    }

    [Fact]
    public async Task Manager_AllowsTagsOnlyAndLocalizesEmptyAndDuplicateContentOnAddAndEdit()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var form = h.Form();
        form.Name = "Do pracy";
        await form.SaveAsync();
        Assert.Equal(h.Localization["Template_ContentRequired"], form.SummaryError);
        form.Tags[0].IsSelected = true;
        await form.SaveAsync();
        var saved = Assert.Single(await h.List());
        Assert.Null(saved.CategoryId);
        Assert.Null(saved.Description);
        var duplicate = h.Form();
        duplicate.Name = "Inna nazwa";
        duplicate.Tags[0].IsSelected = true;
        await duplicate.SaveAsync();
        Assert.Equal(h.Localization["Template_ContentDuplicate"], duplicate.SummaryError);
        var edit = h.Form(saved);
        edit.Name = "Nowa nazwa";
        await edit.SaveAsync();
        Assert.Empty(edit.SummaryError);
        edit.Tags[0].IsSelected = false;
        await edit.SaveAsync();
        Assert.Equal(h.Localization["Template_ContentRequired"], edit.SummaryError);
        Assert.Equal(h.Tag.Id, Assert.Single(Assert.Single(await h.List()).TagIds));
    }

    [Fact]
    public async Task SaveAsTemplate_AllowsAmountOnlyAndRejectsDuplicateUnderDifferentName()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var form = new AddEditTransactionViewModel(h.Scopes, h.Dialogs, h.Localization,
            [h.Category], [h.Party], [h.Location], [h.Tag]);
        h.Dialogs.NameResult = "Pierwszy";
        await form.SaveAsTemplateAsync();
        Assert.Equal(h.Localization["Template_ContentRequired"], form.ValidationSummary);
        form.AmountText = "10";
        await form.SaveAsTemplateAsync();
        var saved = Assert.Single(await h.List());
        Assert.Null(saved.CategoryId);
        Assert.Null(saved.Description);
        Assert.Equal(10m, saved.Amount);
        h.Dialogs.NameResult = "Drugi";
        await form.SaveAsTemplateAsync();
        Assert.Equal(h.Localization["Template_ContentDuplicate"], form.ValidationSummary);
        Assert.Single(await h.List());
        var vm = new TransactionListViewModel(h.Scopes, h.Dialogs, h.Localization);
        AddEditTransactionViewModel? shown = null;
        h.Dialogs.TransactionCallback = f => { shown = f; return false; };
        await vm.AddFromTemplateAsync(saved.Id);
        Assert.NotNull(shown);
        Assert.Null(shown.SelectedCategory);
        Assert.Null(shown.Description);
        Assert.Equal("10", shown.AmountText);
    }
}
