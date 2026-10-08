using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
        if (_textBox is not null)
        {
            _textBox.TextChanged -= OnTextChanged;
            _textBox.PreviewKeyDown -= OnTextBoxPreviewKeyDown;
        }
        base.OnApplyTemplate();
        // DatePicker writes directly to the template text box during initialization.
        _textBox = GetTemplateChild("PART_TextBox") as TextBox;
        if (_textBox is not null)
        {
            _textBox.TextChanged += OnTextChanged;
            _textBox.PreviewKeyDown += OnTextBoxPreviewKeyDown;
            NormalizeTextBox();
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e) => NormalizeTextBox();

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == LanguageProperty && SelectedDate is { } date)
        {
            // DatePicker does not refresh the existing text when Language changes.
            SetCurrentValue(TextProperty, date.ToString(
                SelectedDateFormat == DatePickerFormat.Long ? "D" : "d", GetDateCulture()));
        }
    }

    private CultureInfo GetDateCulture()
    {
        // DatePicker uses the thread culture while Language has its default value.
        // Match that rule rather than treating WPF's default en-US as explicit.
        return DependencyPropertyHelper.GetValueSource(this, LanguageProperty).BaseValueSource == BaseValueSource.Default
            ? CultureInfo.CurrentCulture
            : Language.GetSpecificCulture();
    }

    private void OnTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_textBox is null || _textBox.IsReadOnly || IsDropDownOpen
            || Keyboard.Modifiers != ModifierKeys.None || e.Key is not (Key.Up or Key.Down)) return;

        var culture = GetDateCulture();
        var pattern = SelectedDateFormat == DatePickerFormat.Long
            ? culture.DateTimeFormat.LongDatePattern
            : culture.DateTimeFormat.ShortDatePattern;
        if (!DateTime.TryParseExact(_textBox.Text, Normalize(pattern), culture,
                DateTimeStyles.AllowWhiteSpaces, out var date)) return;

        e.Handled = true;
        var days = e.Key == Key.Up ? 1 : -1;
        if (days > 0 && date == DateTime.MaxValue.Date || days < 0 && date == DateTime.MinValue.Date) return;

        var nextDate = date.AddDays(days);
        if (DisplayDateStart.HasValue && nextDate < DisplayDateStart.Value.Date
            || DisplayDateEnd.HasValue && nextDate > DisplayDateEnd.Value.Date
            || BlackoutDates.Contains(nextDate)) return;

        var start = _textBox.SelectionStart;
        var length = _textBox.SelectionLength;
        SetCurrentValue(SelectedDateProperty, nextDate);
        start = Math.Min(start, _textBox.Text.Length);
        _textBox.Select(start, Math.Min(length, _textBox.Text.Length - start));
    }

    private void NormalizeTextBox()
    {
        if (_textBox is null) return;
        var text = Normalize(_textBox.Text);
        if (text == _textBox.Text) return;
        var start = _textBox.SelectionStart;
        var length = _textBox.SelectionLength;
        if (start == _textBox.Text.Length) start = text.Length;
        _textBox.SetCurrentValue(TextBox.TextProperty, text);
        _textBox.Select(start, length);
    }

    private static string Normalize(string text)
    {
        var normalized = text.Replace('.', '-').Replace('/', '-');
        var parts = normalized.Split('-');
        if (parts.Length == 3 && parts[0].Length is 1 or 2 && parts[1].Length is 1 or 2
            && parts[2].Length == 4 && parts.All(part => part.All(char.IsAsciiDigit)))
        {
            return $"{parts[0].PadLeft(2, '0')}-{parts[1].PadLeft(2, '0')}-{parts[2]}";
        }

        return normalized;
    }
}
