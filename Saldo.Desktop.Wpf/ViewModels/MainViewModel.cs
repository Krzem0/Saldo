using Saldo.Desktop.Wpf.Infrastructure;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public TransactionListViewModel TransactionList { get; }

    public MainViewModel(TransactionListViewModel transactionList)
    {
        TransactionList = transactionList;
    }
}
