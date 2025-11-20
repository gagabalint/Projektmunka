using Microsoft.EntityFrameworkCore;
using PlantConditionAnalyzer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.Infrastructure.Persistence
{
    public class AppDbContext:DbContext
    {
        public DbSet<CaptureSet> CaptureSets { get; set; }
        public DbSet<Snapshot> Snapshots { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PlantConditionAnalyzer_Data.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaptureSet>().HasMany(i => i.Snapshots).WithOne(i => i.CaptureSet).HasForeignKey(i => i.CaptureSetId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
