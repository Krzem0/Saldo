using System.Runtime.ExceptionServices;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Threading;
using Saldo.Desktop.Wpf.Controls;
using Saldo.Desktop.Wpf.Localization;

namespace Saldo.Tests.Wpf;

public sealed class HyphenDatePickerTests
{
    [Fact]
    public void ArrowKeys_DefaultLanguageUsesPolishDateOrder() => OnSta(() =>
    {
        var picker = CreatePicker("pl-PL", setLanguage: false);
        var culture = new CultureInfo("pl-PL", false);
        culture.DateTimeFormat.ShortDatePattern = "dd.MM.yyyy";
        CultureInfo.CurrentCulture = culture;
        picker.SelectedDate = new DateTime(2019, 8, 1);
        var box = (DatePickerTextBox)picker.Template.FindName("PART_TextBox", picker);
        Assert.Equal("01-08-2019", box.Text);

        Assert.True(Press(box, Key.Up));
        Assert.Equal(new DateTime(2019, 8, 2), picker.SelectedDate);
        Assert.Equal("02-08-2019", box.Text);
        Assert.True(Press(box, Key.Down));
        Assert.Equal(new DateTime(2019, 8, 1), picker.SelectedDate);
        Assert.Equal("01-08-2019", box.Text);
    });

    [Fact]
    public void ArrowKeys_FollowLanguageChangesWithoutSwappingDayAndMonth() => OnSta(() =>
    {
        var localization = new LocalizationService { CurrentCulture = new CultureInfo("pl-PL", false) };
        var picker = CreatePicker("pl-PL", setLanguage: false);
        picker.SetBinding(FrameworkElement.LanguageProperty, new Binding("CurrentCulture.Name")
        {
            Source = localization,
            ConverterCulture = new CultureInfo("en-US", false)
        });
        picker.SelectedDate = new DateTime(2019, 8, 1);
        var box = (DatePickerTextBox)picker.Template.FindName("PART_TextBox", picker);
        Assert.Equal("01-08-2019", box.Text);
        Assert.True(Press(box, Key.Up));
        Assert.Equal(new DateTime(2019, 8, 2), picker.SelectedDate);

        localization.CurrentCulture = new CultureInfo("en-US", false);
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert.Equal("en-US", picker.Language.IetfLanguageTag, ignoreCase: true);
        Assert.Equal("08-02-2019", box.Text);
        Assert.True(Press(box, Key.Up));
        Assert.Equal(new DateTime(2019, 8, 3), picker.SelectedDate);
        Assert.Equal("08-03-2019", box.Text);

        localization.CurrentCulture = new CultureInfo("pl-PL", false);
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert.Equal("03-08-2019", box.Text);
        Assert.True(Press(box, Key.Down));
        Assert.Equal(new DateTime(2019, 8, 2), picker.SelectedDate);
        Assert.Equal("02-08-2019", box.Text);
    });

    [Theory]
    [InlineData("pl-PL", "31.12.2026", Key.Up, 2027, 1, 1)]
    [InlineData("en-US", "12/31/2026", Key.Up, 2027, 1, 1)]
    [InlineData("pl-PL", "01.03.2024", Key.Down, 2024, 2, 29)]
    [InlineData("en-US", "3/1/2024", Key.Down, 2024, 2, 29)]
    public void ArrowKeys_AdjustTypedDateAndKeepBinding(string culture, string typed, Key key, int year, int month, int day) => OnSta(() =>
    {
        var picker = CreatePicker(culture);
        var source = new DatePicker { SelectedDate = new DateTime(2026, 10, 7) };
        picker.SetBinding(DatePicker.SelectedDateProperty, new Binding(nameof(DatePicker.SelectedDate))
        {
            Source = source,
            Mode = BindingMode.TwoWay
        });
        var box = (DatePickerTextBox)picker.Template.FindName("PART_TextBox", picker);
        box.Text = typed;
        box.Select(2, 0);
        var expected = new DateTime(year, month, day);

        Assert.True(Press(box, key));
        Assert.Equal(expected, picker.SelectedDate);
        Assert.Equal(expected, source.SelectedDate);
        Assert.True(BindingOperations.IsDataBound(picker, DatePicker.SelectedDateProperty));
        Assert.Equal(2, box.CaretIndex);
        Assert.DoesNotContain('.', box.Text);
        Assert.DoesNotContain('/', box.Text);

        Assert.True(Press(box, key == Key.Up ? Key.Down : Key.Up));
        Assert.Equal(expected.AddDays(key == Key.Up ? -1 : 1), source.SelectedDate);
    });

    [Theory]
    [InlineData("")]
    [InlineData("08-10")]
    [InlineData("31-02-2026")]
    [InlineData("invalid")]
    public void ArrowKeys_DoNotReplaceIncompleteOrInvalidDate(string text) => OnSta(() =>
    {
        var picker = CreatePicker("pl-PL");
        var box = (DatePickerTextBox)picker.Template.FindName("PART_TextBox", picker);
        box.Text = text;
        Assert.False(Press(box, Key.Up));
        Assert.False(Press(box, Key.Down));
        Assert.Equal(text, box.Text);
        Assert.Equal(new DateTime(2026, 10, 7), picker.SelectedDate);
    });

    [Fact]
    public void ArrowKeys_LeaveCalendarAndOtherKeysAloneAndRespectSelectableDates() => OnSta(() =>
    {
        var picker = CreatePicker("pl-PL");
        var box = (DatePickerTextBox)picker.Template.FindName("PART_TextBox", picker);
        var initial = picker.SelectedDate;
        picker.IsDropDownOpen = true;
        Assert.False(Press(box, Key.Up));
        Assert.False(Press(box, Key.Down));
        Assert.Equal(initial, picker.SelectedDate);
        picker.IsDropDownOpen = false;
        Assert.False(Press(box, Key.Left));
        picker.DisplayDateEnd = initial;
        Assert.True(Press(box, Key.Up));
        Assert.Equal(initial, picker.SelectedDate);
        picker.DisplayDateEnd = null;
        picker.BlackoutDates.Add(new CalendarDateRange(initial!.Value.AddDays(-1)));
        Assert.True(Press(box, Key.Down));
        Assert.Equal(initial, picker.SelectedDate);
        Assert.True(Press(box, Key.Up));
        Assert.Equal(initial.Value.AddDays(1), picker.SelectedDate);
    });

    private static HyphenDatePicker CreatePicker(string culture, bool setLanguage = true)
    {
        CultureInfo.CurrentCulture = new CultureInfo(culture, false);
        var picker = new HyphenDatePicker
        {
            SelectedDateFormat = DatePickerFormat.Short,
            SelectedDate = new DateTime(2026, 10, 7),
            Template = (ControlTemplate)XamlReader.Parse("""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="DatePicker">
                    <DatePickerTextBox x:Name="PART_TextBox"/>
                </ControlTemplate>
                """)
        };
        if (setLanguage) picker.Language = XmlLanguage.GetLanguage(culture);
        picker.ApplyTemplate();
        return picker;
    }

    private static bool Press(DatePickerTextBox box, Key key)
    {
        using var source = new HwndSource(new HwndSourceParameters("Date picker keyboard verification")
        {
            Width = 1, Height = 1, WindowStyle = 0
        });
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        box.RaiseEvent(args);
        return args.Handled;
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Theory]
    [InlineData("pl-PL", "08.10.2026")]
    [InlineData("en-US", "10/8/2026")]
    public void DatePicker_UsesHyphensForSelectionAndTyping(string culture, string typed)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture, false);
                var initial = culture == "pl-PL" ? "07-10-2026" : "10-07-2026";
                var expected = culture == "pl-PL" ? "08-10-2026" : "10-08-2026";
                var picker = new HyphenDatePicker
                {
                    Language = XmlLanguage.GetLanguage(culture),
                    SelectedDateFormat = DatePickerFormat.Short,
                    SelectedDate = new DateTime(2026, 10, 7),
                    Template = (ControlTemplate)XamlReader.Parse("""
                        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="DatePicker">
                            <DatePickerTextBox x:Name="PART_TextBox"/>
                        </ControlTemplate>
                        """)
                };
                picker.ApplyTemplate();
                var box = (DatePickerTextBox)picker.Template.FindName("PART_TextBox", picker);
                Assert.Equal(initial, box.Text);
                picker.SelectedDate = new DateTime(2026, 10, 8);
                Assert.Equal(expected, box.Text);
                box.Text = typed;
                box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
                Assert.Equal(new DateTime(2026, 10, 8), picker.SelectedDate);
                Assert.Equal(expected, box.Text);
                box.Text = string.Empty;
                box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
                Assert.Null(picker.SelectedDate);
            }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
