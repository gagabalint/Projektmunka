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
    public interface IImageProcessingService
    {
        Task<ProcessingResult> ProcessImageAsync(string imagePath, double minThreshold, double maxThreshold, bool isHotspotFilterEnabled, VegetationIndex vegetationIndex=VegetationIndex.ExG );
        Task<ProcessingResult> ProcessImageAsync(Mat frame, double minThreshold, double maxThreshold, bool isHotspotFilterEnabled, VegetationIndex vegetationIndex=VegetationIndex.ExG);
        byte[] GenerateColormapLegend();
    }
}
