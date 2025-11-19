using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlantConditionAnalyzer.Core.Interfaces;
using PlantConditionAnalyzer.Core.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.AvaloniaApp.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IImageProcessingService imageProcessor;

        // A 'Greeting' egy új tulajdonság, amit a 'ViewModelBase'-ből örökölt
        // 'ObservableObject' fog kezelni. A sablonodban a 'Greeting'
        // helyett lehet, hogy más van, azt átírhatod.
        public string Greeting { get; }

        public MainWindowViewModel(IImageProcessingService imageProcessor)
        {
            this.imageProcessor = imageProcessor;
            statisticsText = "Válassz egy képet a feldolgozáshoz";
        }
        [ObservableProperty]
        private Bitmap? originalImage;

        [ObservableProperty]
        private Bitmap? processedImage;

        [ObservableProperty]
        private string? statisticsText;

        [ObservableProperty]
        private bool isProcessing=false;

        [RelayCommand]
        private async Task LoadImageAsync()
        {
            if (IsProcessing) return;
            IsProcessing = true;
            statisticsText = "Kép kiválasztása...";

            var file = await GetFilePickerAsync();
            if (file == null)
            {
                isProcessing = false;
                statisticsText = "Képkiválasztás megszakítva.";
                return;
            }
            await using (var stream = await file.OpenReadAsync())
            {
                OriginalImage = new Bitmap(stream); 
            }
            StatisticsText = "Kép feldolgozása...";
            ProcessingResult? result = null;

           
            await Task.Run(() =>
            {
                try
                {
                    result = imageProcessor.ProcessImage(file.Path.LocalPath);
                }
                catch (Exception ex)
                {
                    StatisticsText = $"Hiba: {ex.Message}";
                }
            });

            if (result != null)
            {
                ProcessedImage = ConvertBytesToBitmap(result.ProcessedImageBytes);
                StatisticsText = $"Feldolgozás kész.\n" +
                                 $"Átlag: {result.Statistics.ViMean:F2}\n" + 
                                 $"Szórás: {result.Statistics.ViStdDev:F2}\n" +
                                 $"Terület: {result.Statistics.PlantAreaPercentage:F1}%";

            }

            IsProcessing = false; 
        }
        private Bitmap? ConvertBytesToBitmap(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            using (var ms = new MemoryStream(bytes))
            {
                return new Bitmap(ms);
            }
        }
        private async Task<IStorageFile?> GetFilePickerAsync()
        {
            // Az Avalonia megköveteli, hogy a Fájl Dialógus a "legfelső" ablakhoz
            // kapcsolódjon, ezért kell ez a kis varázslat.
            // Közvetlenül elkérjük a MainWindow-t az alkalmazás élettartamától
            var topLevel = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow; if (topLevel == null) return null;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Válassz egy képet a feldolgozáshoz",
                AllowMultiple = false, 
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
            });

            // Visszaadjuk az első kiválasztott fájlt (vagy null-t, ha nem választott)
            return files.Count >= 1 ? files[0] : null;
        }
    }
}
