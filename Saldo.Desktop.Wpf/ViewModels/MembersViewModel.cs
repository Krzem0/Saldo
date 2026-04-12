using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Domain.Entities;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class PartiesViewModel : ReferenceListViewModel<Party>
{
    public PartiesViewModel(IServiceScopeFactory scopeFactory, IDialogService dialogService, ILocalizationService localization)
        : base(scopeFactory, dialogService, localization) { }

    protected override string EntityDisplayNameKey => "Entity_Party";

    protected override Task<IReadOnlyList<Party>> GetAllAsync(IServiceScope scope, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<IPartyRepository>().GetAllAsync(ct);

    protected override string GetName(Party item) => item.Name;

    protected override Task AddCoreAsync(IServiceScope scope, string name, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<IPartyRepository>().AddAsync(new Party { Name = name }, ct);

    protected override async Task UpdateCoreAsync(IServiceScope scope, Party item, string name, CancellationToken ct)
    {
        item.Name = name;
        await scope.ServiceProvider.GetRequiredService<IPartyRepository>().UpdateAsync(item, ct);
    }

    protected override Task DeleteCoreAsync(IServiceScope scope, Party item, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<IPartyRepository>().DeleteAsync(item.Id, ct);
}
