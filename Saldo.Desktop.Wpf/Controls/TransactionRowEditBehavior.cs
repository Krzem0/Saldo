using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Saldo.Application.DTOs;

namespace Saldo.Desktop.Wpf.Controls;

public static class TransactionRowEditBehavior
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command", typeof(ICommand), typeof(TransactionRowEditBehavior), new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(DependencyObject element) => (ICommand?)element.GetValue(CommandProperty);
    public static void SetCommand(DependencyObject element, ICommand? value) => element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not DataGridRow row) return;
        if (e.OldValue is not null)
        {
            row.PreviewMouseRightButtonDown -= OnRightButtonDown;
            row.MouseDoubleClick -= OnDoubleClick;
        }
        if (e.NewValue is not null)
        {
            row.PreviewMouseRightButtonDown += OnRightButtonDown;
            row.MouseDoubleClick += OnDoubleClick;
        }
    }

    private static bool SelectRow(DataGridRow row)
    {
        if (row.Item is not TransactionDto || ItemsControl.ItemsControlFromItemContainer(row) is not DataGrid grid)
            return false;
        grid.SetCurrentValue(DataGrid.SelectedItemProperty, row.Item);
        return true;
    }

    private static void OnRightButtonDown(object sender, MouseButtonEventArgs e) => SelectRow((DataGridRow)sender);

    private static void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.Handled) return;
        var row = (DataGridRow)sender;
        // A double-click on a row action must keep invoking that action only.
        for (var source = e.OriginalSource as DependencyObject; source is not null && source != row;)
        {
            if (source is ButtonBase or Hyperlink) return;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        if (!SelectRow(row)) return;
        var command = GetCommand(row);
        if (command?.CanExecute(null) != true) return;
        e.Handled = true;
        command.Execute(null);
    }
}
