using System.Windows.Input;
using Saldo.Desktop.Wpf.Infrastructure;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private MainPage _selectedPage = MainPage.Transactions;

    public TransactionListViewModel TransactionList { get; }
    public CategoriesViewModel Categories { get; }
    public PartiesViewModel Parties { get; }
    public LocationsViewModel Locations { get; }
    public SettingsViewModel Settings { get; }

    public object CurrentPage => _selectedPage switch
    {
        MainPage.Transactions => TransactionList,
        MainPage.Categories => Categories,
        MainPage.Parties => Parties,
        MainPage.Locations => Locations,
        MainPage.Settings => Settings,
        _ => TransactionList
    };

    public bool IsTransactionsSelected => _selectedPage == MainPage.Transactions;
    public bool IsCategoriesSelected => _selectedPage == MainPage.Categories;
    public bool IsPartiesSelected => _selectedPage == MainPage.Parties;
    public bool IsLocationsSelected => _selectedPage == MainPage.Locations;
    public bool IsSettingsSelected => _selectedPage == MainPage.Settings;

    public ICommand ShowTransactionsCommand { get; }
    public ICommand ShowCategoriesCommand { get; }
    public ICommand ShowPartiesCommand { get; }
    public ICommand ShowLocationsCommand { get; }
    public ICommand ShowSettingsCommand { get; }

    public MainViewModel(
        TransactionListViewModel transactionList,
        CategoriesViewModel categories,
        PartiesViewModel parties,
        LocationsViewModel locations,
        SettingsViewModel settings)
    {
        TransactionList = transactionList;
        Categories = categories;
        Parties = parties;
        Locations = locations;
        Settings = settings;

        ShowTransactionsCommand = new RelayCommand(() => SelectPage(MainPage.Transactions));
        ShowCategoriesCommand = new RelayCommand(() => SelectPage(MainPage.Categories));
        ShowPartiesCommand = new RelayCommand(() => SelectPage(MainPage.Parties));
        ShowLocationsCommand = new RelayCommand(() => SelectPage(MainPage.Locations));
        ShowSettingsCommand = new RelayCommand(() => SelectPage(MainPage.Settings));
    }

    private void SelectPage(MainPage page)
    {
        if (_selectedPage == page)
        {
            return;
        }

        _selectedPage = page;

        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(IsTransactionsSelected));
        OnPropertyChanged(nameof(IsCategoriesSelected));
        OnPropertyChanged(nameof(IsPartiesSelected));
        OnPropertyChanged(nameof(IsLocationsSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));

        switch (page)
        {
            case MainPage.Categories:
                Categories.LoadCommand.Execute(null);
                break;
            case MainPage.Parties:
                Parties.LoadCommand.Execute(null);
                break;
            case MainPage.Locations:
                Locations.LoadCommand.Execute(null);
                break;
        }
    }

    private enum MainPage
    {
        Transactions,
        Categories,
        Parties,
        Locations,
        Settings
    }
}
