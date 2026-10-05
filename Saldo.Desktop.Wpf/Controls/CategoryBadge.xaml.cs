using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.IconPacks;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.Controls;

public partial class CategoryBadge : UserControl
{
    public static readonly DependencyProperty NameTextProperty = DependencyProperty.Register(nameof(NameText), typeof(string), typeof(CategoryBadge));
    public static readonly DependencyProperty ColorCodeProperty = DependencyProperty.Register(nameof(ColorCode), typeof(string), typeof(CategoryBadge));
    public static readonly DependencyProperty IconKeyProperty = DependencyProperty.Register(nameof(IconKey), typeof(string), typeof(CategoryBadge), new PropertyMetadata(null, OnIconChanged));
    public string? NameText { get => (string?)GetValue(NameTextProperty); set => SetValue(NameTextProperty, value); }
    public string? ColorCode { get => (string?)GetValue(ColorCodeProperty); set => SetValue(ColorCodeProperty, value); }
    public string? IconKey { get => (string?)GetValue(IconKeyProperty); set => SetValue(IconKeyProperty, value); }
    public CategoryBadge()
    {
        InitializeComponent();
        SetResourceReference(TagProperty, "ControlBrush");
        UpdateIcon();
    }
    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((CategoryBadge)d).UpdateIcon();
    private void UpdateIcon()
    {
        if (IconTile is not null) IconTile.Visibility = CategoryIconCatalog.Resolve(IconKey) == PackIconMaterialKind.None ? Visibility.Collapsed : Visibility.Visible;
    }
}
