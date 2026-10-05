using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Entities;
using Saldo.Application.DTOs;

namespace Saldo.Tests.Wpf;

public sealed class TagChipStyleTests
{
    [Theory]
    [InlineData("LightTheme", "#FFFFFF", "mdi:Account")]
    [InlineData("DarkTheme", "#000000", "mdi:Gift")]
    [InlineData("LightTheme", null, "mdi:Airplane")]
    [InlineData("DarkTheme", null, "mdi:Account")]
    [InlineData("DarkTheme", "#3366CC", "mdi:UnknownFutureIcon")]
    public void TagIcon_SelectDeselect_PreservesIdentityAndSelectionAppearance(string theme, string? color, string iconKey) => OnSta(() =>
    {
        var tag = new SelectableTag(1, "For Iwona", color, iconKey);
        var chip = CreateChip(tag, theme);
        Layout(chip);
        var icon = (MahApps.Metro.IconPacks.PackIconMaterial)chip.Template.FindName("TagIcon", chip);
        var tile = (Border)chip.Template.FindName("TagIconTile", chip);
        var dot = (Border)chip.Template.FindName("ColorDot", chip);
        var fill = (Border)chip.Template.FindName("ChipBorder", chip);
        var kind = Saldo.Desktop.Wpf.Services.CategoryIconCatalog.Resolve(iconKey);
        Assert.Equal(kind, icon.Kind);
        Assert.Equal(kind == MahApps.Metro.IconPacks.PackIconMaterialKind.None ? Visibility.Collapsed : Visibility.Visible, tile.Visibility);
        Assert.Equal(kind == MahApps.Metro.IconPacks.PackIconMaterialKind.None && color is not null ? Visibility.Visible : Visibility.Collapsed, dot.Visibility);
        Assert.Equal(Colors.Transparent, ((SolidColorBrush)fill.Background).Color);
        foreach (var selected in new[] { true, false })
        {
            chip.IsChecked = selected;
            Layout(chip);
            Assert.Equal(selected, tag.IsSelected);
            Assert.Equal(selected ? Color(chip, "PrimaryBrush") : Colors.Transparent, ((SolidColorBrush)fill.Background).Color);
            Assert.Equal(color is null ? Color(chip, "ControlBrush") : Parse(color), ((SolidColorBrush)tile.Background).Color);
            Assert.True(Contrast(((SolidColorBrush)tile.Background).Color, ((SolidColorBrush)icon.Foreground).Color) >= 4.5);
        }
        var label = CreateTagLabel(tag.Name, color, theme, iconKey);
        Layout(label);
        var labelIcon = Descendants(label).OfType<MahApps.Metro.IconPacks.PackIconMaterial>().Single();
        Assert.Equal(kind, labelIcon.Kind);
        Assert.Equal(tile.Visibility, labelIcon.Visibility);
        var labelFill = Descendants(label).OfType<Border>().Single(border => border.Name == "TagLabelBorder");
        Assert.True(Contrast(((SolidColorBrush)labelFill.Background).Color, ((SolidColorBrush)labelIcon.Foreground).Color) >= 4.5);
    });

    [Fact]
    public void CategoryBadge_PreviewRendersActualVectorIcons() => OnSta(() =>
    {
        var root = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var theme in new[] { "LightTheme", "DarkTheme" })
        {
            var panel = new StackPanel { Width = 280, Margin = new Thickness(16) };
            panel.Resources.MergedDictionaries.Add(Load(theme));
            panel.Background = (Brush)panel.FindResource("SurfaceBrush");
            panel.Children.Add(new TextBlock { Text = theme, Foreground = (Brush)panel.FindResource("PrimaryTextBrush"), Margin = new Thickness(8) });
            foreach (var (name, color, key) in new[] {
                ("Mieszkanie", "#3366CC", "mdi:Home"), ("Supermarkety", "#12956A", "mdi:Cart"),
                ("Zwierzęta", "#8E44AD", "mdi:Paw"), ("Zdrowie", "#DD3377", "mdi:HeartPulse"),
                ("Ikona bez koloru", (string?)null, "mdi:Home"), ("Sam kolor", "#F4C542", (string?)null),
                ("Bez ikony i koloru", (string?)null, (string?)null) })
            {
                var badge = new Saldo.Desktop.Wpf.Controls.CategoryBadge { NameText = name, ColorCode = color, IconKey = key, Margin = new Thickness(8, 4, 8, 4) };
                panel.Children.Add(badge);
            }
            root.Children.Add(panel);
        }
        root.Measure(new Size(640, double.PositiveInfinity));
        root.Arrange(new Rect(new Point(), root.DesiredSize));
        root.UpdateLayout();
        foreach (var icon in Descendants(root).OfType<MahApps.Metro.IconPacks.PackIconMaterial>().Where(icon => icon.Kind != MahApps.Metro.IconPacks.PackIconMaterialKind.None))
            Assert.Contains(Descendants(icon).OfType<System.Windows.Shapes.Path>(), path => path.Data is not null);
        if (Environment.GetEnvironmentVariable("SALDO_CATEGORY_PREVIEW") is { Length: > 0 } output)
        {
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = System.IO.File.Create(output);
            encoder.Save(file);
        }
    });

    [Theory]
    [InlineData("LightTheme", "#FFFFFF", "mdi:Home")]
    [InlineData("DarkTheme", "#000000", "mdi:Cart")]
    [InlineData("DarkTheme", "#3366CC", "mdi:Paw")]
    [InlineData("LightTheme", null, "mdi:Home")]
    [InlineData("DarkTheme", null, null)]
    [InlineData("LightTheme", "#3366CC", null)]
    [InlineData("DarkTheme", "#3366CC", "mdi:UnknownFutureIcon")]
    public void CategoryBadge_OptionalIconColorAndTheme_RenderWithContrast(string theme, string? color, string? iconKey) => OnSta(() =>
    {
        var badge = new Saldo.Desktop.Wpf.Controls.CategoryBadge { NameText = "Mieszkanie", ColorCode = color, IconKey = iconKey };
        badge.Resources.MergedDictionaries.Add(Load(theme));
        Layout(badge);
        var tile = (Border)badge.FindName("IconTile");
        var icon = (MahApps.Metro.IconPacks.PackIconMaterial)badge.FindName("CategoryIcon");
        var expectedKind = Saldo.Desktop.Wpf.Services.CategoryIconCatalog.Resolve(iconKey);
        Assert.Equal(expectedKind, icon.Kind);
        Assert.Equal(expectedKind == MahApps.Metro.IconPacks.PackIconMaterialKind.None ? Visibility.Collapsed : Visibility.Visible, tile.Visibility);
        Assert.Equal(color is null ? Color(badge, "ControlBrush") : Parse(color), ((SolidColorBrush)tile.Background).Color);
        Assert.True(Contrast(((SolidColorBrush)tile.Background).Color, ((SolidColorBrush)icon.Foreground).Color) >= 4.5);
        Assert.Contains(Descendants(badge).OfType<TextBlock>(), text => text.Text == "Mieszkanie");
        badge.Resources.MergedDictionaries[0] = Load(theme == "LightTheme" ? "DarkTheme" : "LightTheme");
        Layout(badge);
        Assert.Equal(color is null ? Color(badge, "ControlBrush") : Parse(color), ((SolidColorBrush)tile.Background).Color);
    });

    [Theory]
    [InlineData("LightTheme", null)]
    [InlineData("DarkTheme", null)]
    [InlineData("LightTheme", "#FFFFFF")]
    [InlineData("DarkTheme", "#000000")]
    [InlineData("LightTheme", "#8E44AD")]
    [InlineData("DarkTheme", "#F4C542")]
    public void MonthlyTagLabel_UsesCurrentColorOrNeutralThemeBackground(string theme, string? colorCode) => OnSta(() =>
    {
        var presenter = CreateTagLabel("Dla Iwony", colorCode, theme);
        Layout(presenter);
        var border = Descendants(presenter).OfType<Border>().Single(item => item.Name == "TagLabelBorder");
        var text = Descendants(presenter).OfType<TextBlock>().Single(item => item.Name == "TagLabelText");
        Assert.Equal("Dla Iwony", text.Text);
        Assert.Equal(colorCode is null ? Color(presenter, "ControlBrush") : Parse(colorCode), ((SolidColorBrush)border.Background).Color);
        Assert.True(Contrast(((SolidColorBrush)border.Background).Color, ((SolidColorBrush)text.Foreground).Color) >= 4.5);
        Assert.Empty(Descendants(presenter).OfType<CheckBox>());
        presenter.Resources.MergedDictionaries[0] = Load(theme == "LightTheme" ? "DarkTheme" : "LightTheme");
        Layout(presenter);
        Assert.Equal(colorCode is null ? Color(presenter, "ControlBrush") : Parse(colorCode), ((SolidColorBrush)border.Background).Color);
    });

    [Theory]
    [InlineData("Category", "#8E44AD")]
    [InlineData("Category", null)]
    [InlineData("Tag", "#F4C542")]
    [InlineData("Tag", null)]
    [InlineData("Party", null)]
    [InlineData("Location", null)]
    public void DictionaryTemplates_ShowOptionalColorsOnlyForColoredEntityTypes(string entityType, string? colorCode) => OnSta(() =>
    {
        foreach (var theme in new[] { "LightTheme", "DarkTheme" })
        {
            var presenter = CreateReferenceItem(entityType, colorCode, theme);
            Layout(presenter);
            var swatch = Descendants(presenter).OfType<Border>().FirstOrDefault(border => border.Name == "ColorSwatch");
            Assert.Contains(Descendants(presenter).OfType<TextBlock>(), text => text.Text == "Example");
            Assert.Null(swatch);
            if (entityType is "Category" or "Tag")
            {
                var badge = Assert.Single(Descendants(presenter).OfType<Saldo.Desktop.Wpf.Controls.CategoryBadge>());
                Assert.Equal(colorCode, badge.ColorCode);
                Assert.Equal("Example", badge.NameText);
            }
        }
    });

    [Theory]
    [InlineData("LightTheme", null)]
    [InlineData("DarkTheme", null)]
    [InlineData("LightTheme", "#FFFFFF")]
    [InlineData("DarkTheme", "#000000")]
    [InlineData("LightTheme", "#BADA55")]
    [InlineData("DarkTheme", "#176B5A")]
    public void Chip_SelectDeselect_UsesUniformAccentAndPreservesColorDot(string theme, string? colorCode) => OnSta(() =>
    {
        var tag = new SelectableTag(1, "Dla Iwony", colorCode);
        var chip = CreateChip(tag, theme);
        Layout(chip);
        var border = (Border)chip.Template.FindName("ChipBorder", chip);
        var label = (TextBlock)chip.Template.FindName("ChipLabel", chip);
        var dot = (Border)chip.Template.FindName("ColorDot", chip);
        Assert.Equal(Colors.Transparent, ((SolidColorBrush)border.Background).Color);
        Assert.Equal(Color(chip, "ControlBorderBrush"),
            ((SolidColorBrush)border.BorderBrush).Color);
        Assert.Equal(colorCode is null ? Visibility.Collapsed : Visibility.Visible, dot.Visibility);
        Assert.Equal(colorCode is null ? Colors.Transparent : Parse(colorCode), ((SolidColorBrush)dot.Background).Color);
        Assert.Equal(Color(chip, "PrimaryTextBrush"), ((SolidColorBrush)label.Foreground).Color);

        chip.IsChecked = true;
        Layout(chip);

        Assert.True(tag.IsSelected);
        Assert.Equal(Color(chip, "PrimaryBrush"),
            ((SolidColorBrush)border.Background).Color);
        Assert.Equal(colorCode is null ? Visibility.Collapsed : Visibility.Visible, dot.Visibility);
        Assert.Equal(colorCode is null ? Colors.Transparent : Parse(colorCode), ((SolidColorBrush)dot.Background).Color);
        Assert.Same(border.Background, border.BorderBrush);
        Assert.True(Contrast(((SolidColorBrush)border.Background).Color, ((SolidColorBrush)label.Foreground).Color) >= 4.5);

        tag.IsSelected = false;
        Layout(chip);
        Assert.False(chip.IsChecked);
        Assert.Equal(Colors.Transparent, ((SolidColorBrush)border.Background).Color);
        Assert.Equal(Color(chip, "PrimaryTextBrush"), ((SolidColorBrush)label.Foreground).Color);
        Assert.Equal(colorCode is null ? Colors.Transparent : Parse(colorCode), ((SolidColorBrush)dot.Background).Color);
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
                foreach (var (name, code, key) in new (string, string?, string?)[]
                    { ("Bez koloru", null, "mdi:Account"), ("Dla Iwony", "#8E44AD", "mdi:Gift"), ("Wakacje", "#F4C542", "mdi:Airplane"), ("Sam kolor", "#FFFFFF", null) })
                    row.Children.Add(CreateChip(new SelectableTag(1, name, code, key) { IsSelected = selected }, theme));
                panel.Children.Add(row);
            }
            var references = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            header.Children.Add(new TextBlock { Text = "Nazwa", Foreground = (Brush)panel.FindResource("SecondaryTextBrush") });
            references.Children.Add(header);
            foreach (var (entity, code) in new (string, string?)[]
                { ("Category", "#8E44AD"), ("Tag", "#F4C542"), ("Tag", null) })
            {
                var item = CreateReferenceItem(entity, code, theme, "mdi:Gift");
                item.Margin = new Thickness(0, 4, 0, 4);
                references.Children.Add(item);
            }
            panel.Children.Add(references);
            var monthlyTags = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            foreach (var (name, code) in new (string, string?)[]
                { ("Bez koloru", null), ("Dla Iwony", "#8E44AD"), ("Wakacje", "#F4C542") })
                monthlyTags.Children.Add(CreateTagLabel(name, code, theme, "mdi:Account"));
            panel.Children.Add(monthlyTags);
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

    private static ContentPresenter CreateReferenceItem(string entity, string? code, string theme, string? iconKey = null)
    {
        var presenter = new ContentPresenter
        {
            Content = entity switch
            {
                "Category" => (object)new Category { Name = "Example", ColorCode = code, IconKey = iconKey },
                "Tag" => new Tag { Name = "Example", ColorCode = code, IconKey = iconKey },
                "Party" => new Party { Name = "Example" },
                _ => new Location { Name = "Example" }
            }
        };
        presenter.Resources.MergedDictionaries.Add(Load(theme));
        presenter.Resources["Localization"] = new LocalizationService();
        presenter.Resources.MergedDictionaries.Add(Load("ReferenceItemTemplates"));
        presenter.SetResourceReference(TextElement.ForegroundProperty, "PrimaryTextBrush");
        return presenter;
    }

    private static ContentPresenter CreateTagLabel(string name, string? code, string theme, string? iconKey = null)
    {
        var presenter = new ContentPresenter { Content = new TransactionTagDto(1, name, code, iconKey) };
        presenter.Resources.MergedDictionaries.Add(Load(theme));
        var templates = Load("TagLabelTemplates");
        presenter.Resources.MergedDictionaries.Add(templates);
        presenter.ContentTemplate = (DataTemplate)templates["TagLabelTemplate"];
        return presenter;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
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
