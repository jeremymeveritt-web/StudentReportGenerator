using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentReportGenerator.Services;

namespace StudentReportGenerator
{
    public partial class App : Application
    {
        // This is our central service registry
        public static IServiceProvider? ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigureLogging();
            RegisterGlobalExceptionHandlers();

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            // Build the engine
            ServiceProvider = serviceCollection.BuildServiceProvider();

            Log.Information("FacultyFlow AI starting (version {Version})",
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);

            // Request the MainWindow from the DI engine, which will automatically
            // build and inject all the required ViewModels it needs!
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("FacultyFlow AI shutting down.");
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        private static void ConfigureLogging()
        {
            // Rolling daily log files under %AppData%\FacultyFlow\logs, kept for two weeks
            string logFolder = FileSandboxService.GetSafeFilePath("logs");
            Directory.CreateDirectory(logFolder);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(Path.Combine(logFolder, "facultyflow-.log"),
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: 14)
                .CreateLogger();
        }

        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException += (_, args) =>
            {
                Log.Error(args.Exception, "Unhandled UI exception");
                MessageBox.Show(
                    "Something unexpected went wrong, but your data is safe.\n\n" +
                    $"Details: {args.Exception.Message}\n\n" +
                    "A full error log has been saved in the FacultyFlow folder under AppData — " +
                    "please include it if you contact support.",
                    "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                Log.Fatal(args.ExceptionObject as Exception, "Fatal unhandled exception — application terminating");
                Log.CloseAndFlush();
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Log.Error(args.Exception, "Unobserved background task exception");
                args.SetObserved();
            };
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Named HttpClient via IHttpClientFactory so DNS/socket lifetimes are managed correctly
            services.AddHttpClient(AiServiceFactory.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(90));

            // Core Services (Singleton = One instance shared across the whole app)
            services.AddSingleton<AppStateService>();
            services.AddSingleton<IAiServiceFactory, AiServiceFactory>();
            services.AddSingleton<ReportOrchestratorService>();
            services.AddSingleton<SchoolDataOrchestratorService>();

            // ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<MainViewModel>();

            // Views
            services.AddTransient<MainWindow>();
        }
    }
}
