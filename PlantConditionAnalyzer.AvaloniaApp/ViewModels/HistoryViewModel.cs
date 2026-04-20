using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

using PlantConditionAnalyzer.Core.Interfaces;
using PlantConditionAnalyzer.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    }
}