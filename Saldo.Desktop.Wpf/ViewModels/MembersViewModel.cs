using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;
using Saldo.Desktop.Wpf.Services;
using Saldo.Domain.Entities;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class MembersViewModel : ReferenceListViewModel<Member>
{
    public MembersViewModel(IServiceScopeFactory scopeFactory, IDialogService dialogService)
        : base(scopeFactory, dialogService) { }

    protected override string EntityDisplayName => "Member";

    protected override Task<IReadOnlyList<Member>> GetAllAsync(IServiceScope scope, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<IMemberRepository>().GetAllAsync(ct);

    protected override string GetName(Member item) => item.Name;

    protected override Task AddCoreAsync(IServiceScope scope, string name, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<IMemberRepository>().AddAsync(new Member { Name = name }, ct);

    protected override async Task UpdateCoreAsync(IServiceScope scope, Member item, string name, CancellationToken ct)
    {
        item.Name = name;
        await scope.ServiceProvider.GetRequiredService<IMemberRepository>().UpdateAsync(item, ct);
    }

    protected override Task DeleteCoreAsync(IServiceScope scope, Member item, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<IMemberRepository>().DeleteAsync(item.Id, ct);
}
