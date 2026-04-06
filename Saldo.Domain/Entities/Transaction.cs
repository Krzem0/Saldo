using Saldo.Domain.Enums;

namespace Saldo.Domain.Entities;

public sealed class Transaction
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public TransactionDirection Direction { get; set; }

    /// <summary>
    /// Always positive; semantics are defined by Direction (Expense/Income).
    /// </summary>
    public decimal Amount { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    /// <summary>
    /// Who initiated/paid the transaction.
    /// </summary>
    public int PayerId { get; set; }
    public Member Payer { get; set; } = null!;

    /// <summary>
    /// The other side of the transaction (shop/company/person).
    /// </summary>
    public int CounterpartyId { get; set; }
    public Counterparty Counterparty { get; set; } = null!;

    public string? Description { get; set; }
    public string? Location { get; set; }

    public ICollection<TransactionTag> Tags { get; set; } = new List<TransactionTag>();
}
