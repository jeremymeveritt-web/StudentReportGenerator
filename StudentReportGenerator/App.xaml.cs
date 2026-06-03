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
            // 1. Core Services (Singleton = One instance shared across the whole app)
            services.AddSingleton<HttpClient>();

            // 2. ViewModels (Transient = Fresh instance every time it's requested)
            services.AddTransient<MainViewModel>();

            // 3. Views / Windows
            services.AddTransient<MainWindow>();

            // Note: As we break apart the God-Class, we will register the new 
            // smaller ViewModels and AI Services right here!
        }
    }
}