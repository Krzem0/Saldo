using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Saldo.Desktop.Wpf.Controls;

public static class InitialCapitalBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(InitialCapitalBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty HasHandledInitialProperty = DependencyProperty.RegisterAttached(
        "HasHandledInitial", typeof(bool), typeof(InitialCapitalBehavior), new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox box) return;
        if ((bool)e.OldValue)
        {
            box.GotKeyboardFocus -= OnGotKeyboardFocus;
            box.PreviewTextInput -= OnPreviewTextInput;
            DataObject.RemovePastingHandler(box, OnPasting);
        }
        if ((bool)e.NewValue)
        {
            box.SetValue(HasHandledInitialProperty, false);
            box.GotKeyboardFocus += OnGotKeyboardFocus;
            box.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(box, OnPasting);
        }
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => ((TextBox)sender).SetValue(HasHandledInitialProperty, false);

    private static bool StartsWithLetter(string text) => text.Length > 0 && char.IsLetter(text, 0);

    private static string Capitalize(string text)
    {
        var initial = StringInfo.GetNextTextElement(text);
        return CultureInfo.CurrentCulture.TextInfo.ToUpper(initial) + text[initial.Length..];
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var box = (TextBox)sender;
        if (e.Handled || box.IsReadOnly || box.SelectionStart != 0
            || (bool)box.GetValue(HasHandledInitialProperty) || !StartsWithLetter(e.Text)) return;

        // Consume the first initial even when it is already uppercase, so a later edit can use lowercase.
        box.SetValue(HasHandledInitialProperty, true);
        var text = Capitalize(e.Text);
        if (text == e.Text) return;
        e.Handled = true;
        box.SelectedText = text;
        box.CaretIndex = text.Length;
        box.SelectionLength = 0;
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        var box = (TextBox)sender;
        if (e.CommandCancelled || box.IsReadOnly || box.SelectionStart != 0
            || e.DataObject.GetData(e.FormatToApply) is not string text || !StartsWithLetter(text)) return;

        box.SetValue(HasHandledInitialProperty, true);
        var capitalized = Capitalize(text);
        if (capitalized == text) return;
        // Let TextBox perform the paste normally, preserving its selection, binding and undo behavior.
        e.DataObject = new DataObject(DataFormats.UnicodeText, capitalized);
        e.FormatToApply = DataFormats.UnicodeText;
    }
}
