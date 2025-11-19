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

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                
                desktop.MainWindow = new MainWindow
                {
                   
                    DataContext = Services.GetRequiredService<MainWindowViewModel>()
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