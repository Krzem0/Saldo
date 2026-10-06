using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Saldo.Desktop.Wpf.Controls;

public static partial class AmountInputBehavior
{
    // Keep the bound input separate from the formatted TextBox presentation.
    public static readonly DependencyProperty RawTextProperty = DependencyProperty.RegisterAttached(
        "RawText", typeof(string), typeof(AmountInputBehavior),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => { if (d is TextBox box && GetIsEnabled(box)) RefreshPresentation(box); }));

    public static string GetRawText(DependencyObject element) => (string)element.GetValue(RawTextProperty);
    public static void SetRawText(DependencyObject element, string value) => element.SetValue(RawTextProperty, value);

    private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached(
        "IsUpdating", typeof(bool), typeof(AmountInputBehavior), new PropertyMetadata(false));
    private static readonly DependencyProperty IsEditingProperty = DependencyProperty.RegisterAttached(
        "IsEditing", typeof(bool), typeof(AmountInputBehavior), new PropertyMetadata(false));

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
            textBox.TextChanged -= OnTextChanged;
            textBox.GotKeyboardFocus -= OnGotKeyboardFocus;
            textBox.LostKeyboardFocus -= OnLostKeyboardFocus;
            DataObject.RemovePastingHandler(textBox, OnPasting);
        }

        if ((bool)e.NewValue)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.TextChanged += OnTextChanged;
            textBox.GotKeyboardFocus += OnGotKeyboardFocus;
            textBox.LostKeyboardFocus += OnLostKeyboardFocus;
            DataObject.AddPastingHandler(textBox, OnPasting);
            textBox.SetValue(IsEditingProperty, textBox.IsKeyboardFocusWithin);
            RefreshPresentation(textBox);
        }
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var textBox = (TextBox)sender;
        if (!(bool)textBox.GetValue(IsUpdatingProperty))
            textBox.SetCurrentValue(RawTextProperty, textBox.Text);
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var textBox = (TextBox)sender;
        textBox.SetValue(IsEditingProperty, true);
        var raw = GetRawText(textBox);
        SetPresentation(textBox, decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            ? amount.ToString("0.##", CultureInfo.CurrentCulture) : raw);
        textBox.CaretIndex = textBox.Text.Length;
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var textBox = (TextBox)sender;
        textBox.SetValue(IsEditingProperty, false);
        RefreshPresentation(textBox);
    }

    private static void RefreshPresentation(TextBox textBox)
    {
        var raw = GetRawText(textBox);
        var formatted = !(bool)textBox.GetValue(IsEditingProperty)
            && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
                ? amount.ToString("N2", CultureInfo.CurrentCulture) : raw;
        SetPresentation(textBox, formatted);
    }

    private static void SetPresentation(TextBox textBox, string text)
    {
        if (textBox.Text == text) return;
        textBox.SetValue(IsUpdatingProperty, true);
        try { textBox.SetCurrentValue(TextBox.TextProperty, text); }
        finally { textBox.SetValue(IsUpdatingProperty, false); }
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
