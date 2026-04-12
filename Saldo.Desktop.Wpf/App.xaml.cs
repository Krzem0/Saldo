using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Persistence;
using Saldo.Infrastructure.Sqlite.Repositories;
using Serilog;
using System.Windows;

namespace Saldo.Desktop.Wpf;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    private readonly LocalizationService _localization = new();

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureLogging();

        try
        {
            var services = new ServiceCollection();
            System.Windows.Application.Current.Resources["Localization"] = _localization;
            ConfigureServices(services, _localization);
            _serviceProvider = services.BuildServiceProvider();

            var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Saldo is starting.");

            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SaldoDbContext>();
                logger.LogInformation("Applying database migrations.");
                await context.Database.MigrateAsync();
                await SeedInitialDataAsync(context, logger);
            }

            logger.LogInformation("Opening main window.");
            _serviceProvider.GetRequiredService<MainWindow>().Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application startup failed.");
            MessageBox.Show(
                _localization["StartupFailedMessage"],
                _localization["AppTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static void ConfigureServices(IServiceCollection services, ILocalizationService localization)
    {
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddSerilog(Log.Logger, dispose: false);
        });

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
        services.AddSingleton(localization);
        services.AddSingleton<ILocalizationService>(localization);

        services.AddTransient<MainViewModel>();
        services.AddTransient<TransactionListViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<MembersViewModel>();
        services.AddTransient<CounterpartiesViewModel>();
        services.AddTransient<MainWindow>();
    }

    private static void ConfigureLogging()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDir = System.IO.Path.Combine(appData, "Saldo", "Logs");
        System.IO.Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.File(
                System.IO.Path.Combine(logDir, "saldo-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
            .CreateLogger();
    }

    // Seeds a minimal set of reference data so the app is usable out of the box.
    private static async Task SeedInitialDataAsync(SaldoDbContext context, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (await context.Categories.AnyAsync())
        {
            logger.LogInformation("Reference data already exists; skipping seed.");
            return;
        }

        logger.LogInformation("Seeding initial reference data.");

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

        logger.LogInformation("Initial reference data seeded.");
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception.");
        MessageBox.Show(
            _localization["UnhandledUiErrorMessage"],
            _localization["AppTitle"],
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Log.Fatal(exception, "Unhandled non-UI exception.");
        }
        else
        {
            Log.Fatal("Unhandled non-UI exception: {ExceptionObject}", e.ExceptionObject);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception.");
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application is shutting down.");
        _serviceProvider?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

