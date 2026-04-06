using Saldo.Desktop.Wpf.Infrastructure;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public TransactionListViewModel TransactionList { get; }
    public CategoriesViewModel Categories { get; }
    public MembersViewModel Members { get; }
    public CounterpartiesViewModel Counterparties { get; }

    public MainViewModel(
        TransactionListViewModel transactionList,
        CategoriesViewModel categories,
        MembersViewModel members,
        CounterpartiesViewModel counterparties)
    {
        TransactionList = transactionList;
        Categories = categories;
        Members = members;
        Counterparties = counterparties;
    }
}
