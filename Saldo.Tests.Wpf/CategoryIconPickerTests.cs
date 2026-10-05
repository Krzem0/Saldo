using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Tests.Wpf;

public sealed class CategoryIconPickerTests
{
    [Fact]
    public void ClearSearch_RestoresAllIconsAndResetsPageImmediately()
    {
        var vm = new CategoryIconPickerViewModel(new LocalizationService());
        vm.SearchText = "home";
        vm.SearchCommand.Execute(null);
        vm.NextCommand.Execute(null);
        vm.SelectedIcon = vm.PageIcons[0];
        Assert.True(vm.CanClearSearch);
        vm.ClearSearchCommand.Execute(null);
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal(CategoryIconCatalog.All.Count, vm.ResultCount);
        Assert.Equal(1, vm.PageNumber);
        Assert.Equal("1", vm.PageNumberText);
        Assert.Null(vm.SelectedIcon);
        Assert.False(vm.CanClearSearch);
        // A manually erased query must still offer clearing an already-applied filter.
        vm.SearchText = "home";
        vm.SearchCommand.Execute(null);
        vm.SearchText = string.Empty;
        Assert.True(vm.CanClearSearch);
        vm.ClearSearchCommand.Execute(null);
        Assert.Equal(CategoryIconCatalog.All.Count, vm.ResultCount);
    }

    [Fact]
    public void ManualPage_UsesFilteredRangeAndNormalizesInvalidInput()
    {
        var vm = new CategoryIconPickerViewModel(new LocalizationService());
        vm.PageNumberText = "10";
        vm.GoToPageCommand.Execute(null);
        Assert.Equal(10, vm.PageNumber);
        Assert.Equal(CategoryIconCatalog.All.Skip(9 * CategoryIconPickerViewModel.PageSize).Take(CategoryIconPickerViewModel.PageSize), vm.PageIcons);
        foreach (var invalid in new[] { "", "abc", "999999999999999999999" })
        {
            vm.PageNumberText = invalid;
            vm.GoToPageCommand.Execute(null);
            Assert.Equal(10, vm.PageNumber);
            Assert.Equal("10", vm.PageNumberText);
        }
        vm.SearchText = "home";
        vm.SearchCommand.Execute(null);
        Assert.Equal("1", vm.PageNumberText);
        vm.PageNumberText = "9999";
        vm.GoToPageCommand.Execute(null);
        Assert.Equal(vm.PageCount, vm.PageNumber);
        Assert.Equal(vm.PageCount.ToString(), vm.PageNumberText);
        Assert.False(vm.NextCommand.CanExecute(null));
        vm.PageNumberText = "-1";
        vm.GoToPageCommand.Execute(null);
        Assert.Equal(1, vm.PageNumber);
        Assert.False(vm.PreviousCommand.CanExecute(null));
    }

    [Fact]
    public void Picker_PagesThroughFullCatalogAndRestoresExistingSelection()
    {
        var last = CategoryIconCatalog.All.Last();
        var vm = new CategoryIconPickerViewModel(new LocalizationService(), last.Key);
        Assert.True(vm.ResultCount > 1000);
        Assert.Equal(last.Key, vm.SelectedIcon!.Key);
        Assert.Contains(last, vm.PageIcons);
        Assert.False(vm.NextCommand.CanExecute(null));
        vm.PreviousCommand.Execute(null);
        Assert.Null(vm.SelectedIcon);
        Assert.Equal(CategoryIconPickerViewModel.PageSize, vm.PageIcons.Count);
        vm.SearchText = "home";
        vm.SearchCommand.Execute(null);
        Assert.Equal(1, vm.PageNumber);
        Assert.Contains(vm.PageIcons, icon => icon.Key == "mdi:Home");
        Assert.True(vm.PageIcons.Count <= CategoryIconPickerViewModel.PageSize);
        Assert.False(vm.PreviousCommand.CanExecute(null));
        vm.SearchText = "this_icon_does_not_exist";
        vm.SearchCommand.Execute(null);
        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.PageIcons);
        Assert.False(vm.CanChoose);
        Assert.False(vm.NextCommand.CanExecute(null));
        vm.SearchText = string.Empty;
        vm.SearchCommand.Execute(null);
        Assert.Equal(CategoryIconCatalog.All.Count, vm.ResultCount);
    }

    [Theory]
    [InlineData("DOM", "mdi:Home")]
    [InlineData("zakupy", "mdi:Cart")]
    [InlineData("łap", "mdi:Paw")]
    [InlineData("ubiór", "mdi:TshirtCrew")]
    public void Catalog_SearchesEnglishAndCommonPolishAliases(string query, string? expected)
    {
        var found = CategoryIconCatalog.Search(query);
        if (expected is not null) Assert.Contains(found, icon => icon.Key == expected);
    }
}
