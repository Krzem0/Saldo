using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Saldo.Desktop.Wpf.Controls;

namespace Saldo.Tests.Wpf;

public sealed class ExistingNameSuggestionsTests
{
    [Fact]
    public void Typing_FiltersFromThreeCharactersAndHidesAfterClearing() => OnSta(() =>
    {
        var input = new TextBox();
        var suggestions = new ExistingNameSuggestions
        {
            ExistingNames = ["Anna Kowalska", "Jan Kowalski", "Transport"]
        };
        suggestions.SetBinding(ExistingNameSuggestions.QueryProperty,
            new Binding(nameof(TextBox.Text)) { Source = input });
        var matches = (ItemsControl)suggestions.FindName("MatchesList");
        var noMatches = (TextBlock)suggestions.FindName("NoMatchesText");

        foreach (var query in new[] { "", "k", "ko", "  ko  " })
        {
            input.Text = query;
            Assert.Equal(Visibility.Collapsed, suggestions.Visibility);
            Assert.Empty(matches.Items);
            Assert.Equal(Visibility.Collapsed, noMatches.Visibility);
        }

        input.Text = "  KOW  ";
        Assert.Equal(Visibility.Visible, suggestions.Visibility);
        Assert.Equal(Visibility.Collapsed, noMatches.Visibility);
        Assert.Equal(new[] { "Anna Kowalska", "Jan Kowalski" }, matches.Items.Cast<string>());

        input.Text = "kowalSki";
        Assert.Equal("Jan Kowalski", Assert.Single(matches.Items.Cast<string>()));

        input.Text = "nie istnieje";
        Assert.Equal(Visibility.Visible, suggestions.Visibility);
        Assert.Equal(Visibility.Visible, noMatches.Visibility);
        Assert.Empty(matches.Items);

        input.Text = "kow";
        input.Text = "";
        Assert.Equal(Visibility.Collapsed, suggestions.Visibility);
        Assert.Equal(Visibility.Collapsed, noMatches.Visibility);
        Assert.Empty(matches.Items);
    });

    [Fact]
    public void PastedText_ShowsNoMatchesAndSwitchesBackToResults() => OnSta(() =>
    {
        var input = new TextBox();
        var suggestions = new ExistingNameSuggestions
        {
            ExistingNames = ["Transport"],
            EmptyMessage = "Brak podobnych wpisów."
        };
        suggestions.SetBinding(ExistingNameSuggestions.QueryProperty,
            new Binding(nameof(TextBox.Text)) { Source = input });
        var message = (TextBlock)suggestions.FindName("NoMatchesText");
        var results = (ScrollViewer)suggestions.FindName("MatchesScroll");

        // Insert an entire string in one edit, as when pasting into the name field.
        input.SelectedText = "Nowy płatnik";
        Assert.Equal(Visibility.Visible, suggestions.Visibility);
        Assert.Equal(Visibility.Visible, message.Visibility);
        Assert.Equal("Brak podobnych wpisów.", message.Text);
        Assert.Equal(Visibility.Collapsed, results.Visibility);

        input.SelectAll();
        input.SelectedText = "TRA";
        Assert.Equal(Visibility.Visible, results.Visibility);
        Assert.Equal(Visibility.Collapsed, message.Visibility);

        suggestions.ExistingNames = [];
        Assert.Equal(Visibility.Visible, message.Visibility);
        Assert.Equal(Visibility.Collapsed, results.Visibility);
    });

    [Fact]
    public void Names_RefreshForCurrentQueryAndIncludeAllMatches() => OnSta(() =>
    {
        var suggestions = new ExistingNameSuggestions { Query = "dom" };
        Assert.Equal(Visibility.Collapsed, suggestions.Visibility);
        var names = Enumerable.Range(1, 20).Select(i => $"Zakupy domowe {i:00}").ToArray();
        suggestions.ExistingNames = names;
        var matches = (ItemsControl)suggestions.FindName("MatchesList");
        Assert.Equal(names, matches.Items.Cast<string>());
        Assert.Equal(Visibility.Visible, suggestions.Visibility);

        suggestions.ExistingNames = ["DOM", "dom", "Praca"];
        Assert.Equal("DOM", Assert.Single(matches.Items.Cast<string>()));
        suggestions.ExistingNames = null;
        Assert.Equal(Visibility.Collapsed, suggestions.Visibility);
        Assert.Empty(matches.Items);
    });

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
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Name suggestions test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
