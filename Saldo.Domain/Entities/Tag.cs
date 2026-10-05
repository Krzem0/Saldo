namespace Saldo.Domain.Entities;

public sealed class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Portable icon identifier, for example mdi:Account; independent of WPF types.</summary>
    public string? IconKey { get; set; }
    public string? ColorCode { get; set; }

    public ICollection<TransactionTag> Transactions { get; set; } = new List<TransactionTag>();
}
