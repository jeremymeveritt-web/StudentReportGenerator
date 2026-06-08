using System;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
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

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            // Build the engine
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Request the MainWindow from the DI engine, which will automatically 
            // build and inject all the required ViewModels it needs!
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Core Services (Singleton = One instance shared across the whole app)
            services.AddSingleton<HttpClient>();
            services.AddSingleton<AppStateService>();

            // ADD THIS NEW LINE:
            services.AddSingleton<ReportOrchestratorService>();

            // ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<MainViewModel>();

            // Views
            services.AddTransient<MainWindow>();
        }
    }
}