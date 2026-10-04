using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Domain.Entities;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class CategoriesViewModel : ReferenceListViewModel<Category>
{
    public CategoriesViewModel(IServiceScopeFactory scopeFactory, IDialogService dialogService, ILocalizationService localization)
        : base(scopeFactory, dialogService, localization) { }

    protected override string EntityDisplayNameKey => "Entity_Category";

    protected override Task<IReadOnlyList<Category>> GetAllAsync(IServiceScope scope, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<ICategoryRepository>().GetAllAsync(ct);

    protected override string GetName(Category item) => item.Name;

    protected override ReferenceItemInput? ShowAddDialog()
    {
        var result = DialogService.ShowCategoryDialog(string.Format(System.Globalization.CultureInfo.CurrentCulture, T("AddEntityTitleTemplate"), EntityDisplayName));
        return result is null ? null : new ReferenceItemInput(result.Name, result.ColorCode);
    }

    protected override ReferenceItemInput? ShowEditDialog(Category item)
    {
        var result = DialogService.ShowCategoryDialog(string.Format(System.Globalization.CultureInfo.CurrentCulture, T("EditEntityTitleTemplate"), EntityDisplayName), item.Name, item.ColorCode);
        return result is null ? null : new ReferenceItemInput(result.Name, result.ColorCode);
    }

    protected override Task AddCoreAsync(IServiceScope scope, string name, string? colorCode, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<AddCategory>().ExecuteAsync(name, colorCode, ct);

    protected override Task UpdateCoreAsync(IServiceScope scope, Category item, string name, string? colorCode, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<EditCategory>().ExecuteAsync(item.Id, name, colorCode, ct);

    protected override Task DeleteCoreAsync(IServiceScope scope, Category item, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<ICategoryRepository>().DeleteAsync(item.Id, ct);
}
