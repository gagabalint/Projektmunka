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
        Task InitializeAsync();

        Task<List<CaptureSet>> GetCaptureSetsAsync();
        Task<CaptureSet> CreateCaptureSetAsync(string name, string? description);
        Task DeleteCaptureSetAsync(int id);
        Task SaveSnapshotAsync(Snapshot snapshot);
        Task<List<Snapshot>> GetSnapshotsForSetAsync(int captureSetId);
    }
}
