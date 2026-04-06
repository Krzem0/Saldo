namespace Saldo.Domain.Entities;

public sealed class Counterparty
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
