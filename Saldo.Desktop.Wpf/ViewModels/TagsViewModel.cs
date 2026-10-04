using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Domain.Entities;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class TagsViewModel(IServiceScopeFactory scopeFactory, IDialogService dialogService, ILocalizationService localization)
    : ReferenceListViewModel<Tag>(scopeFactory, dialogService, localization)
{
    protected override string EntityDisplayNameKey => "Entity_Tag";
    public override string Title => T("Tags");
    protected override string GetName(Tag item) => item.Name;
    protected override int GetId(Tag item) => item.Id;
    public override bool HasColorColumn => true;
    protected override ReferenceItemInput? ShowAddDialog()
    {
        var result = DialogService.ShowReferenceColorDialog(
            string.Format(System.Globalization.CultureInfo.CurrentCulture, T("AddEntityTitleTemplate"), EntityDisplayName));
        return result is null ? null : new ReferenceItemInput(result.Name, result.ColorCode);
    }
    protected override ReferenceItemInput? ShowEditDialog(Tag item)
    {
        var result = DialogService.ShowReferenceColorDialog(
            string.Format(System.Globalization.CultureInfo.CurrentCulture, T("EditEntityTitleTemplate"), EntityDisplayName),
            item.Name, item.ColorCode);
        return result is null ? null : new ReferenceItemInput(result.Name, result.ColorCode);
    }
    protected override Task<IReadOnlyList<Tag>> GetAllAsync(IServiceScope scope, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<ITagRepository>().GetAllAsync(ct);
    protected override Task AddCoreAsync(IServiceScope scope, string name, string? colorCode, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<AddTag>().ExecuteAsync(name, colorCode, ct);
    protected override Task UpdateCoreAsync(IServiceScope scope, Tag item, string name, string? colorCode, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<EditTag>().ExecuteAsync(item.Id, name, colorCode, ct);
    protected override Task DeleteCoreAsync(IServiceScope scope, Tag item, CancellationToken ct)
        => scope.ServiceProvider.GetRequiredService<ITagRepository>().DeleteAsync(item.Id, ct);
}
