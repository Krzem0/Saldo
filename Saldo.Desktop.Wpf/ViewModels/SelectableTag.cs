using Saldo.Desktop.Wpf.Infrastructure;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class SelectableTag(int id, string name, string? colorCode = null, string? iconKey = null) : ViewModelBase
{
    private bool _isSelected;
    public int Id { get; } = id;
    public string Name { get; } = name;
    public string? ColorCode { get; } = colorCode;
    public string? IconKey { get; } = iconKey;
    public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }
}
