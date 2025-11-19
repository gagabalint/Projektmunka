using PlantConditionAnalyzer.Core.Enums;
using PlantConditionAnalyzer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.Core.Interfaces
{
    public interface IImageProcessingService
    {
        ProcessingResult ProcessImage(string imagePath, VegetationIndex vegetationIndex=VegetationIndex.ExG);
    }
}
