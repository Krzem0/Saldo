using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Domain.Entities;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class LocationsViewModel : ReferenceListViewModel<Location>
{
    public LocationsViewModel(IServiceScopeFactory scopeFactory, IDialogService dialogService, ILocalizationService localization)
        : base(scopeFactory, dialogService, localization) { }

    protected override string EntityDisplayNameKey => "Entity_Location";

    protected override Task<IReadOnlyList<Location>> GetAllAsync(IServiceScope scope, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<ILocationRepository>().GetAllAsync(ct);

    protected override string GetName(Location item) => item.Name;

    protected override Task AddCoreAsync(IServiceScope scope, string name, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<AddLocation>().ExecuteAsync(name, ct);

    protected override async Task UpdateCoreAsync(IServiceScope scope, Location item, string name, CancellationToken ct)
    {
        item.Name = name;
        await scope.ServiceProvider.GetRequiredService<ILocationRepository>().UpdateAsync(item, ct);
    }

    protected override Task DeleteCoreAsync(IServiceScope scope, Location item, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<ILocationRepository>().DeleteAsync(item.Id, ct);
}
