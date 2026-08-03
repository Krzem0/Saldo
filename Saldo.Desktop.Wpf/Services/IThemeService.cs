using System.ComponentModel;

namespace Saldo.Desktop.Wpf.Services;

public interface IThemeService : INotifyPropertyChanged
{
    AppearanceTheme SelectedTheme { get; set; }
}
