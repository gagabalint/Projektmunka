using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

using PlantConditionAnalyzer.Core.Interfaces;
using PlantConditionAnalyzer.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.AvaloniaApp.ViewModels
{
    public partial class HistoryViewModel : ViewModelBase
    {
        private readonly IDatabaseService databaseService;

        [ObservableProperty]
        private ObservableCollection<CaptureSet> projects = new();

        [ObservableProperty]
        private CaptureSet? selectedProject;

        [ObservableProperty]
        private ObservableCollection<Snapshot> snapshots = new();

        [ObservableProperty]
        private Snapshot? selectedSnapshot;

        [ObservableProperty]
        private Bitmap? snapshotImage;

        public event Action<List<Snapshot>, Snapshot?>? ChartDataChanged;

        private string appPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PlantConditionAnalyzer");

        [ObservableProperty]
        private bool isDeleteConfirmVisible = false;

        [ObservableProperty]
        private string statusMessage = "Ready";
        public HistoryViewModel(IDatabaseService databaseService)
        {
            this.databaseService = databaseService;
           
        }

        public async Task LoadAsync()
        {
            var sets = await databaseService.GetCaptureSetsAsync();
            Projects = new ObservableCollection<CaptureSet>(sets);
            if (Projects.Any()) SelectedProject = Projects[0];
        }

        async partial void OnSelectedProjectChanged(CaptureSet? value)
        {
            if (value == null) return;
            var snaps = await databaseService.GetSnapshotsForSetAsync(value.Id);
            // Időrendbe (legrégebbi elöl a diagramhoz)
            Snapshots = new ObservableCollection<Snapshot>(snaps.OrderBy(s => s.Timestamp));
            SelectedSnapshot = Snapshots.LastOrDefault();
            UpdateChart();
        }

        partial void OnSelectedSnapshotChanged(Snapshot? value)
        {
            if (value == null) { SnapshotImage = null; return; }

            if (File.Exists(value.ImagePath))
            {
                SnapshotImage = new Bitmap(value.ImagePath);
            }
            else
            {
                SnapshotImage = null;
            }

            // Kiemeljük a kiválasztott pontot
            UpdateChart();
        }

        private void UpdateChart()
        {

            ChartDataChanged?.Invoke(Snapshots.ToList(), SelectedSnapshot);

        }
      

        [RelayCommand]
        private void OpenRecordingsFolder()
        {
            if (SelectedProject == null) return;

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PlantConditionAnalyzer", "Recordings", SelectedProject.Name);

            // Ha még nincs videó rögzítve, akkor a fő Recordings mappát nyitjuk meg
            if (!Directory.Exists(folder))
            {
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PlantConditionAnalyzer", "Recordings");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            }

            // OS szintű mappa megnyitás
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
                Verb = "open"
            });
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
        [RelayCommand]
        private async Task DeleteSnapshotAsync()
        {
            if (SelectedSnapshot == null) return;
            // Ha van kép, töröljük a fájlt is
            if (File.Exists(SelectedSnapshot.ImagePath))
                File.Delete(SelectedSnapshot.ImagePath);
            await databaseService.DeleteSnapshotAsync(SelectedSnapshot.Id);
            Snapshots.Remove(SelectedSnapshot);
            SelectedSnapshot = Snapshots.LastOrDefault();
            UpdateChart();
        }
        [RelayCommand]
        private void DeleteProject()
        {
            if (SelectedProject != null) IsDeleteConfirmVisible = true; // Csak felhozzuk az ablakot!
        }

        [RelayCommand]
        private void CancelDelete()
        {
            IsDeleteConfirmVisible = false; // Eltüntetjük az ablakot
        }

        [RelayCommand]
        private async Task ConfirmDeleteProjectAsync()
        {
            if (SelectedProject == null) return;

            string imagesDir = Path.Combine(appPath, "Images", SelectedProject.Name);
            if (Directory.Exists(imagesDir)) Directory.Delete(imagesDir, true);

            string videosDir = Path.Combine(appPath, "Recordings", SelectedProject.Name);
            if (Directory.Exists(videosDir)) Directory.Delete(videosDir, true);

            await databaseService.DeleteCaptureSetAsync(SelectedProject.Id);

            Projects.Remove(SelectedProject);
            SelectedProject = Projects.FirstOrDefault();

            IsDeleteConfirmVisible = false;
            StatusMessage = "Project deleted permanently.";
        }
    }
}