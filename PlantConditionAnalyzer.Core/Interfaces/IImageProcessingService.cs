using PlantConditionAnalyzer.Core.Enums;
using PlantConditionAnalyzer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;


namespace PlantConditionAnalyzer.Core.Interfaces
{
    public record HotspotData(double Min, double Max, double Step, double SickPercentage);

    public interface IImageProcessingService
    {
        bool IsRecording { get; }
        Task<ProcessingResult> ProcessImageAsync(string imagePath, double minThreshold, double maxThreshold, bool isHotspotFilterEnabled, VegetationIndex vegetationIndex=VegetationIndex.ExG );
        Task<ProcessingResult> ProcessImageAsync(Mat frame, double minThreshold, double maxThreshold, bool isHotspotFilterEnabled, VegetationIndex vegetationIndex=VegetationIndex.ExG);
        public void ToggleRecording(string projectName);
        byte[] GenerateColormapLegend();
        event Action<HotspotData> OnStatisticsUpdated;
    }
}
