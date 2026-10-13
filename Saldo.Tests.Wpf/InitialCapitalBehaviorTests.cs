using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Saldo.Desktop.Wpf.Controls;

namespace Saldo.Tests.Wpf;

public sealed class InitialCapitalBehaviorTests
{
    [Fact]
    public void Typing_CapitalizesOncePerFocusAndAllowsLowercaseCorrection() => OnSta(() =>
    {
        var box = CreateBox();
        Focus(box);
        Type(box, "ż");
        Assert.Equal("Ż", box.Text);
        Type(box, "ywność");
        Assert.Equal("Żywność", box.Text);
        box.Select(0, 1);
        Type(box, "ż");
        Assert.Equal("żywność", box.Text);
        Focus(box);
        Assert.Equal("żywność", box.Text);
        box.Select(0, 1);
        Type(box, "ż");
        Assert.Equal("Żywność", box.Text);
    });

    [Theory]
    [InlineData("zakupy", "Zakupy")]
    [InlineData("łódź", "Łódź")]
    [InlineData("7 zakupów", "7 zakupów")]
    [InlineData(" zakupy", " zakupy")]
    [InlineData("", "")]
    public void Paste_CapitalizesOnlyInitialLetterAndAllowsLaterCorrection(string text, string expected) => OnSta(() =>
    {
        var box = CreateBox();
        Focus(box);
        Paste(box, text);
        Assert.Equal(expected, box.Text);
        if (text.Length == 0 || !char.IsLetter(text, 0)) return;
        box.Select(0, 1);
        Type(box, text[..1]);
        Assert.Equal(text, box.Text);
        box.SelectAll();
        Paste(box, text);
        Assert.Equal(expected, box.Text);
    });

    [Fact]
    public void EditingMiddleAndLoadingExistingText_DoNotChangeInitialOrBreakBinding() => OnSta(() =>
    {
        var state = new DescriptionState { Description = "mały opis" };
        var box = CreateBox();
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(DescriptionState.Description))
        { Source = state, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        Focus(box);
        Assert.Equal("mały opis", box.Text);
        box.Select(5, 4);
        Paste(box, "tekst");
        Assert.Equal("mały tekst", box.Text);
        Assert.Equal(box.Text, state.Description);
        box.SelectAll();
        Paste(box, "nowy opis");
        Assert.Equal("Nowy opis", state.Description);
        box.Select(0, 1);
        Type(box, "n");
        Assert.Equal("nowy opis", state.Description);
        Assert.NotNull(BindingOperations.GetBindingExpression(box, TextBox.TextProperty));
        InitialCapitalBehavior.SetIsEnabled(box, false);
        Focus(box);
        box.SelectAll();
        Type(box, "opis");
        Assert.Equal("opis", state.Description);
    });

    private static TextBox CreateBox()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
        var box = new TextBox();
        InitialCapitalBehavior.SetIsEnabled(box, true);
        return box;
    }

    private static void Focus(TextBox box) => box.RaiseEvent(
        new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, null, box)
        { RoutedEvent = Keyboard.GotKeyboardFocusEvent });

    private static void Type(TextBox box, string text)
    {
        var input = new TextCompositionEventArgs(Keyboard.PrimaryDevice,
            new TextComposition(InputManager.Current, box, text))
        { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
        box.RaiseEvent(input);
        if (!input.Handled) Insert(box, text);
    }

    private static void Paste(TextBox box, string text)
    {
        var paste = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, text), false, DataFormats.UnicodeText)
        { RoutedEvent = DataObject.PastingEvent };
        box.RaiseEvent(paste);
        Assert.False(paste.CommandCancelled);
        Insert(box, (string)paste.DataObject.GetData(paste.FormatToApply)!);
    }

    private static void Insert(TextBox box, string text)
    {
        var caret = box.SelectionStart + text.Length;
        box.SelectedText = text;
        box.Select(caret, 0);
    }

    public sealed class DescriptionState
    {
        public string Description { get; set; } = string.Empty;
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
