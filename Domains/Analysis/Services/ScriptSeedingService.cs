using SmartLab.Domains.Analysis.Database;
using SmartLab.Domains.Analysis.Models;
using SmartLab.Domains.Data.Database;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace SmartLab.Domains.Analysis.Services
{
    /// <summary>
    /// Service to seed built-in scripts into the database on startup.
    /// </summary>
    public class ScriptSeedingService
    {
        private readonly SmartLabDbContext _dbContext;
        private readonly ILogger<ScriptSeedingService> _logger;
        private readonly string _builtInScriptsPath;

        public ScriptSeedingService(
            SmartLabDbContext dbContext,
            ILogger<ScriptSeedingService> logger,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _logger = logger;

            var baseDir = configuration["Analysis:ScriptsDirectory"] ?? "analysis-scripts";
            _builtInScriptsPath = Path.Combine(baseDir, "built-in", "python");
        }

        public async Task SeedBuiltInScriptsAsync()
        {
            try
            {
                if (!Directory.Exists(_builtInScriptsPath))
                {
                    _logger.LogWarning("Built-in scripts directory not found: {Path}", _builtInScriptsPath);
                    return;
                }

                var scriptFiles = Directory.GetFiles(_builtInScriptsPath, "*.py");
                _logger.LogInformation("Found {Count} built-in script files", scriptFiles.Length);

                foreach (var filePath in scriptFiles)
                {
                    await SeedScriptAsync(filePath);
                }

                _logger.LogInformation("Built-in script seeding completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding built-in scripts");
            }
        }

        private async Task SeedScriptAsync(string filePath)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);

                // Check if script already exists
                var existing = await _dbContext.ScriptMetadata
                    .FirstOrDefaultAsync(s => s.IsBuiltIn && s.FileName == fileName);

                if (existing != null)
                {
                    _logger.LogDebug("Built-in script already exists: {FileName}", fileName);
                    return;
                }

                // Create metadata based on file name
                var displayName = GetDisplayName(fileName);
                var description = GetDescription(fileName);

                var entity = new ScriptMetadataEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = "system",
                    FileName = fileName,
                    DisplayName = displayName,
                    Description = description,
                    Author = "SmartLab Team",
                    UploadDate = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Version = "1.0.0",
                    Language = "Python",
                    TagsJson = JsonSerializer.Serialize(GetTags(fileName)),
                    ParametersJson = "[]",
                    ValidationStatus = "Passed",
                    ValidationErrorsJson = "[]",
                    ValidationWarningsJson = "[]",
                    IsShared = false,
                    IsBuiltIn = true,
                    ExecutionCount = 0,
                    FilePath = filePath
                };

                _dbContext.ScriptMetadata.Add(entity);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Seeded built-in script: {DisplayName}", displayName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding script: {FilePath}", filePath);
            }
        }

        private string GetDisplayName(string fileName)
        {
            return fileName switch
            {
                "basic_line_plot.py" => "Basic Line Plot",
                "statistical_summary.py" => "Statistical Summary",
                _ => Path.GetFileNameWithoutExtension(fileName)
                        .Replace('_', ' ')
                        .Replace('-', ' ')
                        .Trim()
            };
        }

        private string GetDescription(string fileName)
        {
            return fileName switch
            {
                "basic_line_plot.py" => "Creates a simple time-series line plot of measurement data with statistics.",
                "statistical_summary.py" => "Generates comprehensive statistical analysis with multi-panel visualizations including time series, histogram, box plot, and statistics table.",
                _ => "Built-in analysis script"
            };
        }

        private List<string> GetTags(string fileName)
        {
            return fileName switch
            {
                "basic_line_plot.py" => new List<string> { "visualization", "line-plot", "time-series" },
                "statistical_summary.py" => new List<string> { "statistics", "visualization", "analysis" },
                _ => new List<string> { "built-in" }
            };
        }
    }
}
