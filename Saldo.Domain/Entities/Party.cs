namespace Saldo.Domain.Entities;

public sealed class Party
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Transaction> PayerTransactions { get; set; } = new List<Transaction>();

    public ICollection<Transaction> CounterpartyTransactions { get; set; } = new List<Transaction>();
}
