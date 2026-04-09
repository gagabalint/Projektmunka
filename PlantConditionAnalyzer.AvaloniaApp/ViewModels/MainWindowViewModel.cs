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
using PlantConditionAnalyzer.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.AvaloniaApp.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IDisposable
    {
        #region fields and props
        private readonly IImageProcessingService imageProcessor;
        private readonly IDatabaseService databaseService;
        private readonly ICameraService cameraService;

        private string? currentFilePath;
        private string appPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PlantConditionAnalyzer");

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

        [ObservableProperty]
        private double minThreshold = 0.0;

        [ObservableProperty]
        private double maxThreshold = 1.0;
        [ObservableProperty]
        private bool isHotspotFilterEnabled;

        [ObservableProperty]
        private string _recordButtonText = "Start Recording";

        [ObservableProperty]
        private string _recordButtonColor = "#d32f2f"; // Alapból piros gomb

        [RelayCommand]
        private void ToggleRecording()
        {
            // Szólunk a szerviznek, hogy váltson állapotot
            imageProcessor.ToggleRecording(SelectedProject!.Name);

            if (imageProcessor.IsRecording)
            {
                RecordButtonText = "Stop Recording";
                RecordButtonColor = "#424242"; // Szürkére vált, ha rögzít
                StatusMessage = "Video recording started...";
            }
            else
            {
                RecordButtonText = "Start Recording";
                RecordButtonColor = "#d32f2f"; // Vissza pirosra
                StatusMessage = "Record saved to Recordings";
            }
        }
        #endregion
        public MainWindowViewModel(IImageProcessingService imageProcessor, IDatabaseService databaseService, ICameraService cameraService)
        {
            this.cameraService = cameraService;
            this.imageProcessor = imageProcessor;
            this.databaseService = databaseService;
            cameraService.FrameCaptured += OnFrameCaptured;
            cameraService.ErrorOccurred += (s, msg) => StatusMessage = msg;

            statisticsText = "Select an image to process";

            var legendBytes = imageProcessor.GenerateColormapLegend();
            ColormapLegend = ConvertBytesToBitmap(legendBytes);

            LoadProjectsAsync();
        }
        [RelayCommand]
        private async Task Remeasure()
        {
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                await ProcessCurrentFileAsync();
            }
        }

        [RelayCommand]
        private void ToggleCamera()
        {
            if (cameraService.IsRunning)
            {
                cameraService.Stop();
                IsCameraModeOn = false;
                StatusMessage = "Camera stopped.";
            }
            else
            {
                IsCameraModeOn = true;
                StatusMessage = "Starting camera...";
                cameraService.Start(0);
            }
        }

        [RelayCommand]
        private async Task LoadVideoAsync()
        {
            cameraService.Stop();

            var file = await GetVideoPickerAsync();
            if (file == null) return;

            IsCameraModeOn = true;
            StatusMessage = "Playing video...";

            cameraService.Start(file.Path.LocalPath);
        }

        private async void OnFrameCaptured(object? sender, Mat frame)
        {
            // Ha épp dolgozunk egy előző képen, ezt eldobjuk (hogy ne akadjon a UI)
            if (IsProcessing)
            {
                frame.Dispose();
                return;
            }

            IsProcessing = true;

            try
            {
                using (frame)
                {
                    var result = await imageProcessor.ProcessImageAsync(frame, MinThreshold, MaxThreshold, IsHotspotFilterEnabled, SelectedIndex);

                    try
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (result != null && result.Statistics != null)
                            {
                                OriginalImage = ConvertBytesToBitmap(frame.ToBytes());
                                ProcessedImage = ConvertBytesToBitmap(result.ProcessedImageBytes);
                                var s = result.Statistics;
                                StatisticsText = $"LIVE: {s.VegetationIndexName}\n" +
                                                 $"Mean: {s.ViMean:F2} | Cover: {s.PlantAreaPercentage:F1}%";

                                lastResult = result;
                            }
                        });
                    }
                    catch (TaskCanceledException)
                    {
                        //kamera mod kozben program bezaraskor ide jon
                        return;
                    }
                }
            }
            catch (Exception ex)
            {

                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public void Dispose()
        {
            cameraService.Stop();
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
                string imagesDir = Path.Combine(appPath, "Images");
                if (!Directory.Exists(imagesDir))
                {
                    Directory.CreateDirectory(imagesDir);
                }
                string setPath = Path.Combine(imagesDir, SelectedProject.Name);
                if (!Directory.Exists(setPath))
                {
                    Directory.CreateDirectory(setPath);
                }
                string fileName = $"Img_{DateTime.Now:yyyyMMdd_HHmmss}_{SelectedProject.Name}.png";
                string imgPath = Path.Combine(setPath, fileName);

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
            if (cameraService.IsRunning) cameraService.Stop();
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

        [RelayCommand]
        private async Task ExportCurrentProject()
        {
            if (SelectedProject == null || string.IsNullOrEmpty(SelectedProject.Name)) return;
            try
            {
                StatusMessage = $"Export in progress ({SelectedProject.Name})";
                var query = await databaseService.GetSnapshotsForSetAsync(SelectedProject.Id);
                if (!query.Any())
                { StatusMessage = $"No saved data found in {SelectedProject.Name}"; return; }
                CsvWriter(query);
            }
            catch (Exception ex) { StatusMessage = $"Error in export process: {ex.Message}"; }
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
                    result = await imageProcessor.ProcessImageAsync(currentFilePath, MinThreshold, MaxThreshold, IsHotspotFilterEnabled, SelectedIndex);
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
            // Az Avalonia megköveteli hogy a file dialogue a legfelso ablakon nyiljon, ezert kell a topLevel

            var topLevel = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow; if (topLevel == null) return null;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Válassz egy képet a feldolgozáshoz",
                AllowMultiple = false,
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
            });

            return files.Count >= 1 ? files[0] : null;
        }
        private async Task<IStorageFile?> GetVideoPickerAsync()
        {
            var topLevel = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (topLevel == null) return null;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Video File",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Video Files") { Patterns = new[] { "*.mp4", "*.avi", "*.mkv" } } }
            });

            return files.Count > 0 ? files[0] : null;
        }

        private void CsvWriter(List<Snapshot> query)
        {
            string exportFolder = Path.Combine(appPath, "Exports");
            if (!Directory.Exists(exportFolder))
            {
                Directory.CreateDirectory(exportFolder);
            }
            string setPath = Path.Combine(exportFolder, SelectedProject!.Name);
            if (!Directory.Exists(setPath))
            {
                Directory.CreateDirectory(setPath);
            }
            string fileName = $"{SelectedProject!.Name}_Export_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            string filePath = Path.Combine(setPath, fileName);

            // 5. Fájlba írás
            using (StreamWriter sw = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                sw.WriteLine("ID;Date;Index Type;Avg;Dev;Plant Area(%)");

                foreach (var snap in query)
                {
                    string date = snap.Timestamp.ToString("yyyy.MM.dd HH:mm:ss");
                    string mean = snap.ViMean.ToString("F10");
                    string std = snap.ViStdDev.ToString("F10");
                    string area = snap.PlantAreaPercentage.ToString("F2");

                    sw.WriteLine($"{snap.Id};{date};{snap.VegetationIndexName};{mean};{std};{area}");
                }
            }
            StatusMessage = $"Succesful export: {fileName}";
        }
    }
}
