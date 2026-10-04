using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Tests.Wpf;

public sealed class TagChipStyleTests
{
    [Theory]
    [InlineData("LightTheme", null)]
    [InlineData("DarkTheme", null)]
    [InlineData("LightTheme", "#FFFFFF")]
    [InlineData("DarkTheme", "#000000")]
    [InlineData("LightTheme", "#BADA55")]
    [InlineData("DarkTheme", "#176B5A")]
    public void Chip_SelectDeselect_UsesColorAndKeepsBinding(string theme, string? colorCode) => OnSta(() =>
    {
        var tag = new SelectableTag(1, "Dla Iwony", colorCode);
        var chip = CreateChip(tag, theme);
        Layout(chip);
        var border = (Border)chip.Template.FindName("ChipBorder", chip);
        var label = (TextBlock)chip.Template.FindName("ChipLabel", chip);
        Assert.Equal(Colors.Transparent, ((SolidColorBrush)border.Background).Color);
        Assert.Equal(colorCode is null ? Color(chip, "ControlBorderBrush") : Parse(colorCode),
            ((SolidColorBrush)border.BorderBrush).Color);
        Assert.Equal(Color(chip, "PrimaryTextBrush"), ((SolidColorBrush)label.Foreground).Color);

        chip.IsChecked = true;
        Layout(chip);

        Assert.True(tag.IsSelected);
        Assert.Equal(colorCode is null ? Color(chip, "PrimaryBrush") : Parse(colorCode),
            ((SolidColorBrush)border.Background).Color);
        Assert.Same(border.Background, border.BorderBrush);
        Assert.True(Contrast(((SolidColorBrush)border.Background).Color, ((SolidColorBrush)label.Foreground).Color) >= 4.5);

        tag.IsSelected = false;
        Layout(chip);
        Assert.False(chip.IsChecked);
        Assert.Equal(Colors.Transparent, ((SolidColorBrush)border.Background).Color);
        Assert.Equal(Color(chip, "PrimaryTextBrush"), ((SolidColorBrush)label.Foreground).Color);
    });

    [Fact]
    public void SelectedChipWithoutColor_FollowsThemeChange() => OnSta(() =>
    {
        var chip = CreateChip(new SelectableTag(1, "Wakacje") { IsSelected = true }, "LightTheme");
        Layout(chip);
        var border = (Border)chip.Template.FindName("ChipBorder", chip);
        var before = ((SolidColorBrush)border.Background).Color;

        chip.Resources.MergedDictionaries[0] = Load("DarkTheme");
        Layout(chip);

        Assert.NotEqual(before, ((SolidColorBrush)border.Background).Color);
        Assert.Equal(Color(chip, "PrimaryBrush"), ((SolidColorBrush)border.Background).Color);
        var label = (TextBlock)chip.Template.FindName("ChipLabel", chip);
        Assert.True(Contrast(((SolidColorBrush)border.Background).Color, ((SolidColorBrush)label.Foreground).Color) >= 4.5);
    });

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("#GGGGGG")]
    public void MissingOrInvalidColor_UsesThemeFallback(string? code)
    {
        var brush = new TagColorBrushConverter().Convert([code!, Brushes.Green], typeof(Brush), null!, CultureInfo.InvariantCulture);
        Assert.Same(Brushes.Green, brush);
    }

    [Fact]
    public void ChipPreview_CanRenderBothThemesWithoutOpeningWindows() => OnSta(() =>
    {
        var previewPath = Environment.GetEnvironmentVariable("SALDO_CHIP_PREVIEW");
        if (string.IsNullOrEmpty(previewPath)) return;
        var board = new StackPanel { Width = 580 };
        foreach (var theme in new[] { "LightTheme", "DarkTheme" })
        {
            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Resources.MergedDictionaries.Add(Load(theme));
            panel.Children.Add(new TextBlock
            {
                Text = theme == "LightTheme" ? "Jasny motyw" : "Ciemny motyw", FontSize = 18,
                Foreground = (Brush)panel.FindResource("PrimaryTextBrush"), Margin = new Thickness(0, 0, 0, 12)
            });
            foreach (var selected in new[] { false, true })
            {
                var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
                foreach (var (name, code) in new (string, string?)[]
                    { ("Bez koloru", null), ("Dla Iwony", "#8E44AD"), ("Wakacje", "#F4C542"), ("Jasny", "#FFFFFF") })
                    row.Children.Add(CreateChip(new SelectableTag(1, name, code) { IsSelected = selected }, theme));
                panel.Children.Add(row);
            }
            board.Children.Add(new Border { Background = (Brush)panel.FindResource("SurfaceBrush"), Child = panel });
        }
        Layout(board);
        var bitmap = new RenderTargetBitmap((int)board.ActualWidth, (int)Math.Ceiling(board.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(board);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(previewPath);
        encoder.Save(stream);
    });

    private static CheckBox CreateChip(SelectableTag tag, string theme)
    {
        var chip = new CheckBox { DataContext = tag, Content = tag.Name };
        chip.Resources.MergedDictionaries.Add(Load(theme));
        var styles = Load("TagChipStyles");
        chip.Resources.MergedDictionaries.Add(styles);
        chip.Style = (Style)styles["TagChipStyle"];
        chip.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            new Binding(nameof(SelectableTag.IsSelected)) { Mode = BindingMode.TwoWay });
        return chip;
    }

    private static ResourceDictionary Load(string name) => new()
    {
        Source = new Uri($"/Saldo.Desktop.Wpf;component/Themes/{name}.xaml", UriKind.Relative)
    };

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(580, double.PositiveInfinity));
        element.Arrange(new Rect(new Point(), element.DesiredSize));
        element.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static System.Windows.Media.Color Color(FrameworkElement element, string key)
        => ((SolidColorBrush)element.FindResource(key)).Color;
    private static System.Windows.Media.Color Parse(string code) => (System.Windows.Media.Color)ColorConverter.ConvertFromString(code);
    private static double Contrast(System.Windows.Media.Color background, System.Windows.Media.Color text)
    {
        static double Luminance(System.Windows.Media.Color color)
        {
            static double Channel(byte value) => value / 255d <= 0.04045
                ? value / 255d / 12.92 : Math.Pow((value / 255d + 0.055) / 1.055, 2.4);
            return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        }
        var a = Luminance(background);
        var b = Luminance(text);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "WPF rendering timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
