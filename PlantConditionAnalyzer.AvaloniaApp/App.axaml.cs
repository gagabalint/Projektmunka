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
                // === ITT A VÁLTOZTATÁS ===
                // Nem 'MainView'-t, hanem 'MainWindow'-t hozunk létre
                desktop.MainWindow = new MainWindow
                {
                    // És 'MainWindowViewModel'-t kérünk a DI-tõl
                    DataContext = Services.GetRequiredService<MainWindowViewModel>()
                };
            }
            // Az else if ág (singleViewPlatform) valószínûleg nem is kell
            // egy asztali alkalmazásnál, de a biztonság kedvéért átírjuk azt is.
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                // A sablonod valószínûleg nem generált 'MainView'-t,
                // így ezt az ágat akár törölheted is, vagy
                // átírhatod 'MainWindow'-ra, bár az nem UserControl.
                // Egyelõre koncentráljunk a desktop.MainWindow-ra.

                // Kommenteld ki ezt az ágat, ha nincs 'MainView'-d:
                /*
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = Services.GetRequiredService<MainWindowViewModel>()
                };
                */
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IImageProcessingService, ImageProcessingService>();

            // === ITT A VÁLTOZTATÁS ===
            // Nem 'MainViewModel'-t, hanem 'MainWindowViewModel'-t regisztrálunk
            services.AddTransient<MainWindowViewModel>();
        }
    }
}