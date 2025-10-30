using PlantConditionAnalyzer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.Core.Interfaces
{
    public interface IDatabaseService
    {
        Task SaveSnapshotAsync(Snapshot snapshot);
        Task<List<Snapshot>> GetAllSnapshotsAsync();
    }
}
