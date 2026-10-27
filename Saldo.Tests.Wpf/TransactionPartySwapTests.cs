using Saldo.Application.DTOs;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;

namespace Saldo.Tests.Wpf;

public sealed class TransactionPartySwapTests
{
    [Theory]
    [InlineData(TransactionType.Expense, false)]
    [InlineData(TransactionType.Expense, true)]
    [InlineData(TransactionType.Income, false)]
    [InlineData(TransactionType.Income, true)]
    public void TemplateTypeChange_SwapsPartiesAndText_AndChangingBackRestoresThem(TransactionType initialType, bool hasCounterparty)
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var recipient = new Party { Id = h.Party.Id + 1, Name = "Odbiorca" };
        var existing = new TransactionTemplateDto(1, "Szablon", initialType, null,
            null, h.Party.Id, hasCounterparty ? recipient.Id : null, null, null, []);
        var form = new TransactionTemplateFormViewModel(h.Scopes, h.Dialogs, h.Localization,
            [h.Category], [h.Party, recipient], [h.Location], [h.Tag], existing);
        Assert.Same(h.Party, form.SelectedPayer);
        Assert.Equal(hasCounterparty ? recipient : null, form.SelectedCounterparty);
        Assert.False(form.HasUnsavedChanges);

        if (initialType == TransactionType.Expense) form.IsIncome = true;
        else form.IsExpense = true;

        Assert.Equal(hasCounterparty ? recipient : null, form.SelectedPayer);
        Assert.Equal(hasCounterparty ? recipient.Name : "", form.PayerText);
        Assert.Same(h.Party, form.SelectedCounterparty);
        Assert.Equal(h.Party.Name, form.CounterpartyText);
        Assert.True(form.HasUnsavedChanges);
        form.SelectedType = form.SelectedType;
        Assert.Same(h.Party, form.SelectedCounterparty);

        form.SelectedType = form.Types.Single(type => type.Value == initialType);
        Assert.Same(h.Party, form.SelectedPayer);
        Assert.Equal(h.Party.Name, form.PayerText);
        Assert.Equal(hasCounterparty ? recipient : null, form.SelectedCounterparty);
        Assert.Equal(hasCounterparty ? recipient.Name : "", form.CounterpartyText);
        Assert.False(form.HasUnsavedChanges);
    }

    [Fact]
    public void TemplateTypeChange_PreservesUnselectedTypedNames()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var form = h.Form();
        form.PayerText = "Niewybrany płatnik";
        form.CounterpartyText = "Niewybrany odbiorca";
        form.IsIncome = true;
        Assert.Null(form.SelectedPayer);
        Assert.Null(form.SelectedCounterparty);
        Assert.Equal("Niewybrany odbiorca", form.PayerText);
        Assert.Equal("Niewybrany płatnik", form.CounterpartyText);
        form.IsExpense = true;
        Assert.Equal("Niewybrany płatnik", form.PayerText);
        Assert.Equal("Niewybrany odbiorca", form.CounterpartyText);
    }

    [Fact]
    public async Task EditingTemplateType_PersistsSwappedParties()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var form = h.Form();
        form.Name = "Szablon";
        form.SelectedCounterparty = h.Party;
        form.CounterpartyText = h.Party.Name;
        await form.SaveAsync();
        var edit = h.Form(Assert.Single(await h.List()));
        edit.IsIncome = true;
        await edit.SaveAsync();
        var saved = Assert.Single(await h.List());
        Assert.Equal(TransactionType.Income, saved.TransactionType);
        Assert.Equal(h.Party.Id, saved.PayerId);
        Assert.Null(saved.CounterpartyId);
    }

    [Theory]
    [InlineData(TransactionType.Expense, false)]
    [InlineData(TransactionType.Expense, true)]
    [InlineData(TransactionType.Income, false)]
    [InlineData(TransactionType.Income, true)]
    public void TypeChange_SwapsPartiesAndText_AndChangingBackRestoresThem(TransactionType initialType, bool hasCounterparty)
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var payer = h.Party;
        var recipient = new Party { Id = payer.Id + 1, Name = "Odbiorca" };
        var defaults = new NewTransactionDefaultsDto(new DateOnly(2026, 10, 20), initialType, payer.Id);
        var form = new AddEditTransactionViewModel(h.Scopes, h.Dialogs, h.Localization,
            [h.Category], [payer, recipient], [h.Location], [h.Tag], defaults);
        if (hasCounterparty)
        {
            form.SelectedCounterparty = recipient;
            form.CounterpartyText = recipient.Name;
        }
        var before = form.CreateDraft();

        form.SelectedType = form.Types.Single(type => type.Value != initialType);

        Assert.Equal(before.CounterpartyId, form.SelectedPayer?.Id);
        Assert.Equal(before.CounterpartyText, form.PayerText);
        Assert.Same(payer, form.SelectedCounterparty);
        Assert.Equal(before.PayerText, form.CounterpartyText);
        Assert.Equal(before.Date, form.Date);
        var swapped = form.CreateDraft();
        form.SelectedType = form.SelectedType;
        Assert.Equal(swapped.PayerId, form.SelectedPayer?.Id);
        Assert.Equal(swapped.PayerText, form.PayerText);
        Assert.Equal(swapped.CounterpartyId, form.SelectedCounterparty?.Id);
        Assert.Equal(swapped.CounterpartyText, form.CounterpartyText);

        if (initialType == TransactionType.Expense) form.IsExpense = true;
        else form.IsIncome = true;
        Assert.Equal(before.PayerId, form.SelectedPayer?.Id);
        Assert.Equal(before.PayerText, form.PayerText);
        Assert.Equal(before.CounterpartyId, form.SelectedCounterparty?.Id);
        Assert.Equal(before.CounterpartyText, form.CounterpartyText);
        Assert.Equal(payer.Id, defaults.PayerId);
    }

    [Fact]
    public void TypeChange_PreservesUnselectedTypedNames()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var form = new AddEditTransactionViewModel(h.Scopes, h.Dialogs, h.Localization,
            [h.Category], [h.Party], [h.Location], [h.Tag])
        { PayerText = "Niewybrany płatnik", CounterpartyText = "Niewybrany odbiorca" };
        form.IsIncome = true;
        Assert.Null(form.SelectedPayer);
        Assert.Null(form.SelectedCounterparty);
        Assert.Equal("Niewybrany odbiorca", form.PayerText);
        Assert.Equal("Niewybrany płatnik", form.CounterpartyText);
        form.IsExpense = true;
        Assert.Equal("Niewybrany płatnik", form.PayerText);
        Assert.Equal("Niewybrany odbiorca", form.CounterpartyText);
    }
}
