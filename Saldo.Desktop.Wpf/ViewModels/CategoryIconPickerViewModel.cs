using System.Windows.Input;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class CategoryIconPickerViewModel : LocalizedViewModelBase
{
    public const int PageSize = 60;
    private IReadOnlyList<CategoryIconItem> _results = CategoryIconCatalog.All;
    private string _searchText = string.Empty;
    private string _pageNumberText = "1";
    private CategoryIconItem? _selectedIcon;
    private int _page;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value)) return;
            OnPropertyChanged(nameof(CanClearSearch));
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool CanClearSearch => SearchText.Length > 0 || !ReferenceEquals(_results, CategoryIconCatalog.All);
    public string PageNumberText { get => _pageNumberText; set => SetField(ref _pageNumberText, value); }
    public CategoryIconItem? SelectedIcon
    {
        get => _selectedIcon;
        set { SetField(ref _selectedIcon, value); OnPropertyChanged(nameof(CanChoose)); CommandManager.InvalidateRequerySuggested(); }
    }
    public bool CanChoose => SelectedIcon is not null;
    public IReadOnlyList<CategoryIconItem> PageIcons => _results.Skip(_page * PageSize).Take(PageSize).ToArray();
    public int ResultCount => _results.Count;
    public int PageCount => Math.Max(1, (_results.Count + PageSize - 1) / PageSize);
    public int PageNumber => _page + 1;
    public bool IsEmpty => ResultCount == 0;
    public string ResultCountText => string.Format(Localization.CurrentCulture, T("IconPicker_ResultCount"), ResultCount);
    public string PageCountText => string.Format(Localization.CurrentCulture, T("IconPicker_PageCount"), PageCount);
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand GoToPageCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }

    public CategoryIconPickerViewModel(ILocalizationService localization, string? initialIconKey = null) : base(localization)
    {
        SelectedIcon = _results.FirstOrDefault(icon => icon.Key == initialIconKey);
        if (SelectedIcon is not null) _page = Array.FindIndex(_results.ToArray(), icon => icon.Key == initialIconKey) / PageSize;
        _pageNumberText = PageNumber.ToString(Localization.CurrentCulture);
        SearchCommand = new RelayCommand(() =>
        {
            _results = CategoryIconCatalog.Search(SearchText);
            _page = 0;
            SelectedIcon = null;
            Refresh();
        });
        ClearSearchCommand = new RelayCommand(() =>
        {
            SearchText = string.Empty;
            SearchCommand.Execute(null);
        }, () => CanClearSearch);
        GoToPageCommand = new RelayCommand(() =>
        {
            if (int.TryParse(PageNumberText, out var page))
            {
                var target = Math.Clamp(page, 1, PageCount) - 1;
                if (target != _page)
                {
                    _page = target;
                    SelectedIcon = null;
                    Refresh();
                    return;
                }
            }
            PageNumberText = PageNumber.ToString(Localization.CurrentCulture);
        });
        PreviousCommand = new RelayCommand(() => { _page--; SelectedIcon = null; Refresh(); }, () => _page > 0);
        NextCommand = new RelayCommand(() => { _page++; SelectedIcon = null; Refresh(); }, () => _page + 1 < PageCount);
    }

    private void Refresh()
    {
        PageNumberText = PageNumber.ToString(Localization.CurrentCulture);
        foreach (var property in new[] { nameof(PageIcons), nameof(ResultCount), nameof(PageNumber), nameof(PageCount), nameof(ResultCountText), nameof(PageCountText), nameof(IsEmpty), nameof(CanClearSearch) })
            OnPropertyChanged(property);
        CommandManager.InvalidateRequerySuggested();
    }
    protected override void OnCultureChanged()
    {
        OnPropertyChanged(nameof(ResultCountText));
        OnPropertyChanged(nameof(PageCountText));
    }
}
