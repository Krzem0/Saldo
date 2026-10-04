using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Forms = System.Windows.Forms;
using DrawingColor = System.Drawing.Color;

namespace Saldo.Desktop.Wpf.Views;

public partial class ReferenceColorDialog : Window
{
    private readonly ILocalizationService _localization;
    private string? _selectedColorCode;

    public string EnteredName { get; set; }

    public ReferenceColorDialog(string title, string? initialName, string? initialColorCode)
    {
        InitializeComponent();
        Title = title;
        EnteredName = initialName ?? string.Empty;

        _localization = (ILocalizationService)System.Windows.Application.Current.Resources["Localization"];
        _selectedColorCode = initialColorCode;

        DataContext = this;
        UpdateColorPreview();
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public ReferenceColorDialogResult Result => new(EnteredName, _selectedColorCode);

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current.Resources["ThemeService"] is IThemeService themeService)
        {
            ThemeService.ApplyWindowTitleBarTheme(this, themeService.SelectedTheme);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EnteredName))
        {
            NameErrorText.Visibility = Visibility.Visible;
            return;
        }

        DialogResult = true;
    }

    private void NameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameErrorText.Visibility = Visibility.Collapsed;
        }
    }

    private void ChooseColor_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = string.IsNullOrWhiteSpace(_selectedColorCode)
                ? DrawingColor.White
                : System.Drawing.ColorTranslator.FromHtml(_selectedColorCode)
        };

        // WinForms requires an HWND owner; use this reference dialog so the native
        // picker stays above it, disables it while open, and returns activation to it.
        var owner = new NativeWindowOwner(new WindowInteropHelper(this).EnsureHandle());
        if (dialog.ShowDialog(owner) != Forms.DialogResult.OK)
        {
            return;
        }

        _selectedColorCode = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        UpdateColorPreview();
    }

    private void ClearColor_Click(object sender, RoutedEventArgs e)
    {
        _selectedColorCode = null;
        UpdateColorPreview();
    }

    private void UpdateColorPreview()
    {
        ColorPreview.Background = string.IsNullOrWhiteSpace(_selectedColorCode)
            ? Brushes.Transparent
            : CreateBrush(_selectedColorCode);
        ColorCodeText.Text = _selectedColorCode ?? _localization["ReferenceColorNone"];
        ClearColorButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedColorCode);
    }

    private static Brush CreateBrush(string colorCode) => (Brush)new BrushConverter().ConvertFromString(colorCode)!;

    private sealed class NativeWindowOwner(IntPtr handle) : Forms.IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }
}
