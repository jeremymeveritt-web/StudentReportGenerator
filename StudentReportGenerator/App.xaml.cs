using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentReportGenerator.Services;

namespace StudentReportGenerator
{
    /// <summary>
    /// Application entry point and composition root: wires up Serilog logging, global exception
    /// handlers, and the dependency injection container that every ViewModel and service in the
    /// app is resolved from. See <see cref="ConfigureServices"/> for the full DI registration list.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>The app-wide DI container, built once at startup. Exposed statically only
        /// because WPF's <see cref="Application"/>/<see cref="Window"/> types are themselves
        /// constructed outside normal DI flow in a few places; everything else should receive its
        /// dependencies through constructor injection rather than reading this directly.</summary>
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

        /// <summary>Configures Serilog to write rolling daily log files to
        /// <c>%AppData%\FacultyFlow\logs</c>, retaining the last 14 days. Must run before
        /// <see cref="RegisterGlobalExceptionHandlers"/>, since the handlers log through this logger.</summary>
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

        /// <summary>
        /// Catches every category of otherwise-unhandled exception so the app never silently
        /// crashes without a trace: UI-thread exceptions (shown to the user with a friendly
        /// message and marked handled so the app keeps running), fatal AppDomain-level exceptions
        /// (logged before the process terminates), and unobserved exceptions from fire-and-forget
        /// background tasks.
        /// </summary>
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

        /// <summary>
        /// Registers every service, factory, ViewModel, and view with the DI container.
        /// Services that hold shared state (<see cref="AppStateService"/>, the orchestrators) are
        /// singletons; ViewModels and the main window are transient, since WPF only ever constructs
        /// one of each in practice but transient avoids accidentally sharing ViewModel state if
        /// that assumption ever changes.
        /// </summary>
        private void ConfigureServices(IServiceCollection services)
        {
            // Named HttpClient via IHttpClientFactory so DNS/socket lifetimes are managed correctly
            services.AddHttpClient(AiServiceFactory.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(90));
            services.AddHttpClient(SchoolDataOrchestratorService.SchoolDataHttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));

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
