namespace Saldo.Domain.Entities;

public sealed class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Portable icon identifier, for example mdi:Home; independent of WPF types.</summary>
    public string? IconKey { get; set; }
    public string? ColorCode { get; set; }

    // Optional: jeśli chcesz rozróżniać kategorie pod Income/Expense, dodaj później.
    // public TransactionType? AppliesTo { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
