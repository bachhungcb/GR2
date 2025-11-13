using Microsoft.EntityFrameworkCore;
using SIEMServer.Model;

namespace SIEMServer.Context
{
    public sealed class SiemDbContext(DbContextOptions<SiemDbContext> options) : DbContext(options)
    {
        public DbSet<Agent> Agents { get; set; }
        public DbSet<ConnectionEntries> ConnectionEntries { get; set; }
        public DbSet<ProcessEntries> ProcessEntries { get; set; }
        public DbSet<BlacklistedProcess> BlacklistedProcesses { get; set; }
        public DbSet<TelemetrySnapshots> Snapshot { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Agent>()
                .HasMany<TelemetrySnapshots>(s => s.Snapshots)
                .WithOne(a => a.Agent);

            modelBuilder.Entity<TelemetrySnapshots>()
                .HasMany(c => c.ConnectionEntries)
                .WithOne(conn => conn.Snapshot);

            modelBuilder.Entity<TelemetrySnapshots>()
                .HasMany(p => p.ProcessEntries)
                .WithOne(p => p.Snapshot);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            
        }
    }
}
