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
