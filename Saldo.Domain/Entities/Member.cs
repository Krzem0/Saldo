namespace Saldo.Domain.Entities;

public sealed class Member
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Transaction> TransactionsPaid { get; set; } = new List<Transaction>();
}
