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
using Serilog.Events;
using System.Globalization;
using System.Windows;
using FluentValidation;
using Saldo.Application.DTOs;
using Saldo.Application.Validation;

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
            var themeService = _serviceProvider.GetRequiredService<IThemeService>();
            System.Windows.Application.Current.Resources["ThemeService"] = themeService;

            var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Saldo is starting.");

            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SaldoDbContext>();
                logger.LogInformation("Applying database migrations.");
                await context.Database.MigrateAsync();
                await SeedInitialDataAsync(context, logger, _localization.CurrentCulture);
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
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            logging.AddSerilog(Log.Logger, dispose: false);
        });

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dataDir = System.IO.Path.Combine(appData, "Saldo");
        System.IO.Directory.CreateDirectory(dataDir);
        var dbPath = System.IO.Path.Combine(dataDir, "saldo.db");
        services.AddSingleton<IDatabaseFolderService>(new WindowsDatabaseFolderService(dataDir));

        services.AddDbContext<SaldoDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<ITransactionSettingsRepository, TransactionSettingsRepository>();
        services.AddScoped<GetTransactionSettings>();
        services.AddScoped<SetDefaultPayer>();
        services.AddScoped<SetDefaultLocation>();
        services.AddScoped<SetTransactionDefaults>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IPartyRepository, PartyRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IDatabaseBackupService, SqliteDatabaseBackupService>();

        services.AddScoped<IValidator<AddTransactionCommand>, AddTransactionCommandValidator>();
        services.AddScoped<IValidator<EditTransactionCommand>, EditTransactionCommandValidator>();

        services.AddScoped<AddTransaction>();
        services.AddScoped<EditTransaction>();
        services.AddScoped<AddCategory>();
        services.AddScoped<AddTag>();
        services.AddScoped<EditTag>();
        services.AddScoped<EditCategory>();
        services.AddScoped<AddParty>();
        services.AddScoped<AddLocation>();
        services.AddScoped<DeleteTransaction>();
        services.AddScoped<ListTransactions>();
        services.AddScoped<GetSummary>();
        services.AddScoped<GetNewTransactionDefaults>();

        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton(localization);
        services.AddSingleton<ILocalizationService>(localization);

        services.AddTransient<MainViewModel>();
        services.AddTransient<TransactionListViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<TagsViewModel>();
        services.AddTransient<PartiesViewModel>();
        services.AddTransient<LocationsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MainWindow>();
    }

    private static void ConfigureLogging()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDir = System.IO.Path.Combine(appData, "Saldo", "Logs");
        System.IO.Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
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

    private static async Task SeedInitialDataAsync(SaldoDbContext context, Microsoft.Extensions.Logging.ILogger logger, CultureInfo culture)
    {
        // The initial migration already seeds the self party and transaction settings.
        var hasAnyData = await context.Categories.AnyAsync()
            || await context.Tags.AnyAsync()
            || await context.Transactions.AnyAsync()
            || await context.TransactionTags.AnyAsync();

        if (hasAnyData)
        {
            logger.LogInformation("Reference data already exists; skipping seed.");
            return;
        }

        logger.LogInformation("Seeding initial reference data.");

        var seedData = GetSeedData(culture);

        context.Categories.AddRange(seedData.CategoryNames.Select(name => new Category { Name = name }));

        await context.SaveChangesAsync();

        logger.LogInformation("Initial reference data seeded.");
    }

    private static SeedData GetSeedData(CultureInfo culture)
    {
        return culture.TwoLetterISOLanguageName switch
        {
            "pl" => new SeedData(
                [
                    "Mieszkanie",
                    "Media i telefon",
                    "Supermarkety",
                    "Zakupy online",
                    "Jedzenie do pracy",
                    "Jedzenie poza domem",
                    "Używki",
                    "Transport",
                    "Zwierzęta",
                    "Rozrywka",
                    "Wynagrodzenie",
                    "Premia",
                    "Odsetki z oszczędności",
                    "Dywidendy",
                    "Inne",
                    "Zdrowie",
                    "Prezenty i darowizny",
                    "Ubiór i pielęgnacja"
                ]),
            _ => new SeedData(
                [
                    "Housing",
                    "Utilities and phone",
                    "Supermarkets",
                    "Online shopping",
                    "Food for work",
                    "Dining out",
                    "Alcohol and tobacco",
                    "Transport",
                    "Pets",
                    "Entertainment",
                    "Salary",
                    "Bonus",
                    "Interest on savings",
                    "Dividends",
                    "Other",
                    "Health",
                    "Gifts and donations",
                    "Clothing and personal care"
                ])
        };
    }

    private sealed record SeedData(IReadOnlyList<string> CategoryNames);

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
