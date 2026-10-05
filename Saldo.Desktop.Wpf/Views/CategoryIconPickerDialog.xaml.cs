using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Desktop.Wpf.Views;

public partial class CategoryIconPickerDialog : Window
{
    public CategoryIconPickerViewModel ViewModel { get; }
    public CategoryIconPickerDialog(ILocalizationService localization, string? initialKey)
    {
        InitializeComponent();
        DataContext = ViewModel = new CategoryIconPickerViewModel(localization, initialKey);
        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            if (ViewModel.SelectedIcon is not null) IconsList.ScrollIntoView(ViewModel.SelectedIcon);
        };
        SourceInitialized += (_, _) =>
        {
            if (System.Windows.Application.Current.Resources["ThemeService"] is IThemeService theme)
                ThemeService.ApplyWindowTitleBarTheme(this, theme.SelectedTheme);
        };
    }
    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedIcon is not null) DialogResult = true;
    }
    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        ViewModel.SearchCommand.Execute(null);
        e.Handled = true;
    }
    private void ClearSearch_Click(object sender, RoutedEventArgs e) => SearchBox.Focus();
    private void PageNumberBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        ViewModel.GoToPageCommand.Execute(null);
        PageNumberBox.SelectAll();
        e.Handled = true;
    }
    private void PageNumberBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => ViewModel.GoToPageCommand.Execute(null);
    private void Icons_TargetUpdated(object sender, DataTransferEventArgs e)
    {
        // Each new page/search starts at the top rather than retaining the old scroll offset.
        FindScrollViewer(IconsList)?.ScrollToTop();
    }
    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer scroll) return scroll;
            if (FindScrollViewer(child) is { } descendant) return descendant;
        }
        return null;
    }
    private void Icons_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && System.Windows.Controls.ItemsControl.ContainerFromElement(
            (System.Windows.Controls.ListBox)sender, source) is System.Windows.Controls.ListBoxItem)
            Choose_Click(sender, e);
    }
}
