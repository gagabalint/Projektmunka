using Microsoft.EntityFrameworkCore;
using PlantConditionAnalyzer.Core.Interfaces;
using PlantConditionAnalyzer.Core.Models;
using PlantConditionAnalyzer.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.Infrastructure.Services
{
    public class DatabaseService : IDatabaseService
    {
        public async Task<CaptureSet> CreateCaptureSetAsync(string name, string? description)
        {
            using var context = new AppDbContext();
            CaptureSet toAdd= new CaptureSet() { Name=name, Description=description, CreatedAt=DateTime.Now };
            context.CaptureSets.Add(toAdd);
            await context.SaveChangesAsync();
            return toAdd; 
        }

        public async Task DeleteCaptureSetAsync(int id)
        {
            using var context = new AppDbContext();
            var set = await context.CaptureSets.FindAsync(id);
            if (set != null)
            {
                context.CaptureSets.Remove(set);
                await context.SaveChangesAsync();
            }
        }

        public async Task<List<CaptureSet>> GetCaptureSetsAsync()
        {
            using var context = new AppDbContext();
            return await context.CaptureSets.OrderByDescending(i => i.CreatedAt).ToListAsync();
        }

        public async Task<List<Snapshot>> GetSnapshotsForSetAsync(int captureSetId)
        {
            using var context = new AppDbContext();
            return await context.Snapshots.Where(i=> i.CaptureSetId==captureSetId).OrderByDescending(i=>i.Timestamp).ToListAsync();
        }

        public async Task InitializeAsync()
        {
            using var context = new AppDbContext();
            await context.Database.EnsureCreatedAsync();
        }

        public async Task SaveSnapshotAsync(Snapshot snapshot)
        {
            using var context = new AppDbContext();
            context.Snapshots.Add(snapshot);
            await context.SaveChangesAsync();
        }
    }
}
