using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.Core.Models
{
    public class Snapshot
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public double ViMean { get; set; }
        public double ViStdDev { get; set; }
        public double PlantAreaPercentage { get; set; }
        public double? SpadEstimate { get; set; }

        public string VegetationIndexName { get; set; } = "ExG";
    }
}
