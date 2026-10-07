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
                var location = new Location { Id = 2, Name = "Home" };
                using var provider = new ServiceCollection()
                    .AddSingleton<ITransactionSettingsRepository>(settings)
                    .AddSingleton<IPartyRepository>(parties)
                    .AddSingleton<ILocationRepository>(new FakeLocationRepository([location]))
                    .AddScoped<GetTransactionSettings>().AddScoped<SetTransactionDefaults>().BuildServiceProvider();
                var vm = new SettingsViewModel(new LocalizationService(), null!,
                    provider.GetRequiredService<IServiceScopeFactory>(), null!,
                    NullLogger<SettingsViewModel>.Instance, null!);
                vm.LoadTransactionSettingsAsync().GetAwaiter().GetResult();
                Assert.Equal(1, vm.SelectedDefaultPayerOption!.Id);
                Assert.Equal("Marcin", vm.SelectedDefaultPayerOption.Label);
                Assert.Equal(0, settings.SaveCount);
                Assert.Null(vm.SelectedDefaultLocationOption);
                Assert.False(vm.ClearDefaultLocationCommand.CanExecute(null));
                Assert.Single(vm.DefaultLocationOptions);
                vm.SelectedDefaultLocationOption = vm.DefaultLocationOptions[0];
                vm.TransactionSettingsSaveTask.GetAwaiter().GetResult();
                Assert.Equal(2, settings.LocationId);
                Assert.Equal(2, vm.DefaultPayerOptions.Count);
                Assert.True(vm.ClearDefaultPayerCommand.CanExecute(null));
                if (selectedId is null)
                {
                    vm.ClearDefaultPayerCommand.Execute(null);
                    Assert.Null(vm.SelectedDefaultPayerOption);
                    Assert.False(vm.ClearDefaultPayerCommand.CanExecute(null));
                    vm.ClearDefaultLocationCommand.Execute(null);
                    Assert.Null(vm.SelectedDefaultLocationOption);
                    Assert.False(vm.ClearDefaultLocationCommand.CanExecute(null));
                }
                else
                {
                    vm.SelectedDefaultPayerOption = vm.DefaultPayerOptions.Single(p => p.Id == selectedId);
                }
                vm.TransactionSettingsSaveTask.GetAwaiter().GetResult();
                Assert.Equal(selectedId, settings.PayerId);
                Assert.Equal(selectedId, settings.LocationId);
                self.Name = "Renamed";
                location.Name = "Renamed location";
                vm.LoadTransactionSettingsAsync().GetAwaiter().GetResult();
                Assert.Equal(selectedId, vm.SelectedDefaultPayerOption?.Id);
                Assert.Contains(vm.DefaultPayerOptions, p => p.Id == 1 && p.Label == "Renamed");
                Assert.Equal(selectedId, vm.SelectedDefaultLocationOption?.Id);
                Assert.Contains(vm.DefaultLocationOptions, item => item.Id == 2 && item.Label == "Renamed location");
                var saveCount = settings.SaveCount;
                vm.LoadTransactionSettingsAsync().GetAwaiter().GetResult();
                Assert.Equal(saveCount, settings.SaveCount);
                settings.FailSave = true;
                vm.SelectedDefaultLocationOption = selectedId is null ? vm.DefaultLocationOptions[0] : null;
                vm.TransactionSettingsSaveTask.GetAwaiter().GetResult();
                Assert.Equal(selectedId, vm.SelectedDefaultLocationOption?.Id);
                Assert.Equal(selectedId, settings.LocationId);
                Assert.False(vm.IsTransactionSettingsBusy);
                Assert.Equal(new LocalizationService()["TransactionSettingsFailed"], vm.TransactionSettingsMessage);
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
        public int? LocationId { get; private set; }
        public int SaveCount { get; private set; }
        public bool FailSave { get; set; }
        public Task<Saldo.Domain.Entities.TransactionSettings> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new Saldo.Domain.Entities.TransactionSettings { DefaultPayerId = PayerId, DefaultLocationId = LocationId });
        public Task SaveAsync(Saldo.Domain.Entities.TransactionSettings settings, CancellationToken ct = default)
        {
            if (FailSave) throw new InvalidOperationException("Save failed.");
            PayerId = settings.DefaultPayerId;
            LocationId = settings.DefaultLocationId;
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
