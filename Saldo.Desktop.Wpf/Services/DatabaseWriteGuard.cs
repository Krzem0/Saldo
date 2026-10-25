using System.Windows;
using System.Windows.Data;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Desktop.Wpf.Services;

/// <summary>Disables only write controls; navigation and read-only lists remain usable.</summary>
public static class DatabaseWriteGuard
{
    public static readonly DependencyProperty IsWriteActionProperty = DependencyProperty.RegisterAttached(
        "IsWriteAction", typeof(bool), typeof(DatabaseWriteGuard), new PropertyMetadata(false, OnChanged));
    public static void SetIsWriteAction(DependencyObject element, bool value) => element.SetValue(IsWriteActionProperty, value);
    public static bool GetIsWriteAction(DependencyObject element) => (bool)element.GetValue(IsWriteActionProperty);

    private static void OnChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not FrameworkElement element || args.NewValue is not true) return;
        element.Loaded += (_, _) =>
        {
            if (element.TryFindResource("AutomaticBackup") is AutomaticBackupViewModel state)
                BindingOperations.SetBinding(element, UIElement.IsEnabledProperty,
                    new Binding(nameof(AutomaticBackupViewModel.CanWrite)) { Source = state, Mode = BindingMode.OneWay });
        };
    }
}
