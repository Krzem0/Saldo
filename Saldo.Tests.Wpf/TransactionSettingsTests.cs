using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Entities;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Wpf;

public sealed class TransactionSettingsTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(null)]
    public void Settings_LoadSaveAndReload_UsesPartyIds(int? selectedId)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var settings = new MemorySettings();
                var self = new Party { Id = 1, Name = "Marcin" };
                var parties = new FakePartyRepository([self, new Party { Id = 2, Name = "Adam" }]);
                using var provider = new ServiceCollection()
                    .AddSingleton<ITransactionSettingsRepository>(settings)
                    .AddSingleton<IPartyRepository>(parties)
                    .AddScoped<GetTransactionSettings>().AddScoped<SetDefaultPayer>().BuildServiceProvider();
                var vm = new SettingsViewModel(new LocalizationService(), null!,
                    provider.GetRequiredService<IServiceScopeFactory>(), null!,
                    NullLogger<SettingsViewModel>.Instance, null!);
                Assert.False(vm.SaveTransactionSettingsCommand.CanExecute(null));
                vm.LoadTransactionSettingsAsync().GetAwaiter().GetResult();
                Assert.Equal(1, vm.SelectedDefaultPayerOption!.Id);
                Assert.Equal("Marcin", vm.SelectedDefaultPayerOption.Label);
                Assert.True(vm.SaveTransactionSettingsCommand.CanExecute(null));
                vm.SelectedDefaultPayerOption = vm.DefaultPayerOptions.Single(p => p.Id == selectedId);
                vm.SaveTransactionSettingsAsync().GetAwaiter().GetResult();
                Assert.Equal(selectedId, settings.PayerId);
                self.Name = "Renamed";
                vm.LoadTransactionSettingsAsync().GetAwaiter().GetResult();
                Assert.Equal(selectedId, vm.SelectedDefaultPayerOption!.Id);
                Assert.Contains(vm.DefaultPayerOptions, p => p.Id == 1 && p.Label == "Renamed");
            }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class MemorySettings : ITransactionSettingsRepository
    {
        public int? PayerId { get; private set; } = 1;
        public Task<Saldo.Domain.Entities.TransactionSettings> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new Saldo.Domain.Entities.TransactionSettings { DefaultPayerId = PayerId });
        public Task SaveAsync(Saldo.Domain.Entities.TransactionSettings settings, CancellationToken ct = default)
        {
            PayerId = settings.DefaultPayerId;
            return Task.CompletedTask;
        }
    }
}
