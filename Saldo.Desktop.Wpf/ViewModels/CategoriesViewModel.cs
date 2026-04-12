using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;
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

    protected override Task AddCoreAsync(IServiceScope scope, string name, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<ICategoryRepository>().AddAsync(new Category { Name = name }, ct);

    protected override async Task UpdateCoreAsync(IServiceScope scope, Category item, string name, CancellationToken ct)
    {
        item.Name = name;
        await scope.ServiceProvider.GetRequiredService<ICategoryRepository>().UpdateAsync(item, ct);
    }

    protected override Task DeleteCoreAsync(IServiceScope scope, Category item, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<ICategoryRepository>().DeleteAsync(item.Id, ct);
}
