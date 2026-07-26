using Saldo.Domain.Enums;

namespace Saldo.Domain.Entities;

public sealed class Transaction
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public TransactionType Type { get; set; }

    /// <summary>
    /// Always positive; semantics are defined by Type (Expense/Income).
    /// </summary>
    public decimal Amount { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    /// <summary>
    /// Who initiated/paid the transaction.
    /// </summary>
    public int PayerId { get; set; }
    public Party Payer { get; set; } = null!;

    /// <summary>
    /// The other side of the transaction (shop/company/person).
    /// </summary>
    public int CounterpartyId { get; set; }
    public Party Counterparty { get; set; } = null!;

    public int? LocationId { get; set; }
    public Location? Location { get; set; }

    public string? Description { get; set; }

    public ICollection<TransactionTag> Tags { get; set; } = new List<TransactionTag>();
}
