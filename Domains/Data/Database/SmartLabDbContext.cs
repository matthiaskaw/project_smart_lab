using Microsoft.EntityFrameworkCore;
using SmartLab.Domains.Data.Models;
using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Analysis.Database;

namespace SmartLab.Domains.Data.Database
{
    public class SmartLabDbContext : DbContext
    {


        private readonly IDeviceRepository _deviceRepository;
        public SmartLabDbContext(DbContextOptions<SmartLabDbContext> options) : base(options)
        {
        }

        public DbSet<DatasetEntity> Datasets { get; set; }
        public DbSet<DataPointEntity> DataPoints { get; set; }
        public DbSet<ValidationErrorEntity> ValidationErrors { get; set; }
        public DbSet<DeviceConfigurationEntity> DeviceConfigurations { get; set; }

        // Analysis domain entities
        public DbSet<AnalysisResultEntity> AnalysisResults { get; set; }
        public DbSet<ScriptMetadataEntity> ScriptMetadata { get; set; }

        /// <summary>
        /// Configures SQLite PRAGMA settings for optimal performance.
        /// Should be called after database migrations have been applied.
        /// </summary>
        public void ConfigureSqlitePragmas()
        {
            // Configure SQLite PRAGMA settings for optimal performance
            Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");           // Write-Ahead Logging for better concurrency
            Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");         // Balance between speed and safety
            Database.ExecuteSqlRaw("PRAGMA cache_size=-64000;");          // 64MB cache
            Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON;");            // Enable foreign key constraints
            Database.ExecuteSqlRaw("PRAGMA temp_store=MEMORY;");          // Store temp tables in memory
            Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");          // Wait up to 5 seconds on locks
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Dataset entity
            modelBuilder.Entity<DatasetEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.DataSource).HasConversion<int>();
                entity.Property(e => e.EntryMethod).HasConversion<int>();

                entity.HasIndex(e => e.CreatedDate);
                entity.HasIndex(e => e.DataSource);
                entity.HasIndex(e => e.DeviceId);
            });

            // Configure DataPoint entity
            modelBuilder.Entity<DataPointEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.HasIndex(e => new { e.DatasetId, e.Timestamp });
                entity.HasIndex(e => e.ParameterName);
                entity.HasIndex(e => e.RowIndex);

                entity.HasOne(d => d.Dataset)
                      .WithMany(p => p.DataPoints)
                      .HasForeignKey(d => d.DatasetId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure ValidationError entity
            modelBuilder.Entity<ValidationErrorEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("datetime('now')");

                entity.HasIndex(e => e.DatasetId);
                entity.HasIndex(e => e.ErrorType);

                entity.HasOne(d => d.Dataset)
                      .WithMany(p => p.ValidationErrors)
                      .HasForeignKey(d => d.DatasetId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure DeviceConfiguration entity
            modelBuilder.Entity<DeviceConfigurationEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.ModifiedDate).HasDefaultValueSql("datetime('now')");

                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.DeviceType);
                entity.HasIndex(e => e.IsActive);
            });

            // Configure AnalysisResult entity
            modelBuilder.Entity<AnalysisResultEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.ExecutionDate).HasDefaultValueSql("datetime('now')");

                entity.HasIndex(e => e.DatasetId);
                entity.HasIndex(e => e.ScriptId);
                entity.HasIndex(e => e.ExecutionDate);
                entity.HasIndex(e => e.Status);

                entity.HasOne(d => d.Dataset)
                      .WithMany()
                      .HasForeignKey(d => d.DatasetId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure ScriptMetadata entity
            modelBuilder.Entity<ScriptMetadataEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.UploadDate).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.LastModified).HasDefaultValueSql("datetime('now')");

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Language);
                entity.HasIndex(e => e.IsShared);
                entity.HasIndex(e => e.IsBuiltIn);
                entity.HasIndex(e => e.ValidationStatus);
            });
        }
    }
}
