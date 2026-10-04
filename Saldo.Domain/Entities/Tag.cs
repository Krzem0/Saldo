namespace Saldo.Domain.Entities;

public sealed class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ColorCode { get; set; }

    public ICollection<TransactionTag> Transactions { get; set; } = new List<TransactionTag>();
}
