using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Wpf;

public sealed class TransactionListViewTests
{
    [Fact]
    public void View_SummaryButtonsFilterRowsAndKeepMonthlyTotalsAcrossReloads()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                _ = new FrameworkElement();
                XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
                XNamespace localization = "clr-namespace:Saldo.Desktop.Wpf.Localization;assembly=Saldo.Desktop.Wpf";
                var source = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Views", "TransactionListView.xaml"));
                var root = source.Root!;
                root.Attribute(x + "Class")!.Remove();
                root.SetAttributeValue(XNamespace.Xmlns + "localization", localization.NamespaceName);
                root.SetAttributeValue(XNamespace.Xmlns + "controls", "clr-namespace:Saldo.Desktop.Wpf.Controls;assembly=Saldo.Desktop.Wpf");
                foreach (var element in root.Descendants().Where(element =>
                             element.Name.NamespaceName is "clr-namespace:Saldo.Desktop.Wpf.Localization" or "clr-namespace:Saldo.Desktop.Wpf.Controls"))
                    element.Name = XName.Get(element.Name.LocalName, element.Name.NamespaceName + ";assembly=Saldo.Desktop.Wpf");
                foreach (var attribute in root.Descendants().Attributes("TargetUpdated").ToArray()) attribute.Remove();
                var resources = root.Element(presentation + "UserControl.Resources")!.Element(presentation + "ResourceDictionary")!;
                resources.Add(new XElement(localization + "LocalizationService", new XAttribute(x + "Key", "Localization")));
                foreach (var key in new[] { "MonthNavigationButtonStyle", "SecondaryActionButtonStyle", "DangerActionButtonStyle" })
                    resources.Add(new XElement(presentation + "Style", new XAttribute(x + "Key", key), new XAttribute("TargetType", "Button")));
                var view = (UserControl)XamlReader.Parse(source.ToString(SaveOptions.DisableFormatting), new ParserContext
                {
                    BaseUri = new Uri("pack://application:,,,/Saldo.Desktop.Wpf;component/Views/TransactionListView.xaml")
                });
                var transactions = new ObservableCollection<object>();
                view.DataContext = new { Transactions = transactions };
                view.Measure(new Size(1200, 800));
                view.Arrange(new Rect(0, 0, 1200, 800));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var label = ((LocalizationService)view.Resources["Localization"])["TransactionList_RowCount"];
                var counter = Descendants(view).OfType<TextBlock>().Single(block => block.Text.StartsWith(label));
                Assert.Equal(label + " 0", counter.Text);
                transactions.Add(new object());
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Equal(label + " 1", counter.Text);

                var repository = new FakeTransactionRepository();
                var today = DateOnly.FromDateTime(DateTime.Today);
                repository.AddAsync(new Transaction { Date = today, Type = TransactionType.Income, Amount = 100m }).GetAwaiter().GetResult();
                repository.AddAsync(new Transaction { Date = today, Type = TransactionType.Expense, Amount = 30m }).GetAwaiter().GetResult();
                using var provider = new ServiceCollection()
                    .AddSingleton<ITransactionRepository>(repository)
                    .AddScoped<ListTransactions>().AddScoped<GetSummary>()
                    .BuildServiceProvider();
                var vm = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), null!, new LocalizationService());
                view.DataContext = vm;
                vm.LoadCommand.Execute(null);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                var grid = (DataGrid)view.FindName("TransactionGrid");
                var summary = vm.Summary;
                Assert.Equal(2, grid.Items.Count);
                Assert.True(vm.IsAllFilterActive);
                var buttons = Descendants(view).OfType<Button>().ToList();

                void Click(System.Windows.Input.ICommand command)
                {
                    var button = buttons.Single(button => ReferenceEquals(button.Command, command));
                    button.Command.Execute(button.CommandParameter);
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                }

                var expense = vm.Transactions.Single(transaction => transaction.Type == TransactionType.Expense);
                vm.SelectedTransaction = expense;
                Click(vm.ShowIncomeCommand);
                Assert.Equal(TransactionType.Income, Assert.IsType<Saldo.Application.DTOs.TransactionDto>(Assert.Single(grid.Items.Cast<object>())).Type);
                Assert.Null(vm.SelectedTransaction);
                Assert.False(vm.DeleteCommand.CanExecute(null));
                Assert.True(vm.IsIncomeFilterActive);
                Assert.Equal(label + " 1", counter.Text);
                Assert.Same(summary, vm.Summary);

                Click(vm.ShowExpensesCommand);
                Assert.Same(expense, Assert.Single(grid.Items.Cast<object>()));
                Assert.True(vm.IsExpenseFilterActive);
                vm.LoadCommand.Execute(null);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Single(grid.Items.Cast<object>());
                Assert.Equal(100m, vm.Summary!.TotalIncome);
                Assert.Equal(30m, vm.Summary.TotalExpense);

                vm.NextMonthCommand.Execute(null);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Empty(grid.Items.Cast<object>());
                Assert.Equal(label + " 0", counter.Text);
                Assert.True(vm.IsExpenseFilterActive);
                vm.PreviousMonthCommand.Execute(null);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Single(grid.Items.Cast<object>());
                Click(vm.ShowAllCommand);
                Assert.Equal(2, grid.Items.Count);
                Assert.Equal(label + " 2", counter.Text);
                Assert.True(vm.IsAllFilterActive);
            }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
