using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Saldo.Application.DTOs;

namespace Saldo.Desktop.Wpf.Views;

public partial class TransactionListView : UserControl
{
    public TransactionListView()
    {
        InitializeComponent();
    }

    private void TransactionGrid_TargetUpdated(object sender, DataTransferEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        var view = CollectionViewSource.GetDefaultView(grid.ItemsSource);
        if (view is null)
        {
            return;
        }

        if (view.SortDescriptions.Count == 0)
        {
            view.SortDescriptions.Add(new SortDescription(nameof(TransactionDto.Date), ListSortDirection.Descending));
        }

        var dateColumn = grid.Columns.FirstOrDefault(column => column.SortMemberPath == nameof(TransactionDto.Date));
        if (dateColumn is not null
            && view.SortDescriptions.FirstOrDefault().PropertyName == nameof(TransactionDto.Date)
            && view.SortDescriptions.First().Direction == ListSortDirection.Descending)
        {
            dateColumn.SortDirection = ListSortDirection.Descending;
        }
    }
}
