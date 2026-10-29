using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Desktop.Wpf.Views;

public partial class AddTransactionButton : UserControl
{
    public static readonly DependencyProperty IsTemplateOnlyProperty = DependencyProperty.Register(
        nameof(IsTemplateOnly), typeof(bool), typeof(AddTransactionButton), new PropertyMetadata(false));
    public bool IsTemplateOnly { get => (bool)GetValue(IsTemplateOnlyProperty); set => SetValue(IsTemplateOnlyProperty, value); }
    private TransactionTemplatePickerViewModel? Picker => (DataContext as ITransactionTemplatePickerHost)?.TemplatePicker;
    public AddTransactionButton() => InitializeComponent();

    private void OnPopupOpened(object? sender, EventArgs e) => SearchBox.Focus();
    private void ClearSearch_Click(object sender, RoutedEventArgs e) => SearchBox.Focus();

    private void OnTemplateMouseDown(object sender, MouseButtonEventArgs e)
    {
        // ListBoxItem focuses itself on mouse-down and scrolls clipped rows into view.
        // Keep the row under the pointer until mouse-up commits the template.
        if (ItemsControl.ContainerFromElement(TemplateList, e.OriginalSource as DependencyObject) is ListBoxItem)
            e.Handled = true;
    }

    private void OnTemplateClicked(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(TemplateList, e.OriginalSource as DependencyObject) is not ListBoxItem item)
            return;
        TemplateList.SelectedItem = item.Content;
        UseSelectedTemplate();
        e.Handled = true;
    }

    private void OnPickerKeyDown(object sender, KeyEventArgs e)
    {
        if (Picker is not { } vm) return;
        if (e.Key == Key.Escape)
        {
            vm.IsTemplatePickerOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            UseSelectedTemplate();
            e.Handled = true;
        }
        else if (e.Key == Key.Down && SearchBox.IsKeyboardFocusWithin && TemplateList.Items.Count > 0)
        {
            TemplateList.SelectedIndex = 0;
            TemplateList.UpdateLayout();
            (TemplateList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
    }

    private void UseSelectedTemplate()
    {
        if (Picker is { } vm && vm.UseTemplateCommand.CanExecute(null))
            vm.UseTemplateCommand.Execute(null);
    }
}
