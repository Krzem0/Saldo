using System.Runtime.ExceptionServices;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Threading;
using Saldo.Desktop.Wpf.Controls;

namespace Saldo.Tests.Wpf;

public sealed class HyphenDatePickerTests
{
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
                var baseline = new DatePicker
                {
                    Language = XmlLanguage.GetLanguage(culture),
                    SelectedDateFormat = DatePickerFormat.Short,
                    SelectedDate = new DateTime(2026, 10, 7)
                };
                var initial = baseline.Text.Replace('.', '-').Replace('/', '-');
                baseline.SelectedDate = new DateTime(2026, 10, 8);
                var expected = baseline.Text.Replace('.', '-').Replace('/', '-');
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
