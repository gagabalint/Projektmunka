using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.Core.Models
{
    public class ProcessingResult
    {
        public byte[]? ProcessedImageBytes { get; set; }
        public Snapshot? Statistics { get; set; }
    }
}
