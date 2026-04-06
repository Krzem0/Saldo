namespace Saldo.Domain.Entities;

public sealed class TransactionTag
{
    public int TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
