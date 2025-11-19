using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.Core.Models
{
    public class CaptureSet
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; 
        public DateTime CreatedAt { get; set; }
        public string? Description { get; set; }

        
        public List<Snapshot> Snapshots { get; set; } = new();
    }
}
