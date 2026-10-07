using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml.Linq;
using Saldo.Desktop.Wpf.Infrastructure;

namespace Saldo.Tests.Wpf;

public sealed class SettingsTemplateTests
{
    [Fact]
    public void DefaultSelectors_ShareTemplateAndClearTheirOwnSelection()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                _ = new FrameworkElement();
                XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
                XNamespace localization = "clr-namespace:Saldo.Desktop.Wpf.Localization;assembly=Saldo.Desktop.Wpf";
                var source = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Views", "SettingsView.xaml"));
                var style = source.Descendants(p + "Style").Single(element => (string?)element.Attribute(x + "Key") == "DefaultReferenceComboBoxStyle");
                var selectors = source.Descendants(p + "ComboBox").Where(element =>
                    ((string?)element.Attribute("ItemsSource"))?.Contains("Default") == true).ToArray();
                var root = new XElement(p + "StackPanel", new XAttribute(XNamespace.Xmlns + "x", x.NamespaceName),
                    new XElement(p + "StackPanel.Resources", new XElement(p + "ResourceDictionary",
                        new XElement(p + "Style", new XAttribute("TargetType", "ComboBox")),
                        new XElement(localization + "LocalizationService", new XAttribute(x + "Key", "Localization")),
                        new XElement(style))), selectors.Select(element => new XElement(element)));
                var panel = (StackPanel)XamlReader.Parse(root.ToString(SaveOptions.DisableFormatting));
                var state = new SelectorState();
                var payer = (ComboBox)panel.Children[0];
                var location = (ComboBox)panel.Children[1];
                state.ClearDefaultPayerCommand = new RelayCommand(() => payer.SelectedItem = null);
                state.ClearDefaultLocationCommand = new RelayCommand(() => location.SelectedItem = null);
                panel.DataContext = state;
                panel.Measure(new Size(440, 100));
                panel.Arrange(new Rect(0, 0, 440, 100));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var payerClear = (Button)payer.Template.FindName("ClearButton", payer);
                var locationClear = (Button)location.Template.FindName("ClearButton", location);
                Assert.Equal(Visibility.Visible, payerClear.Visibility);
                Assert.Equal(Visibility.Visible, locationClear.Visibility);
                Assert.Same(state.ClearDefaultPayerCommand, payerClear.Command);
                Assert.Same(state.ClearDefaultLocationCommand, locationClear.Command);
                locationClear.Command.Execute(null);
                Assert.Null(location.SelectedItem);
                Assert.NotNull(payer.SelectedItem);
                Assert.Equal(Visibility.Collapsed, locationClear.Visibility);
                payerClear.Command.Execute(null);
                Assert.Null(payer.SelectedItem);
                Assert.Equal(Visibility.Collapsed, payerClear.Visibility);
            }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public sealed class SelectorState
    {
        public record Option(string Label);
        public Option[] DefaultPayerOptions { get; } = [new("Payer")];
        public Option[] DefaultLocationOptions { get; } = [new("Location")];
        public Option? SelectedDefaultPayerOption { get; set; }
        public Option? SelectedDefaultLocationOption { get; set; }
        public RelayCommand ClearDefaultPayerCommand { get; set; } = null!;
        public RelayCommand ClearDefaultLocationCommand { get; set; } = null!;
        public bool IsTransactionSettingsBusy => false;

        public SelectorState()
        {
            SelectedDefaultPayerOption = DefaultPayerOptions[0];
            SelectedDefaultLocationOption = DefaultLocationOptions[0];
        }
    }
}
