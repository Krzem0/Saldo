using System.Windows;
using System.Windows.Controls;

namespace Saldo.Desktop.Wpf.Controls;

public partial class ExistingNameSuggestions : UserControl
{
    public static readonly DependencyProperty ExistingNamesProperty = DependencyProperty.Register(
        nameof(ExistingNames), typeof(IReadOnlyList<string>), typeof(ExistingNameSuggestions),
        new PropertyMetadata(null, OnFilterChanged));

    public static readonly DependencyProperty QueryProperty = DependencyProperty.Register(
        nameof(Query), typeof(string), typeof(ExistingNameSuggestions),
        new PropertyMetadata(string.Empty, OnFilterChanged));

    public static readonly DependencyProperty HeadingProperty = DependencyProperty.Register(
        nameof(Heading), typeof(string), typeof(ExistingNameSuggestions), new PropertyMetadata(string.Empty));

    public IReadOnlyList<string>? ExistingNames
    {
        get => (IReadOnlyList<string>?)GetValue(ExistingNamesProperty);
        set => SetValue(ExistingNamesProperty, value);
    }

    public string Query
    {
        get => (string)GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    public string Heading
    {
        get => (string)GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public ExistingNameSuggestions() => InitializeComponent();

    private static void OnFilterChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        => ((ExistingNameSuggestions)sender).RefreshMatches();

    private void RefreshMatches()
    {
        var query = Query?.Trim() ?? string.Empty;
        var matches = query.Length < 3 ? Array.Empty<string>() : (ExistingNames ?? [])
            .Where(name => name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        MatchesList.ItemsSource = matches;
        Visibility = matches.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}
