using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Saldo.Desktop.Wpf.Controls;

public static partial class AmountInputBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(AmountInputBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        if ((bool)e.OldValue)
        {
            textBox.PreviewTextInput -= OnPreviewTextInput;
            DataObject.RemovePastingHandler(textBox, OnPasting);
        }

        if ((bool)e.NewValue)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(textBox, OnPasting);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            e.Handled = !IsValidAmount(GetCandidateText(textBox, e.Text));
        }
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || !e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
        if (!IsValidAmount(GetCandidateText(textBox, pastedText)))
        {
            e.CancelCommand();
        }
    }

    private static string GetCandidateText(TextBox textBox, string insertedText) =>
        textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
            .Insert(textBox.SelectionStart, insertedText);

    private static bool IsValidAmount(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        var separator = Regex.Escape(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        return new Regex($"^\\d*(?:{separator}\\d{{0,2}})?$").IsMatch(value);
    }
}
