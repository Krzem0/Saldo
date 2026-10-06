using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Input;
using Saldo.Desktop.Wpf.Controls;

namespace Saldo.Tests.Wpf;

public sealed class AutocompleteComboBoxTests
{
    [Fact]
    public void CategoryTemplate_ShowsAppearanceAndStillFiltersAndSelectsByName() => OnSta(() =>
    {
        var category = new Saldo.Domain.Entities.Category
            { Id = 1, Name = "Mieszkanie", ColorCode = "#3366CC", IconKey = "mdi:Home" };
        var control = new AutocompleteComboBox
        {
            ItemsSource = new[] { category, new Saldo.Domain.Entities.Category { Id = 2, Name = "Transport" } },
            DisplayMemberPath = "Name"
        };
        var resources = new ResourceDictionary
            { Source = new Uri("/Saldo.Desktop.Wpf;component/Themes/Generic.xaml", UriKind.Relative) };
        control.Style = (Style)resources[typeof(AutocompleteComboBox)];
        control.ItemTemplate = (DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                          xmlns:controls="clr-namespace:Saldo.Desktop.Wpf.Controls;assembly=Saldo.Desktop.Wpf">
                <controls:CategoryBadge NameText="{Binding Name}" ColorCode="{Binding ColorCode}" IconKey="{Binding IconKey}"/>
            </DataTemplate>
            """);
        control.ApplyTemplate();
        var list = (ListBox)control.Template.FindName("PART_ItemsList", control);
        try
        {
            Assert.Empty(list.DisplayMemberPath);
            list.Measure(new Size(300, 100));
            list.Arrange(new Rect(0, 0, 300, 100));
            list.UpdateLayout();
            var badge = Descendants(list).OfType<CategoryBadge>().Single(b => b.NameText == category.Name);
            Assert.Equal(category.ColorCode, badge.ColorCode);
            Assert.Equal(category.IconKey, badge.IconKey);
            control.Text = "miesz";
            Assert.Same(category, Assert.Single(control.FilteredItemsSource!.Cast<object>()));
            list.SelectedItem = category;
            Assert.Same(category, control.SelectedItem);
            Assert.Equal(category.Name, control.Text);
            control.Measure(new Size(300, 40));
            control.Arrange(new Rect(0, 0, 300, 40));
            control.UpdateLayout();
            var selectedPresenter = (ContentPresenter)control.Template.FindName("PART_SelectedItemPresenter", control);
            var textBox = (TextBox)control.Template.FindName("PART_EditableTextBox", control);
            Assert.Equal(Visibility.Visible, selectedPresenter.Visibility);
            var selectedBadge = Descendants(selectedPresenter).OfType<CategoryBadge>().Single();
            Assert.Equal(category.Name, selectedBadge.NameText);
            Assert.Equal(category.IconKey, selectedBadge.IconKey);
            Assert.Equal(category.ColorCode, selectedBadge.ColorCode);
            Assert.Equal(0, textBox.Opacity);
            textBox.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent });
            Assert.Equal(Visibility.Collapsed, selectedPresenter.Visibility);
            Assert.Equal(1, textBox.Opacity);
            list.UpdateLayout();
            var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(category);
            list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Source = item });
            Assert.Equal(Visibility.Visible, selectedPresenter.Visibility);
            control.ItemTemplate = null;
            Assert.Equal("Name", list.DisplayMemberPath);
            Assert.Equal(Visibility.Collapsed, selectedPresenter.Visibility);
            Assert.Equal(1, textBox.Opacity);
        }
        finally { control.IsDropDownOpen = false; }
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    [Fact]
    public void TypingAfterPopupDismissal_ReopensFilteredSuggestions()
    {
        OnSta(() =>
        {
            var control = new AutocompleteComboBox
            {
                ItemsSource = new[] { "Mieszkanie", "Media i telefon", "Transport" }
            };
            var resources = new ResourceDictionary
            {
                Source = new Uri("/Saldo.Desktop.Wpf;component/Themes/Generic.xaml", UriKind.Relative)
            };
            control.Style = (Style)resources[typeof(AutocompleteComboBox)];
            control.ApplyTemplate();
            control.Measure(new Size(300, 40));
            control.Arrange(new Rect(0, 0, 300, 40));
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var textBox = (TextBox)control.Template.FindName("PART_EditableTextBox", control);
            var popup = (Popup)control.Template.FindName("PART_Popup", control);
            try
            {
                textBox.Text = "m";
                Assert.True(control.IsDropDownOpen);

                // StaysOpen=False dismisses the popup without changing the typed text.
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                // A detached test control keeps Popup.IsOpen coerced to false;
                // propagate dismissal explicitly without showing a native window.
                popup.GetBindingExpression(Popup.IsOpenProperty)?.UpdateSource();
                Assert.False(control.IsDropDownOpen);

                textBox.Text = "mie";
                Assert.True(control.IsDropDownOpen);
                Assert.Equal(new[] { "Mieszkanie" }, control.FilteredItemsSource!.Cast<string>());
            }
            finally
            {
                control.IsDropDownOpen = false;
            }
        });
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Autocomplete test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
