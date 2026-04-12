using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Domain.Entities;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class CounterpartiesViewModel : ReferenceListViewModel<Counterparty>
{
 public CounterpartiesViewModel(IServiceScopeFactory scopeFactory, IDialogService dialogService, ILocalizationService localization)
        : base(scopeFactory, dialogService, localization) { }

  protected override string EntityDisplayNameKey => "Entity_Counterparty";

    protected override Task<IReadOnlyList<Counterparty>> GetAllAsync(IServiceScope scope, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<ICounterpartyRepository>().GetAllAsync(ct);

    protected override string GetName(Counterparty item) => item.Name;

    protected override Task AddCoreAsync(IServiceScope scope, string name, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<ICounterpartyRepository>().AddAsync(new Counterparty { Name = name }, ct);

    protected override async Task UpdateCoreAsync(IServiceScope scope, Counterparty item, string name, CancellationToken ct)
    {
        item.Name = name;
        await scope.ServiceProvider.GetRequiredService<ICounterpartyRepository>().UpdateAsync(item, ct);
    }

    protected override Task DeleteCoreAsync(IServiceScope scope, Counterparty item, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<ICounterpartyRepository>().DeleteAsync(item.Id, ct);
}
