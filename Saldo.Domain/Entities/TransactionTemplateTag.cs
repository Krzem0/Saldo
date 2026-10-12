namespace Saldo.Domain.Entities;

public sealed class TransactionTemplateTag
{
    public int TransactionTemplateId { get; set; }
    public TransactionTemplate TransactionTemplate { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
