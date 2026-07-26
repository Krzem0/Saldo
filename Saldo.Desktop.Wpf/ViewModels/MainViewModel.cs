using Saldo.Desktop.Wpf.Infrastructure;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public TransactionListViewModel TransactionList { get; }
    public CategoriesViewModel Categories { get; }
    public PartiesViewModel Parties { get; }
    public LocationsViewModel Locations { get; }

    public MainViewModel(
        TransactionListViewModel transactionList,
        CategoriesViewModel categories,
        PartiesViewModel parties,
        LocationsViewModel locations)
    {
        TransactionList = transactionList;
        Categories = categories;
        Parties = parties;
        Locations = locations;
    }
}
