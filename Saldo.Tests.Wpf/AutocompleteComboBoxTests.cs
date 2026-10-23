using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Interop;
using Saldo.Desktop.Wpf.Controls;

namespace Saldo.Tests.Wpf;

public sealed class AutocompleteComboBoxTests
{
    [Fact]
    public void SuggestionItems_DoNotUseAncestorBindingsWhenFilteredOrDetached() => OnSta(() =>
    {
        using var bindings = new BindingErrorScope();
        var control = CreateKeyboardControl();
        var surface = new Border { Child = control };
        var list = (ListBox)control.Template.FindName("PART_ItemsList", control);
        try
        {
            for (var i = 0; i < 3; i++)
            {
                control.Text = i == 1 ? "m" : string.Empty;
                list.Measure(new Size(300, 200));
                list.Arrange(new Rect(0, 0, 300, 200));
                list.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.All(Descendants(list).OfType<ListBoxItem>(), item =>
                    Assert.Equal(HorizontalAlignment.Stretch, item.HorizontalContentAlignment));
                surface.Child = null;
                surface.Child = control;
            }
            control.IsDropDownOpen = false;
            surface.Child = null;
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Empty(bindings.Errors);
        }
        finally { control.IsDropDownOpen = false; }
    });

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
            Assert.True(PressKey(textBox: (TextBox)control.Template.FindName("PART_EditableTextBox", control), Key.Enter).Handled);
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

    [Fact]
    public void ArrowNavigation_PreviewsWithoutCommittingAndEnterAcceptsHighlightedSuggestion() => OnSta(() =>
    {
        var control = CreateKeyboardControl();
        var textBox = (TextBox)control.Template.FindName("PART_EditableTextBox", control);
        var list = (ListBox)control.Template.FindName("PART_ItemsList", control);
        try
        {
            textBox.Text = "m";
            Assert.True(PressKey(textBox, Key.Down).Handled);
            Assert.Equal("Mieszkanie", list.SelectedItem);
            Assert.Null(control.SelectedItem);
            Assert.Equal("m", control.Text);
            Assert.True(PressKey(textBox, Key.Down).Handled);
            Assert.Equal("Media", list.SelectedItem);
            Assert.True(PressKey(textBox, Key.Enter).Handled);
            Assert.Equal("Media", control.SelectedItem);
            Assert.Equal("Media", textBox.Text);
            Assert.False(control.IsDropDownOpen);
            Assert.False(PressKey(textBox, Key.Enter).Handled);
        }
        finally { control.IsDropDownOpen = false; }
    });

    [Theory]
    [InlineData("miesz", "Mieszkanie", false)]
    [InlineData("unknown", null, true)]
    [InlineData("m", null, true)]
    public void Enter_ConsumesOpenListAndAcceptsOnlyUnambiguousOrHighlightedMatch(string query, string? expected, bool staysOpen) => OnSta(() =>
    {
        var control = CreateKeyboardControl();
        var textBox = (TextBox)control.Template.FindName("PART_EditableTextBox", control);
        try
        {
            textBox.Text = query;
            Assert.True(PressKey(textBox, Key.Enter).Handled);
            Assert.Equal(expected, control.SelectedItem);
            Assert.Equal(expected ?? query, textBox.Text);
            Assert.Equal(staysOpen, control.IsDropDownOpen);
        }
        finally { control.IsDropDownOpen = false; }
    });

    [Fact]
    public void ListFocus_EnterCommitsAndEscapeDismissesWithoutCommitting() => OnSta(() =>
    {
        var control = CreateKeyboardControl();
        var textBox = (TextBox)control.Template.FindName("PART_EditableTextBox", control);
        var list = (ListBox)control.Template.FindName("PART_ItemsList", control);
        try
        {
            control.IsDropDownOpen = true;
            Assert.True(PressKey(list, Key.Up).Handled);
            Assert.Equal("Transport", list.SelectedItem);
            Assert.True(PressKey(list, Key.Escape).Handled);
            Assert.False(control.IsDropDownOpen);
            Assert.Null(control.SelectedItem);
            textBox.Text = "m";
            Assert.Equal(-1, list.SelectedIndex);
            Assert.True(PressKey(list, Key.Down).Handled);
            Assert.True(PressKey(list, Key.Enter).Handled);
            Assert.Equal("Mieszkanie", control.SelectedItem);
            Assert.False(control.IsDropDownOpen);
        }
        finally { control.IsDropDownOpen = false; }
    });

    private static AutocompleteComboBox CreateKeyboardControl()
    {
        var control = new AutocompleteComboBox { ItemsSource = new[] { "Mieszkanie", "Media", "Transport" } };
        var resources = new ResourceDictionary
            { Source = new Uri("/Saldo.Desktop.Wpf;component/Themes/Generic.xaml", UriKind.Relative) };
        control.Style = (Style)resources[typeof(AutocompleteComboBox)];
        control.ApplyTemplate();
        return control;
    }

    [Theory]
    [InlineData("m", true)]
    [InlineData("unknown", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void ClickingAfterDismissal_ReopensSuggestionsForExistingText(string query, bool shouldOpen) => OnSta(() =>
    {
        var control = CreateKeyboardControl();
        var textBox = (TextBox)control.Template.FindName("PART_EditableTextBox", control);
        var list = (ListBox)control.Template.FindName("PART_ItemsList", control);
        var window = new Window { Content = control, Width = 300, Height = 80,
            Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            textBox.Focus();
            textBox.Text = query;
            control.IsDropDownOpen = false;
            var matches = control.FilteredItemsSource!.Cast<string>().ToArray();
            textBox.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent });
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Equal(shouldOpen, control.IsDropDownOpen);
            Assert.Equal(query, textBox.Text);
            Assert.Null(control.SelectedItem);
            Assert.Equal(matches, control.FilteredItemsSource!.Cast<string>());
            Assert.Equal(-1, list.SelectedIndex);
            if (shouldOpen)
            {
                Assert.True(PressKey(textBox, Key.Escape).Handled);
                Assert.False(control.IsDropDownOpen);
                textBox.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    { RoutedEvent = Mouse.PreviewMouseDownEvent });
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.True(control.IsDropDownOpen);
            }
        }
        finally { control.IsDropDownOpen = false; window.Close(); }
    });

    [Fact]
    public void ClickingSelectedBadge_OpensAfterFocusAndAllowsEditing() => OnSta(() =>
    {
        var control = CreateKeyboardControl();
        control.ItemTemplate = (DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBlock Text="{Binding}"/>
            </DataTemplate>
            """);
        control.SelectedItem = "Mieszkanie";
        var textBox = (TextBox)control.Template.FindName("PART_EditableTextBox", control);
        var outside = new TextBox();
        var panel = new StackPanel();
        panel.Children.Add(control);
        panel.Children.Add(outside);
        var window = new Window { Content = panel, Width = 300, Height = 120,
            Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            outside.Focus();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.False(control.IsTextEditing);
            textBox.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent });
            // The preview event must allow TextBox to finish focusing before opening the popup.
            Assert.False(control.IsDropDownOpen);
            textBox.Focus();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.True(textBox.IsKeyboardFocused);
            Assert.True(control.IsTextEditing);
            Assert.Equal(1, textBox.Opacity);
            Assert.True(control.IsDropDownOpen);
            Assert.Equal("Mieszkanie", textBox.Text);
            textBox.Text = "medi";
            Assert.Null(control.SelectedItem);
            Assert.Equal(new[] { "Media" }, control.FilteredItemsSource!.Cast<string>());
            Assert.True(PressKey(textBox, Key.Enter).Handled);
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Equal("Media", control.SelectedItem);
            Assert.False(control.IsDropDownOpen);
        }
        finally { control.IsDropDownOpen = false; window.Close(); }
    });

    private static KeyEventArgs PressKey(UIElement textBox, Key key)
    {
        using var source = new HwndSource(new HwndSourceParameters("Autocomplete keyboard verification")
            { Width = 1, Height = 1, WindowStyle = 0 });
        var input = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        textBox.RaiseEvent(input);
        return input;
    }

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
