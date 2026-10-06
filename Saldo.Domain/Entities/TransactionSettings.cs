namespace Saldo.Domain.Entities;

public sealed class TransactionSettings
{
    public const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;
    public int? DefaultPayerId { get; set; }
}
