using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Persistence;
using Saldo.Infrastructure.Sqlite.Repositories;
using System.Windows;

namespace Saldo.Desktop.Wpf
{
    public partial class App : System.Windows.Application
    {
        private ServiceProvider? _serviceProvider;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SaldoDbContext>();
                await context.Database.MigrateAsync();
                await SeedInitialDataAsync(context);
            }

            _serviceProvider.GetRequiredService<MainWindow>().Show();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dataDir = System.IO.Path.Combine(appData, "Saldo");
            System.IO.Directory.CreateDirectory(dataDir);
            var dbPath = System.IO.Path.Combine(dataDir, "saldo.db");

            services.AddDbContext<SaldoDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IMemberRepository, MemberRepository>();
            services.AddScoped<ICounterpartyRepository, CounterpartyRepository>();
            services.AddScoped<ITagRepository, TagRepository>();

            services.AddScoped<AddTransaction>();
            services.AddScoped<EditTransaction>();
            services.AddScoped<DeleteTransaction>();
            services.AddScoped<ListTransactions>();
            services.AddScoped<GetSummary>();

            services.AddSingleton<IDialogService, WpfDialogService>();

            services.AddTransient<MainViewModel>();
            services.AddTransient<TransactionListViewModel>();
            services.AddTransient<CategoriesViewModel>();
            services.AddTransient<MembersViewModel>();
            services.AddTransient<CounterpartiesViewModel>();
            services.AddTransient<MainWindow>();
        }

        // Seeds a minimal set of reference data so the app is usable out of the box.
        private static async Task SeedInitialDataAsync(SaldoDbContext context)
        {
            if (await context.Categories.AnyAsync()) return;

            context.Categories.AddRange(
                new Category { Name = "Food & Drink" },
                new Category { Name = "Transport" },
                new Category { Name = "Shopping" },
                new Category { Name = "Entertainment" },
                new Category { Name = "Salary" },
                new Category { Name = "Other" });

            context.Members.Add(new Member { Name = "Me" });

            context.Counterparties.AddRange(
                new Counterparty { Name = "Shop" },
                new Counterparty { Name = "Restaurant" },
                new Counterparty { Name = "Online Store" });

            await context.SaveChangesAsync();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}

