using System.Windows;
using System.Windows.Controls;

namespace Saldo.Desktop.Wpf.Controls;

public sealed class HyphenDatePicker : DatePicker
{
    private TextBox? _textBox;
    static HyphenDatePicker()
    {
        // Normalize before DatePicker compares and parses its text, so committing
        // a date cannot restore the culture's original separator.
        TextProperty.OverrideMetadata(typeof(HyphenDatePicker), new FrameworkPropertyMetadata(
            string.Empty, null, (_, value) => value is string text ? Normalize(text) : value));
    }

    public override void OnApplyTemplate()
    {
        if (_textBox is not null) _textBox.TextChanged -= OnTextChanged;
        base.OnApplyTemplate();
        // DatePicker writes directly to the template text box during initialization.
        _textBox = GetTemplateChild("PART_TextBox") as TextBox;
        if (_textBox is not null)
        {
            _textBox.TextChanged += OnTextChanged;
            NormalizeTextBox();
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e) => NormalizeTextBox();

    private void NormalizeTextBox()
    {
        if (_textBox is null) return;
        var text = Normalize(_textBox.Text);
        if (text == _textBox.Text) return;
        var start = _textBox.SelectionStart;
        var length = _textBox.SelectionLength;
        _textBox.SetCurrentValue(TextBox.TextProperty, text);
        _textBox.Select(start, length);
    }

    private static string Normalize(string text) => text.Replace('.', '-').Replace('/', '-');
}
