using System.ComponentModel;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Saldo.Desktop.Wpf.Controls;

namespace Saldo.Tests.Wpf;

public sealed class AmountInputBehaviorTests
{
    [Theory]
    [InlineData("pl-PL", "25", "25")]
    [InlineData("pl-PL", "1234,5", "1234,5")]
    [InlineData("en-US", "1234.5", "1234.5")]
    public void FocusChanges_FormatPresentationWithoutChangingBoundAmount(string culture, string raw, string editable)
        => OnSta(() =>
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var state = new AmountState { Raw = raw };
            var box = CreateBox(state);
            var amount = decimal.Parse(raw, CultureInfo.CurrentCulture);
            Assert.Equal(amount.ToString("N2", CultureInfo.CurrentCulture), box.Text);
            Focus(box, true);
            Assert.Equal(editable, box.Text);
            Focus(box, false);
            Assert.Equal(raw, state.Raw);
            Assert.Equal(0, state.Changes);
            Assert.Equal(amount, decimal.Parse(state.Raw, NumberStyles.Number, CultureInfo.CurrentCulture));
            Assert.DoesNotContain("zł", state.Raw);
        });

    [Fact]
    public void TypingFractionThenBlurring_PreservesRawInputAndAllowsReset() => OnSta(() =>
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
        var state = new AmountState();
        var box = CreateBox(state);
        Assert.Empty(box.Text);
        Focus(box, true);
        box.Text = "25,";
        Assert.Equal("25,", box.Text);
        box.Text = "25,5";
        Focus(box, false);
        Assert.Equal("25,50", box.Text);
        Assert.Equal("25,5", state.Raw);
        state.Raw = string.Empty;
        Assert.Empty(box.Text);
        Assert.NotNull(BindingOperations.GetBindingExpression(box, AmountInputBehavior.RawTextProperty));
    });

    [Fact]
    public void FocusedInput_StillRejectsLettersAndCurrencyPaste() => OnSta(() =>
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
        var box = CreateBox(new AmountState { Raw = "25" });
        Focus(box, true);
        var input = new TextCompositionEventArgs(Keyboard.PrimaryDevice,
            new TextComposition(InputManager.Current, box, "a"))
        { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
        box.RaiseEvent(input);
        Assert.True(input.Handled);
        var paste = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, " zł"), false, DataFormats.UnicodeText)
        { RoutedEvent = DataObject.PastingEvent };
        box.RaiseEvent(paste);
        Assert.True(paste.CommandCancelled);
    });

    private static TextBox CreateBox(AmountState state)
    {
        var box = new TextBox();
        box.SetBinding(AmountInputBehavior.RawTextProperty, new Binding(nameof(AmountState.Raw))
        { Source = state, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        AmountInputBehavior.SetIsEnabled(box, true);
        state.Changes = 0;
        return box;
    }

    private static void Focus(TextBox box, bool focused) => box.RaiseEvent(
        new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, focused ? null : box, focused ? box : null)
        { RoutedEvent = focused ? Keyboard.GotKeyboardFocusEvent : Keyboard.LostKeyboardFocusEvent });

    private sealed class AmountState : INotifyPropertyChanged
    {
        private string _raw = string.Empty;
        public int Changes { get; set; }
        public string Raw
        {
            get => _raw;
            set { if (_raw == value) return; _raw = value; Changes++; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Raw))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
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
}
