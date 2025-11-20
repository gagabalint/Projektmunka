using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PlantConditionAnalyzer.AvaloniaApp.ViewModels;
using PlantConditionAnalyzer.AvaloniaApp.Views;
using PlantConditionAnalyzer.Core.Interfaces;
using PlantConditionAnalyzer.Infrastructure.Services;
using System;
using System.Linq;

namespace PlantConditionAnalyzer.AvaloniaApp
{
    public partial class App : Application
    {
        public static ServiceProvider Services { get; private set; } = null!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();


            try
            {
                var dbService = Services.GetRequiredService<IDatabaseService>();
                dbService.InitializeAsync().GetAwaiter().GetResult(); // megvarjuk amig betolti a dbt
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FATAL ERROR: Database init failed: {ex.Message}");
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();


                desktop.MainWindow = new MainWindow
                {
                   
                    DataContext =mainWindowViewModel
                };
                desktop.Exit += (sender, args) =>
                {
                    mainWindowViewModel.Dispose(); //ha bezartak az appot, ez allitja le a kamerat
                };
            }
           

            base.OnFrameworkInitializationCompleted();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IImageProcessingService, ImageProcessingService>();
            services.AddSingleton<IDatabaseService,DatabaseService>();
           
            services.AddTransient<MainWindowViewModel>();
        }
    }
}