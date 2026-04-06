namespace Saldo.Domain.Entities;

public sealed class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Optional: jeśli chcesz rozróżniać kategorie pod Income/Expense, dodaj później.
    // public TransactionDirection? AppliesTo { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
