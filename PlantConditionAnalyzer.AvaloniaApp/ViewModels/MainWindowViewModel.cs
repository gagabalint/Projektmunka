using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlantConditionAnalyzer.Core.Enums;
using PlantConditionAnalyzer.Core.Interfaces;
using PlantConditionAnalyzer.Core.Models;
using System;
using System.Collections.ObjectModel;
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
        private string? currentFilePath;

        public MainWindowViewModel(IImageProcessingService imageProcessor)
        {
            this.imageProcessor = imageProcessor;
            statisticsText = "Select an image to process";
            var legendBytes = imageProcessor.GenerateColormapLegend();
            ColormapLegend=ConvertBytesToBitmap(legendBytes);
        }
        [ObservableProperty]
        private Bitmap? originalImage;

        [ObservableProperty]
        private Bitmap? processedImage;

        [ObservableProperty]
        private string? statisticsText;

        [ObservableProperty]
        private bool isProcessing=false;

        [ObservableProperty]
        private Bitmap? colormapLegend;

        public ObservableCollection<VegetationIndex> AvailableIndices { get;  }=new ObservableCollection<VegetationIndex>(Enum.GetValues<VegetationIndex>());

        [ObservableProperty]
        private VegetationIndex selectedIndex = VegetationIndex.ExG;

        async partial void OnSelectedIndexChanged(VegetationIndex value)
        {
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                await ProcessCurrentFileAsync();
            }
        }
        [RelayCommand]
        private async Task LoadImageAsync()
        {
            if (IsProcessing) return;
            IsProcessing = true;
            StatisticsText = "Selecting image...";

            var file = await GetFilePickerAsync();
            if (file == null)
            {
                IsProcessing = false;
                StatisticsText = "Image selection cancelled.";
                return;
            }

            // Elmentjük az útvonalat
            currentFilePath = file.Path.LocalPath;

            // Megjelenítjük az eredetit
            await using (var stream = await file.OpenReadAsync())
            {
                OriginalImage = new Bitmap(stream);
            }

            // Feldolgozzuk
            await ProcessCurrentFileAsync();
        }

        private async Task ProcessCurrentFileAsync()
        {
            if (string.IsNullOrEmpty(currentFilePath)) return;

            IsProcessing = true;
            StatisticsText = $"Processing with {SelectedIndex}...";
            ProcessingResult? result = null;

            await Task.Run(() =>
            {
                try
                {
                    // Átadjuk a kiválasztott indexet is!
                    result = imageProcessor.ProcessImage(currentFilePath, SelectedIndex);
                }
                catch (Exception ex)
                {
                    StatisticsText = $"Error: {ex.Message}";
                }
            });

            if (result != null && result.Statistics != null)
            {
                ProcessedImage = ConvertBytesToBitmap(result.ProcessedImageBytes);
                StatisticsText = $"Index: {result.Statistics.VegetationIndexName}\n" +
                                 $"Mean: {result.Statistics.ViMean:F2}\n" +
                                 $"StdDev: {result.Statistics.ViStdDev:F2}\n" +
                                 $"Area: {result.Statistics.PlantAreaPercentage:F1}%";
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

            return files.Count >=1 ? files[0]:null;
        }
    }
}
