using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Controls;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Tests.Wpf;

public sealed class TransactionTemplatesViewTests
{
    [Fact]
    public void TemplateForm_EscapeDismissesSuggestionsBeforeRequestingFormCancellation() => OnSta(() =>
    {
        using var harness = new TransactionTemplatesTests.Harness("pl-PL");
        var window = (Window)Load("TransactionTemplateDialog.xaml", "LightTheme");
        window.DataContext = harness.Form();
        var surface = (FrameworkElement)window.Content;
        Layout(surface, 520, 900);
        var selector = Descendants(surface).OfType<AutocompleteComboBox>().First();
        var textBox = (TextBox)selector.Template.FindName("PART_EditableTextBox", selector);
        // Use the window's declared event so this catches a regression to preview-phase cancellation.
        var xaml = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Views", "TransactionTemplateDialog.xaml"));
        var eventName = xaml.Root!.Attributes().Single(a => a.Name.LocalName is "KeyDown" or "PreviewKeyDown").Name.LocalName;
        var cancelEvent = EventManager.GetRoutedEventsForOwner(typeof(UIElement)).Single(e => e.Name == eventName);
        var cancellationRequests = 0;
        window.AddHandler(cancelEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Key != Key.Escape) return;
            cancellationRequests++;
            e.Handled = true;
        }));
        using var source = new HwndSource(new HwndSourceParameters("Template Escape verification")
            { Width = 1, Height = 1, WindowStyle = 0 });
        void Escape()
        {
            var input = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            textBox.RaiseEvent(input);
            input.RoutedEvent = Keyboard.KeyDownEvent;
            textBox.RaiseEvent(input);
            Assert.True(input.Handled);
        }
        try
        {
            textBox.Text = "Jedzenie";
            Assert.True(selector.IsDropDownOpen);
            Escape();
            Assert.False(selector.IsDropDownOpen);
            Assert.Equal(0, cancellationRequests);
            Assert.Equal("Jedzenie", textBox.Text);
            Escape();
            Assert.Equal(1, cancellationRequests);
        }
        finally { selector.IsDropDownOpen = false; window.Close(); }
    });

    [Theory]
    [InlineData("LightTheme")]
    [InlineData("DarkTheme")]
    public void PageAndForm_LoadRenderAndBindSelections(string theme) => OnSta(() =>
    {
        using var harness = new TransactionTemplatesTests.Harness("pl-PL");
        var form = harness.Form();
        form.Name = "Jedzenie do pracy — Społem"; form.Description = "Zakupy";
        form.SelectedCategory = harness.Category; form.SelectedCounterparty = harness.Party;
        form.SelectedLocation = harness.Location; form.Tags[0].IsSelected = true;
        var closed = false;
        form.RequestClose += value => closed = value;
        form.SaveAsync().GetAwaiter().GetResult();
        Assert.True(closed);

        var pageModel = new TransactionTemplatesViewModel(harness.Scopes, harness.Dialogs, harness.Localization);
        pageModel.LoadAsync().GetAwaiter().GetResult();
        var page = (UserControl)Load("TransactionTemplatesView.xaml", theme);
        page.DataContext = pageModel;
        Layout(page, 900, 560);
        var list = Descendants(page).OfType<ListBox>().Single();
        Assert.Same(pageModel.SelectedItem, list.SelectedItem);
        Assert.Contains(Descendants(page).OfType<TextBlock>(), block => block.Text == "Jedzenie do pracy — Społem");
        SavePreview(page, theme + "-templates", 900, 560);

        var search = Descendants(page).OfType<TextBox>().Single();
        var selectedId = pageModel.SelectedItem!.Template.Id;
        search.Text = "  PRACY  ";
        Layout(page, 900, 560);
        Assert.Single(list.Items);
        Assert.Equal(selectedId, pageModel.SelectedItem!.Template.Id);
        search.Text = "brak";
        Layout(page, 900, 560);
        Assert.Empty(list.Items);
        Assert.Null(pageModel.SelectedItem);
        Assert.False(pageModel.EditCommand.CanExecute(null));
        Assert.False(pageModel.DeleteCommand.CanExecute(null));
        Assert.Contains(Descendants(page).OfType<TextBlock>(), block => block.Text == "Brak pasujących szablonów." && block.Visibility == Visibility.Visible);
        pageModel.LoadAsync().GetAwaiter().GetResult();
        Layout(page, 900, 560);
        Assert.Empty(list.Items);
        var clearSearch = Descendants(page).OfType<Button>().Single(b => ReferenceEquals(b.Command, pageModel.ClearSearchCommand));
        Assert.True(clearSearch.Command.CanExecute(null));
        clearSearch.Command.Execute(null);
        Layout(page, 900, 560);
        Assert.Empty(search.Text);
        Assert.False(clearSearch.Command.CanExecute(null));
        Assert.Single(list.Items);
        Assert.Same(pageModel.SelectedItem, list.SelectedItem);

        var dialog = (Window)Load("TransactionTemplateDialog.xaml", theme);
        var edit = harness.Form(pageModel.SelectedItem!.Template);
        dialog.DataContext = edit;
        var content = (FrameworkElement)dialog.Content;
        dialog.Content = null;
        var surface = new Border { Child = content, Resources = dialog.Resources,
            Background = (Brush)dialog.Resources["SurfaceBrush"], DataContext = edit };
        Layout(surface, 520, 710);
        var selectors = Descendants(surface).OfType<AutocompleteComboBox>().ToArray();
        Assert.Equal(4, selectors.Length);
        Assert.Contains(selectors, combo => ReferenceEquals(combo.SelectedItem, harness.Category));
        Assert.Contains(selectors, combo => ReferenceEquals(combo.SelectedItem, harness.Location));
        Assert.DoesNotContain(Descendants(surface).OfType<TextBlock>(), block => block.Text.StartsWith("Saldo."));
        Assert.Contains(Descendants(surface).OfType<TextBlock>(), block => block.Text == "Wydatek");
        Assert.Contains(selectors, selector => selector.Text == "Olsztyn");
        var switches = Descendants(surface).OfType<RadioButton>().ToArray();
        Assert.Equal(2, switches.Length);
        Assert.True(switches[0].IsChecked);
        switches[1].IsChecked = true;
        Assert.True(edit.IsIncome);
        Assert.False(edit.IsExpense);
        var categorySelector = selectors.Single(selector => ReferenceEquals(selector.SelectedItem, harness.Category));
        Assert.NotNull(categorySelector.ItemTemplate);
        var badge = Assert.Single(Descendants(categorySelector).OfType<CategoryBadge>());
        Assert.Equal(harness.Category.ColorCode, badge.ColorCode);
        Assert.Equal(harness.Category.IconKey, badge.IconKey);
        var amountBox = Descendants(surface).OfType<TextBox>().Single(box => AmountInputBehavior.GetIsEnabled(box));
        Assert.Equal(edit.AmountText, AmountInputBehavior.GetRawText(amountBox));
        Assert.True(Descendants(surface).OfType<Button>().Count(button => Equals(button.Content, "+")) >= 5);
        var locationSelector = selectors.Single(selector => ReferenceEquals(selector.SelectedItem, harness.Location));
        locationSelector.SetCurrentValue(AutocompleteComboBox.TextProperty, "Olsz");
        Assert.Equal("Olsz", edit.LocationText);
        Assert.Same(harness.Location, Assert.Single(locationSelector.FilteredItemsSource!.Cast<object>()));
        locationSelector.IsDropDownOpen = false;
        edit.ClearLocationCommand.Execute(null);
        Assert.Null(edit.SelectedLocation);
        Assert.Empty(edit.LocationText);
        var tag = Descendants(surface).OfType<CheckBox>().Single();
        Assert.True(tag.IsChecked);
        tag.IsChecked = false;
        Assert.False(edit.Tags[0].IsSelected);
        Assert.True(edit.HasUnsavedChanges);
        Assert.DoesNotContain(Descendants(surface).OfType<TextBlock>(), block => block.Text == "Data");
        SavePreview(surface, theme + "-template-form", 520, 710);
        dialog.Close();
    });

    [Theory]
    [InlineData("LightTheme")]
    [InlineData("DarkTheme")]
    public void SplitButtonAndPicker_RenderWithSearchAndBoundedList(string theme) => OnSta(() =>
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var form = h.Form();
        form.Name = "Zakupy — Społem"; form.Description = "Zakupy"; form.SelectedCategory = h.Category;
        form.SaveAsync().GetAwaiter().GetResult();
        var vm = new TransactionListViewModel(h.Scopes, h.Dialogs, h.Localization);
        vm.OpenTemplatePickerAsync().GetAwaiter().GetResult();
        vm.IsTemplatePickerOpen = false;
        var control = (UserControl)Load("AddTransactionButton.xaml", theme);
        control.DataContext = vm;
        Layout(control, 230, 40);
        var buttons = Descendants(control).OfType<Button>().ToArray();
        Assert.Equal(2, buttons.Length);
        Assert.Same(vm.AddCommand, buttons[0].Command);
        Assert.Same(vm.OpenTemplatePickerCommand, buttons[1].Command);
        SavePreview(control, theme + "-add-button", 230, 40);
        var popup = (System.Windows.Controls.Primitives.Popup)control.FindName("TemplatePopup");
        var surface = (FrameworkElement)popup.Child;
        surface.Resources = control.Resources;
        Layout(surface, 320, 180);
        Assert.Same(vm.TemplatePicker, surface.DataContext);
        var list = Descendants(surface).OfType<ListBox>().Single();
        Assert.Equal(280, list.MaxHeight);
        Assert.Equal("Zakupy — Społem", Assert.Single(list.Items.Cast<Saldo.Application.DTOs.TransactionTemplateDto>()).Name);
        SavePreview(surface, theme + "-template-picker", 320, 180);
        var search = Descendants(surface).OfType<TextBox>().Single();
        search.Text = "brak";
        Layout(surface, 320, 180);
        Assert.Empty(list.Items);
        Assert.Contains(Descendants(surface).OfType<TextBlock>(), block => block.Text == "Brak pasujących szablonów.");
        var clearSearch = Descendants(surface).OfType<Button>().Single();
        Assert.Same(vm.TemplatePicker.ClearSearchCommand, clearSearch.Command);
        Assert.True(clearSearch.IsEnabled);
        clearSearch.Command.Execute(null);
        Layout(surface, 320, 180);
        Assert.Empty(search.Text);
        Assert.Single(list.Items);
        Assert.False(clearSearch.Command.CanExecute(null));
    });

    [Theory]
    [InlineData("LightTheme")]
    [InlineData("DarkTheme")]
    public void TransactionForm_ShowsSaveAsTemplateBetweenClearAndSave(string theme) => OnSta(() =>
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var vm = new AddEditTransactionViewModel(h.Scopes, h.Dialogs, h.Localization,
            [h.Category], [h.Party], [h.Location], [h.Tag]);
        var window = (Window)Load("AddEditTransactionDialog.xaml", theme);
        var content = (FrameworkElement)window.Content;
        window.Content = null;
        var surface = new Border { Child = content, Resources = window.Resources,
            Background = (Brush)window.Resources["SurfaceBrush"], DataContext = vm };
        Layout(surface, 520, 900);
        var buttons = Descendants(surface).OfType<Button>().ToArray();
        var picker = Descendants(surface).OfType<UserControl>().Single();
        Assert.Single(Descendants(picker).OfType<Button>(), b => b.Visibility == Visibility.Visible);
        var template = buttons.Single(b => ReferenceEquals(b.Command, vm.SaveAsTemplateCommand));
        var clear = buttons.Single(b => ReferenceEquals(b.Command, vm.ClearCommand));
        var save = buttons.Single(b => ReferenceEquals(b.Command, vm.SaveCommand));
        Assert.Equal("Zapisz jako szablon", template.Content);
        Assert.Equal(Visibility.Visible, template.Visibility);
        Assert.True(clear.TranslatePoint(new Point(), surface).X + clear.ActualWidth <= template.TranslatePoint(new Point(), surface).X);
        Assert.True(template.TranslatePoint(new Point(), surface).X + template.ActualWidth <= save.TranslatePoint(new Point(), surface).X);
        vm.AmountText = "12,5";
        vm.Description = "Zakupy";
        Layout(surface, 520, 900);
        var clearAmount = buttons.Single(b => ReferenceEquals(b.Command, vm.ClearAmountCommand));
        var clearDescription = buttons.Single(b => ReferenceEquals(b.Command, vm.ClearDescriptionCommand));
        Assert.True(clearAmount.Command.CanExecute(null));
        Assert.True(clearDescription.Command.CanExecute(null));
        Assert.Equal(Visibility.Visible, clearAmount.Visibility);
        Assert.Equal(Visibility.Visible, clearDescription.Visibility);
        Assert.Equal("✕", clearAmount.Content);
        Assert.Equal(new Thickness(0), clearAmount.BorderThickness);
        Assert.Equal(14, clearAmount.FontSize);
        Assert.Equal(28, clearAmount.Width);
        var clearSurface = Assert.IsType<Border>(clearAmount.Template.FindName("ClearBorder", clearAmount));
        Assert.Equal(Colors.Transparent, Assert.IsType<SolidColorBrush>(clearSurface.Background).Color);
        foreach (var trigger in clearAmount.Template.Triggers.OfType<Trigger>())
            Assert.All(trigger.Setters.OfType<Setter>(), setter => Assert.Equal(Border.BackgroundProperty, setter.Property));
        var amountBox = Descendants(surface).OfType<TextBox>().Single(b => AmountInputBehavior.GetIsEnabled(b));
        Assert.Equal(VerticalAlignment.Center, amountBox.VerticalContentAlignment);
        Assert.Equal(HorizontalAlignment.Left, clearAmount.HorizontalAlignment);
        Assert.Equal(new Thickness(32, 0, 4, 0), amountBox.Padding);
        var descriptionBox = Descendants(surface).OfType<TextBox>().Single(b => b.Text == "Zakupy");
        Assert.Equal(VerticalAlignment.Center, descriptionBox.VerticalContentAlignment);
        SavePreview(surface, theme + "-transaction-clear-fields", 520, 900);
        clearAmount.Command.Execute(null);
        clearDescription.Command.Execute(null);
        Layout(surface, 520, 900);
        Assert.Empty(vm.AmountText);
        Assert.Empty(vm.Description!);
        Assert.Empty(AmountInputBehavior.GetRawText(Descendants(surface).OfType<TextBox>().Single(b => AmountInputBehavior.GetIsEnabled(b))));
        Assert.False(clearAmount.Command.CanExecute(null));
        Assert.False(clearDescription.Command.CanExecute(null));
        CommandManager.InvalidateRequerySuggested();
        Layout(surface, 520, 900);
        Assert.Equal(Visibility.Collapsed, clearAmount.Visibility);
        Assert.Equal(Visibility.Collapsed, clearDescription.Visibility);
        SavePreview(surface, theme + "-transaction-template-button", 520, 900);
        window.Close();
    });

    [Theory]
    [InlineData("LightTheme")]
    [InlineData("DarkTheme")]
    public void DictionarySearch_RendersAndFiltersVisibleRows(string theme) => OnSta(() =>
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var vm = new CategoriesViewModel(h.Scopes, h.Dialogs, h.Localization);
        vm.LoadCommand.Execute(null);
        while (vm.IsLoading) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var view = (UserControl)Load("ReferenceListView.xaml", theme);
        view.DataContext = vm;
        Layout(view, 900, 560);
        var search = Descendants(view).OfType<TextBox>().Single();
        var list = Descendants(view).OfType<ListBox>().Single();
        Assert.Single(list.Items);
        search.Text = "PRACY";
        Layout(view, 900, 560);
        Assert.Single(list.Items);
        Assert.Same(vm.SelectedItem, list.SelectedItem);
        SavePreview(view, theme + "-dictionary-search", 900, 560);
        search.Text = "brak";
        Layout(view, 900, 560);
        Assert.Empty(list.Items);
        Assert.Null(list.SelectedItem);
        Assert.Contains(Descendants(view).OfType<TextBlock>(), text => text.Text == "Brak pasujących pozycji." && text.Visibility == Visibility.Visible);
        var clearSearch = Descendants(view).OfType<Button>().Single(b => ReferenceEquals(b.Command, vm.ClearSearchCommand));
        Assert.True(clearSearch.Command.CanExecute(null));
        clearSearch.Command.Execute(null);
        Layout(view, 900, 560);
        Assert.Empty(search.Text);
        Assert.False(clearSearch.Command.CanExecute(null));
        Assert.Single(list.Items);
    });

    [Theory]
    [InlineData("LightTheme")]
    [InlineData("DarkTheme")]
    public void BackupMessageDialog_UsesThemeAndExistingCloseButton(string theme) => OnSta(() =>
    {
        var window = (Window)Load("MessageDialog.xaml", theme);
        var content = (FrameworkElement)window.Content;
        window.Content = null;
        var message = "Kopia zapasowa została utworzona.\nC:\\backups\\saldo.db";
        var surface = new Border { Child = content, Resources = window.Resources,
            Background = (Brush)window.Resources["SurfaceBrush"], DataContext = new { Message = message } };
        Layout(surface, 480, 200);
        Assert.Contains(Descendants(surface).OfType<TextBlock>(), text => text.Text == message && text.TextWrapping == TextWrapping.Wrap);
        var close = Assert.Single(Descendants(surface).OfType<Button>());
        Assert.Equal("Zamknij", close.Content);
        Assert.True(close.IsDefault);
        Assert.True(close.IsCancel);
        SavePreview(surface, theme + "-backup-message", 480, 200);
        window.Close();
    });

    private static object Load(string file, string theme)
    {
        _ = new FrameworkElement();
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var source = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Views", file));
        var root = source.Root!;
        // Inline the reusable control for loose-XAML tests; the compiled control normally resolves app-level resources.
        foreach (var picker in root.Descendants().Where(e => e.Name.LocalName == "AddTransactionButton").ToArray())
        {
            var definition = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Views", "AddTransactionButton.xaml")).Root!;
            var inline = new XElement(p + "UserControl",
                definition.Attributes().Where(a => a.IsNamespaceDeclaration).Select(a => new XAttribute(a)),
                definition.Elements().Select(e => new XElement(e)));
            foreach (var attribute in picker.Attributes().Where(a => a.Name.LocalName != "IsTemplateOnly"))
                inline.SetAttributeValue(attribute.Name, attribute.Value);
            inline.AddFirst(new XElement(p + "UserControl.Tag",
                new XElement(XName.Get("Boolean", "clr-namespace:System;assembly=mscorlib"), "True")));
            foreach (var attribute in inline.Descendants().Attributes().Where(a => a.Value.Contains("{Binding IsTemplateOnly,")))
                attribute.Value = attribute.Value.Replace("{Binding IsTemplateOnly,", "{Binding Tag,");
            picker.ReplaceWith(inline);
        }
        root.Attribute(x + "Class")!.Remove();
        foreach (var attribute in root.DescendantsAndSelf().Attributes().Where(a => a.Name.LocalName is "Closing" or "PreviewKeyDown" or "KeyDown" or "Click" or "Opened" or "PreviewMouseLeftButtonUp").ToArray()) attribute.Remove();
        var controls = "clr-namespace:Saldo.Desktop.Wpf.Controls";
        foreach (var element in root.Descendants().Where(e => e.Name.NamespaceName == controls).ToArray())
            element.Name = XName.Get(element.Name.LocalName, controls + ";assembly=Saldo.Desktop.Wpf");
        foreach (var attribute in root.Descendants().Attributes().Where(a => a.Name.NamespaceName == controls).ToArray())
        {
            attribute.Parent!.SetAttributeValue(XName.Get(attribute.Name.LocalName, controls + ";assembly=Saldo.Desktop.Wpf"), attribute.Value);
            attribute.Remove();
        }
        root.SetAttributeValue(XNamespace.Xmlns + "controls", controls + ";assembly=Saldo.Desktop.Wpf");
        var views = "clr-namespace:Saldo.Desktop.Wpf.Views";
        foreach (var element in root.Descendants().Where(e => e.Name.NamespaceName == views).ToArray())
            element.Name = XName.Get(element.Name.LocalName, views + ";assembly=Saldo.Desktop.Wpf");
        root.SetAttributeValue(XNamespace.Xmlns + "views", views + ";assembly=Saldo.Desktop.Wpf");
        var localization = "clr-namespace:Saldo.Desktop.Wpf.Localization;assembly=Saldo.Desktop.Wpf";
        root.SetAttributeValue(XNamespace.Xmlns + "localization", localization);
        var resourceProperty = root.Element(p + (root.Name.LocalName + ".Resources"));
        if (resourceProperty is null) { resourceProperty = new XElement(p + (root.Name.LocalName + ".Resources")); root.AddFirst(resourceProperty); }
        var dictionary = resourceProperty.Element(p + "ResourceDictionary");
        if (dictionary is null)
        {
            var entries = resourceProperty.Elements().ToArray();
            foreach (var entry in entries) entry.Remove();
            dictionary = new XElement(p + "ResourceDictionary", entries);
            resourceProperty.Add(dictionary);
        }
        var application = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Views", "App.xaml"));
        var merged = dictionary.Element(p + "ResourceDictionary.MergedDictionaries");
        if (merged is null) { merged = new XElement(p + "ResourceDictionary.MergedDictionaries"); dictionary.AddFirst(merged); }
        merged.AddFirst(new XElement(application.Root!.Element(p + "Application.Resources")!.Element(p + "ResourceDictionary")!));
        merged.AddAfterSelf(new XElement(XName.Get("LocalizationService", localization), new XAttribute(x + "Key", "Localization")));
        var parsed = XamlReader.Parse(source.ToString(SaveOptions.DisableFormatting), new ParserContext
        {
            BaseUri = new Uri("pack://application:,,,/Saldo.Desktop.Wpf;component/Views/" + file)
        });
        var resources = ((FrameworkElement)parsed).Resources;
        resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Application.LoadComponent(
            new Uri($"/Saldo.Desktop.Wpf;component/Themes/{theme}.xaml", UriKind.Relative)));
        return parsed;
    }
    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static void SavePreview(FrameworkElement element, string name, int width, int height)
    {
        var folder = Environment.GetEnvironmentVariable("SALDO_TEMPLATE_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(folder)) return;
        Directory.CreateDirectory(folder);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png")); encoder.Save(stream);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); } catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
