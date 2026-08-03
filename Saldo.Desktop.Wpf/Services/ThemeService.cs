using System.ComponentModel;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace Saldo.Desktop.Wpf.Services;

public sealed class ThemeService : IThemeService, IDisposable
{
    private const string ThemeDictionaryMarker = "SaldoThemeDictionary";
    private AppearanceTheme _selectedTheme = AppearanceTheme.System;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        ApplyTheme();
    }

    public AppearanceTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme == value)
            {
                return;
            }

            _selectedTheme = value;
            ApplyTheme();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTheme)));
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (SelectedTheme != AppearanceTheme.System)
        {
            return;
        }

        System.Windows.Application.Current.Dispatcher.Invoke(ApplyTheme);
    }

    private void ApplyTheme()
    {
        var theme = SelectedTheme == AppearanceTheme.System
            ? GetSystemTheme()
            : SelectedTheme;
        var source = theme == AppearanceTheme.Dark
            ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
            : new Uri("Themes/LightTheme.xaml", UriKind.Relative);
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary => Equals(dictionary[ThemeDictionaryMarker], true));

        if (existing is not null)
        {
            dictionaries.Remove(existing);
        }

        var dictionary = new ResourceDictionary { Source = source };
        dictionary[ThemeDictionaryMarker] = true;
        dictionaries.Insert(0, dictionary);

        ApplyWindowTitleBarTheme(System.Windows.Application.Current.MainWindow, theme);
    }

    public static void ApplyWindowTitleBarTheme(Window? window, AppearanceTheme selectedTheme)
    {
        if (window is null)
        {
            return;
        }

        var effectiveTheme = selectedTheme == AppearanceTheme.System
            ? GetSystemTheme()
            : selectedTheme;
        var handle = new WindowInteropHelper(window).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        var useDarkMode = effectiveTheme == AppearanceTheme.Dark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, 20, ref useDarkMode, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 19, ref useDarkMode, sizeof(int));
    }

    private static AppearanceTheme GetSystemTheme()
    {
        var value = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            1);
        return value is int { } lightTheme && lightTheme == 0
            ? AppearanceTheme.Dark
            : AppearanceTheme.Light;
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
