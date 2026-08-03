namespace Saldo.Desktop.Wpf.Services;

public enum AppearanceTheme
{
    System,
    Light,
    Dark
}

public sealed record AppearanceThemeOption(AppearanceTheme Theme, string Label);
