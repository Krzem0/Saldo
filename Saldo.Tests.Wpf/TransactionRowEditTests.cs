using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Saldo.Application.DTOs;
using Saldo.Desktop.Wpf.Controls;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Domain.Enums;

namespace Saldo.Tests.Wpf;

public sealed class TransactionRowEditTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DoubleClick_EditsClickedRowOnlyWhenCommandAllowsIt(bool closedMonth) => OnSta(() =>
    {
        using var fixture = new GridFixture();
        object? edited = null;
        var command = new RelayCommand(() => edited = fixture.Grid.SelectedItem, () => !closedMonth);
        TransactionRowEditBehavior.SetCommand(fixture.Row, command);
        var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            { RoutedEvent = Control.MouseDoubleClickEvent };

        fixture.Row.RaiseEvent(click);

        Assert.Same(fixture.Second, fixture.Grid.SelectedItem);
        Assert.Equal(!closedMonth, click.Handled);
        if (closedMonth) Assert.Null(edited);
        else Assert.Same(fixture.Second, edited);
    });

    [Fact]
    public void RightClick_SelectsTargetRowBeforeContextMenuCommandRuns() => OnSta(() =>
    {
        using var fixture = new GridFixture();
        object? edited = null;
        var command = new RelayCommand(() => edited = fixture.Grid.SelectedItem);
        TransactionRowEditBehavior.SetCommand(fixture.Row, command);
        var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right)
            { RoutedEvent = UIElement.PreviewMouseRightButtonDownEvent };

        fixture.Row.RaiseEvent(click);

        Assert.False(click.Handled);
        Assert.Same(fixture.Second, fixture.Grid.SelectedItem);
        command.Execute(null);
        Assert.Same(fixture.Second, edited);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DoubleClick_IgnoresRowActionButtonsAndRightMouseButton(bool onActionButton) => OnSta(() =>
    {
        using var fixture = new GridFixture();
        var called = false;
        TransactionRowEditBehavior.SetCommand(fixture.Row, new RelayCommand(() => called = true));
        var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, onActionButton ? MouseButton.Left : MouseButton.Right)
        {
            RoutedEvent = Control.MouseDoubleClickEvent,
            Source = onActionButton ? new Button() : fixture.Row
        };
        fixture.Row.RaiseEvent(click);
        Assert.False(called);
        Assert.False(click.Handled);
        Assert.Same(fixture.First, fixture.Grid.SelectedItem);
    });

    private sealed class GridFixture : IDisposable
    {
        private readonly HwndSource _source;
        public TransactionDto First { get; } = Transaction(1);
        public TransactionDto Second { get; } = Transaction(2);
        public DataGrid Grid { get; }
        public DataGridRow Row { get; }

        public GridFixture()
        {
            Grid = new DataGrid { ItemsSource = new[] { First, Second }, AutoGenerateColumns = false,
                IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single };
            Grid.Columns.Add(new DataGridTextColumn { Binding = new System.Windows.Data.Binding("Description") });
            _source = new HwndSource(new HwndSourceParameters("Transaction row edit verification")
                { Width = 400, Height = 200, WindowStyle = 0 }) { RootVisual = Grid };
            Grid.Measure(new Size(400, 200));
            Grid.Arrange(new Rect(0, 0, 400, 200));
            Grid.UpdateLayout();
            Row = (DataGridRow)Grid.ItemContainerGenerator.ContainerFromItem(Second);
            Assert.NotNull(Row);
            Grid.SelectedItem = First;
        }

        private static TransactionDto Transaction(int id) => new(id, new DateOnly(2026, 10, 1),
            TransactionType.Expense, 10m, 1, "Category", null, null, null, null, null, null, null, $"Transaction {id}", [], []);
        public void Dispose() => _source.Dispose();
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Transaction row edit test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
