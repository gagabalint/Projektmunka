using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using PlantConditionAnalyzer.Core.Enums;
using PlantConditionAnalyzer.Core.Interfaces;
using PlantConditionAnalyzer.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.AvaloniaApp.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly IImageProcessingService imageProcessor;
        private readonly IDatabaseService databaseService;


        private string? currentFilePath;

        private VideoCapture? capture;
        private System.Threading.Timer? cameraTimer;
        private bool isCameraRunning = false;
        private bool isProcessingFrame = false;//overload vedelem
        private int frameCounter = 0;

        private ProcessingResult? lastResult;

        [ObservableProperty]
        private bool isCameraModeOn = false;

        [ObservableProperty]
        private Bitmap? originalImage;

        [ObservableProperty]
        private Bitmap? processedImage;

        [ObservableProperty]
        private string? statisticsText;

        [ObservableProperty]
        private bool isProcessing = false;

        [ObservableProperty]
        private Bitmap? colormapLegend;
        [ObservableProperty]
        private VegetationIndex selectedIndex = VegetationIndex.ExG;

        public ObservableCollection<VegetationIndex> AvailableIndices { get; } = new ObservableCollection<VegetationIndex>(Enum.GetValues<VegetationIndex>());
        public ObservableCollection<CaptureSet> Projects { get; } = new();

        [ObservableProperty]
        private CaptureSet? selectedProject;

        [ObservableProperty]
        private string newProjectName = string.Empty;

        [ObservableProperty]
        private string statusMessage = "Ready";



        public MainWindowViewModel(IImageProcessingService imageProcessor, IDatabaseService databaseService)
        {
            this.imageProcessor = imageProcessor;
            this.databaseService = databaseService;

            statisticsText = "Select an image to process";

            var legendBytes = imageProcessor.GenerateColormapLegend();
            ColormapLegend = ConvertBytesToBitmap(legendBytes);

            LoadProjectsAsync();
        }



        [RelayCommand]
        private void ToggleCamera()
        {
            if (isCameraRunning) StopCamera();
            else StartCamera();
        }

        private void StartCamera()
        {
            try
            {
                capture = new VideoCapture(0);//temp, elsodleges, de kulsohoz modositando az index
                if (!capture.IsOpened())
                {
                    StatusMessage = "Couldn't open camera!";
                    return;
                }
                isCameraRunning = true;
                isCameraModeOn = true;
                StatusMessage = "Camera started";
                cameraTimer = new System.Threading.Timer(CameraTick, null, 0, 33);//kb 30fps az input igy

            }
            catch (Exception)
            {

                throw;
            }

        }
        private async void CameraTick(object? state)
        {
            if (capture == null || capture.IsDisposed) return;
            try
            {
                using Mat frame = new();
                if (!capture.Read(frame) || frame.Empty()) return;//kamera buffer overload megelozese 
                frameCounter++;
                if (frameCounter % 3 != 0) return; //csak minden 3. framet dolgozzuk fel memoriaigeny csokkentese miatt
                if (isProcessingFrame) return; //eldobjuk az aktualist, ha az elozo meg nem lett feldolgozva
                isProcessingFrame = true;
                var result = await imageProcessor.ProcessImageAsync(frame, SelectedIndex);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    OriginalImage = ConvertBytesToBitmap(frame.ToBytes(".bmp"));
                    ProcessedImage = ConvertBytesToBitmap(result.ProcessedImageBytes);

                    var s = result.Statistics;
                    StatisticsText = $"LIVE FEED ({s.VegetationIndexName})\n" +
                                     $"Mean Vitality: {s.ViMean:F2}\n" +
                                     $"Plant Cover: {s.PlantAreaPercentage:F1}%";

                    lastResult = result; // video kozbeni snapshothoz szukseges
                });

            }
            catch (Exception e)
            {
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        StatusMessage = $"Stream Error: {e.Message}";
                        StopCamera();
                    });
                }
                catch (TaskCanceledException) { }//hogyha bezartak az alkalmazast, elengedjuk a hibat

            }
            finally
            {
                isProcessingFrame = false;
            }
        }
        private void StopCamera()
        {
            cameraTimer?.Dispose();
            cameraTimer = null;

            capture?.Release();
            capture = null;

            isCameraRunning = false;
            IsCameraModeOn = false;
            StatusMessage = "Camera stopped.";
        }
        private async Task LoadProjectsAsync()
        {
            var sets = await databaseService.GetCaptureSetsAsync();
            Projects.Clear();
            if (sets != null)
            {
                foreach (var item in sets)
                {
                    Projects.Add(item);
                }
                SelectedProject = Projects[0];
            }


        }
        async partial void OnSelectedIndexChanged(VegetationIndex value)
        {
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                await ProcessCurrentFileAsync();
            }
        }

        [RelayCommand]
        private async Task CreateProjectAsync()
        {
            if (string.IsNullOrWhiteSpace(NewProjectName)) return;
            CaptureSet newSet = await databaseService.CreateCaptureSetAsync(NewProjectName, null);
            Projects.Insert(0, newSet);
            SelectedProject = newSet;
            NewProjectName = string.Empty;
            StatusMessage = $"Project {newSet.Name} successfully created";
        }

        [RelayCommand]
        private async Task SaveMeasurementAsync()
        {
            ProcessingResult? resultToSave = lastResult;
            if (resultToSave == null || SelectedProject == null)
            {
                StatusMessage = "No result to save or no project selected.";
                return;
            }

            IsProcessing = true;
            StatusMessage = "Saving";
            try
            {
                string imagesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PlantConditionAnalyzer_Images");
                if (!Directory.Exists(imagesDir))
                {
                    Directory.CreateDirectory(imagesDir);
                }
                string fileName = $"Img_{DateTime.Now:yyyyMMdd_HHmmss}_{SelectedProject.Id}.png";
                string imgPath = Path.Combine(imagesDir, fileName);

                if (resultToSave.ProcessedImageBytes != null)
                {
                    await File.WriteAllBytesAsync(imgPath, resultToSave.ProcessedImageBytes);
                    Snapshot snapshot = resultToSave.Statistics!;
                    snapshot.ImagePath = imgPath;
                    snapshot.CaptureSetId = SelectedProject.Id;
                    await databaseService.SaveSnapshotAsync(snapshot);
                    StatusMessage = "Measurment saved succesfully";
                }

            }
            catch (Exception e)
            {
                StatusMessage = $"Save Error: {e.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }


        [RelayCommand]
        private async Task LoadImageAsync()
        {
            if (isCameraRunning) StopCamera();
            if (IsProcessing) return;
            IsProcessing = true;
            StatusMessage = "Selecting image...";

            var file = await GetFilePickerAsync();
            if (file == null)
            {
                IsProcessing = false;
                StatusMessage = "Image selection cancelled.";
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
            StatusMessage = $"Image processed with {SelectedIndex}";
        }

        private async Task ProcessCurrentFileAsync()
        {
            if (string.IsNullOrEmpty(currentFilePath)) return;

            IsProcessing = true;
            StatisticsText = $"Processing with {SelectedIndex}...";
            ProcessingResult? result = null;

            await Task.Run(async () =>
            {
                try
                {
                    // Átadjuk a kiválasztott indexet is!
                    result = await imageProcessor.ProcessImageAsync(currentFilePath, SelectedIndex);
                }
                catch (Exception ex)
                {
                    StatisticsText = $"Error: {ex.Message}";
                }
            });
            lastResult = result;
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

            return files.Count >= 1 ? files[0] : null;
        }

        public void Dispose()
        {
            StopCamera();
        }
    }
}
