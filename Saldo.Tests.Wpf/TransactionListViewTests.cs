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
    [Theory]
    [InlineData("LightTheme")]
    [InlineData("DarkTheme")]
    public void View_SummaryButtonsFilterRowsAndKeepMonthlyTotalsAcrossReloads(string theme)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var bindings = new BindingErrorScope();
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
                foreach (var attribute in root.Descendants().Attributes("Click").ToArray()) attribute.Remove();
                var resources = root.Element(presentation + "UserControl.Resources")!.Element(presentation + "ResourceDictionary")!;
                resources.Element(presentation + "ResourceDictionary.MergedDictionaries")!.AddAfterSelf(
                    new XElement(localization + "LocalizationService", new XAttribute(x + "Key", "Localization")));
                foreach (var key in new[] { "MonthNavigationButtonStyle", "SecondaryActionButtonStyle", "DangerActionButtonStyle", "ClearTextButtonStyle" })
                    resources.Add(new XElement(presentation + "Style", new XAttribute(x + "Key", key), new XAttribute("TargetType", "Button")));
                var view = (UserControl)XamlReader.Parse(source.ToString(SaveOptions.DisableFormatting).Replace("clr-namespace:Saldo.Desktop.Wpf.Services\"", "clr-namespace:Saldo.Desktop.Wpf.Services;assembly=Saldo.Desktop.Wpf\""), new ParserContext
                {
                    BaseUri = new Uri("pack://application:,,,/Saldo.Desktop.Wpf;component/Views/TransactionListView.xaml")
                });
                view.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Application.LoadComponent(
                    new Uri($"/Saldo.Desktop.Wpf;component/Themes/{theme}.xaml", UriKind.Relative)));
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
                repository.AddAsync(new Transaction { Date = today, Type = TransactionType.Income, Amount = 100m, Description = "Pensja" }).GetAwaiter().GetResult();
                repository.AddAsync(new Transaction { Date = today, Type = TransactionType.Expense, Amount = 30m, Description = "Zakupy",
                    Category = new Category { Name = "Jedzenie" }, Payer = new Party { Name = "Marcin" },
                    Counterparty = new Party { Name = "Społem" }, Location = new Location { Name = "Olsztyn" },
                    Tags = [new TransactionTag { TagId = 1, Tag = new Tag { Name = "Praca" } }] }).GetAwaiter().GetResult();
                repository.AddAsync(new Transaction { Date = today.AddMonths(-1), Type = TransactionType.Expense, Amount = 999m, Description = "Zakupy" }).GetAwaiter().GetResult();
                using var provider = new ServiceCollection()
                    .AddSingleton<ITransactionRepository>(repository)
                    .AddSingleton<IAccountingPeriodRepository, FakeAccountingPeriodRepository>().AddScoped<VerifyAccountingPeriod>()
                    .AddScoped<ListTransactions>().AddScoped<GetSummary>()
                    .BuildServiceProvider();
                var vm = new TransactionListViewModel(provider.GetRequiredService<IServiceScopeFactory>(), null!, new LocalizationService());
                view.DataContext = vm;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Hidden, ((Border)view.FindName("PeriodStateGroup")).Visibility);
                vm.LoadCommand.Execute(null);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                var grid = (DataGrid)view.FindName("TransactionGrid");
                var summary = vm.Summary;
                var periodState = (TextBlock)view.FindName("PeriodStateLabel");
                var periodAction = (Button)view.FindName("PeriodActionButton");
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(((LocalizationService)view.Resources["Localization"])["AccountingPeriod_Open"], periodState.Text);
                Assert.True(periodAction.IsEnabled);
                Assert.Equal(Visibility.Visible, ((Border)view.FindName("PeriodStateGroup")).Visibility);
                Assert.Equal(((SolidColorBrush)view.Resources["SuccessBackgroundBrush"]).Color,
                    ((SolidColorBrush)((Border)view.FindName("PeriodStateGroup")).Background).Color);
                Assert.Equal(Visibility.Collapsed, ((Button)view.FindName("PeriodRetryButton")).Visibility);
                Assert.Equal(Visibility.Collapsed, ((TextBlock)view.FindName("PeriodIntegrityLabel")).Visibility);
                Assert.Equal(2, grid.Items.Count);
                Assert.True(vm.IsAllFilterActive);
                var buttons = Descendants(view).OfType<Button>().ToList();
                view.UpdateLayout();
                var summaryButtons = (Grid)view.FindName("SummaryButtons");
                var searchBorder = (Border)((Grid)((TextBox)view.FindName("TransactionSearchBox")).Parent).Parent;
                var summaryCenter = summaryButtons.TranslatePoint(new Point(summaryButtons.ActualWidth / 2, 0), view).X;
                Assert.True(periodAction.TranslatePoint(new Point(), view).Y < summaryButtons.TranslatePoint(new Point(), view).Y);
                var monthPicker = (Button)view.FindName("MonthPickerButton");
                Assert.True(periodAction.TranslatePoint(new Point(periodAction.ActualWidth, 0), view).X
                    <= monthPicker.TranslatePoint(new Point(), view).X);
                Assert.Equal(view.ActualWidth / 2, summaryCenter, 1);
                Assert.True(summaryButtons.TranslatePoint(new Point(summaryButtons.ActualWidth, 0), view).X
                    <= searchBorder.TranslatePoint(new Point(), view).X);
                var search = (TextBox)view.FindName("TransactionSearchBox");
                void Search(string text)
                {
                    search.Text = text;
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                }
                vm.SelectedTransaction = vm.Transactions.Single(t => t.Type == TransactionType.Income);
                foreach (var query in new[] { "  ZAKUPY  ", "jedzenie", "marcin", "społem", "olsztyn", "praca" })
                {
                    Search(query);
                    Assert.Equal(30m, Assert.IsType<Saldo.Application.DTOs.TransactionDto>(Assert.Single(grid.Items.Cast<object>())).Amount);
                }
                Assert.Null(vm.SelectedTransaction);
                Assert.False(vm.EditCommand.CanExecute(null));
                Assert.Equal(100m, vm.Summary!.TotalIncome);
                Assert.Equal(30m, vm.Summary.TotalExpense);
                Click(vm.ShowIncomeCommand);
                Assert.Empty(grid.Items.Cast<object>());
                Click(vm.ShowExpensesCommand);
                Assert.Single(grid.Items.Cast<object>());
                Search("zakupy");
                vm.LoadCommand.Execute(null);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Single(grid.Items.Cast<object>());
                vm.PreviousMonthCommand.Execute(null);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Equal("zakupy", search.Text);
                Assert.Equal(999m, Assert.IsType<Saldo.Application.DTOs.TransactionDto>(Assert.Single(grid.Items.Cast<object>())).Amount);
                vm.NextMonthCommand.Execute(null);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Equal(30m, Assert.IsType<Saldo.Application.DTOs.TransactionDto>(Assert.Single(grid.Items.Cast<object>())).Amount);
                Search("brak");
                Assert.Empty(grid.Items.Cast<object>());
                Assert.Equal(label + " 0", counter.Text);
                Assert.True(vm.ClearSearchCommand.CanExecute(null));
                Click(vm.ClearSearchCommand);
                Assert.Empty(search.Text);
                Assert.False(vm.ClearSearchCommand.CanExecute(null));
                Assert.Single(grid.Items.Cast<object>());
                Click(vm.ShowAllCommand);
                Assert.Equal(2, grid.Items.Count);
                summary = vm.Summary;

                void Click(System.Windows.Input.ICommand command)
                {
                    var button = buttons.Single(button => ReferenceEquals(button.Command, command));
                    button.Command.Execute(button.CommandParameter);
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                }

                var expense = vm.Transactions.Single(transaction => transaction.Type == TransactionType.Expense);
                view.UpdateLayout();
                var expenseRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(expense);
                var menu = expenseRow.ContextMenu;
                Assert.NotNull(menu);
                menu.PlacementTarget = expenseRow;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                var addSimilar = Assert.IsType<MenuItem>(Assert.Single(menu.Items.Cast<object>()));
                Assert.Same(vm.AddSimilarCommand, addSimilar.Command);
                Assert.Same(expense, addSimilar.CommandParameter);
                Assert.Equal(((LocalizationService)view.Resources["Localization"])["Transaction_Copy"], addSimilar.Header);
                var actionsColumn = Assert.IsType<DataGridTemplateColumn>(grid.Columns.Last());
                Assert.Equal(((LocalizationService)view.Resources["Localization"])["Actions"], actionsColumn.Header);
                Assert.False(actionsColumn.CanUserSort);
                grid.ScrollIntoView(expense, actionsColumn);
                view.UpdateLayout();
                var copyButton = Assert.Single(Descendants(actionsColumn.GetCellContent(expense)).OfType<Button>());
                Assert.Same(vm.AddSimilarCommand, copyButton.Command);
                Assert.Same(expense, copyButton.CommandParameter);
                Assert.Equal(addSimilar.Header, copyButton.ToolTip);
                Assert.Equal(addSimilar.Header, System.Windows.Automation.AutomationProperties.GetName(copyButton));
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

                Click(vm.ShowExpensesCommand);
                var collection = vm.Transactions;
                var sortedView = System.Windows.Data.CollectionViewSource.GetDefaultView(grid.ItemsSource);
                var amountColumn = grid.Columns.Single(column => column.SortMemberPath == "Amount");
                sortedView.SortDescriptions.Clear();
                sortedView.SortDescriptions.Add(new System.ComponentModel.SortDescription("Amount", System.ComponentModel.ListSortDirection.Ascending));
                amountColumn.SortDirection = System.ComponentModel.ListSortDirection.Ascending;
                var anotherExpense = new Transaction { Date = new DateOnly(today.Year, today.Month, 1),
                    Type = TransactionType.Expense, Amount = 10m, Description = "Inny wydatek" };
                repository.AddAsync(anotherExpense).GetAwaiter().GetResult();
                vm.LoadCommand.Execute(null);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Same(collection, vm.Transactions);
                Assert.Same(sortedView, System.Windows.Data.CollectionViewSource.GetDefaultView(grid.ItemsSource));
                Assert.Equal(new[] { 10m, 30m }, grid.Items.Cast<Saldo.Application.DTOs.TransactionDto>().Select(t => t.Amount));
                Assert.Equal(System.ComponentModel.ListSortDirection.Ascending, amountColumn.SortDirection);

                anotherExpense.Amount = 40m;
                repository.UpdateAsync(anotherExpense).GetAwaiter().GetResult();
                vm.LoadCommand.Execute(null);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Equal(new[] { 30m, 40m }, grid.Items.Cast<Saldo.Application.DTOs.TransactionDto>().Select(t => t.Amount));
                Assert.True(vm.IsExpenseFilterActive);
                sortedView.SortDescriptions.Clear();
                sortedView.SortDescriptions.Add(new System.ComponentModel.SortDescription("Date", System.ComponentModel.ListSortDirection.Ascending));
                vm.NextMonthCommand.Execute(null);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                vm.PreviousMonthCommand.Execute(null);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Equal(System.ComponentModel.ListSortDirection.Ascending, Assert.Single(sortedView.SortDescriptions).Direction);
                Assert.Equal("Date", Assert.Single(sortedView.SortDescriptions).PropertyName);
                var dates = grid.Items.Cast<Saldo.Application.DTOs.TransactionDto>().Select(t => t.Date).ToArray();
                Assert.Equal(dates.OrderBy(date => date), dates);
                Assert.DoesNotContain(bindings.Errors, error => error.Contains("AncestorType='System.Windows.Controls.MenuItem'"));
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
