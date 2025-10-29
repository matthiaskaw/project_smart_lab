using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartLab.Domains.Data.Database;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Data.Models;
using System.Text;
using System.Text.Json;

namespace SmartLab.Domains.Data.Services
{
    /// <summary>
    /// Exports datasets in their original format as sent by the device.
    /// No transformation or format conversion - device controls the format entirely.
    /// </summary>
    public class DataExportService : IDataExportService
    {
        private readonly SmartLabDbContext _context;
        private readonly ILogger<DataExportService> _logger;

        public DataExportService(
            SmartLabDbContext context,
            ILogger<DataExportService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Exports raw data exactly as device sent it.
        /// Device controls format - could be CSV, JSON, or any other format.
        /// </summary>
        public async Task<byte[]> ExportToCsvAsync(Guid datasetId)
        {
            return await ExportRawDataAsync(datasetId);
        }

        /// <summary>
        /// Exports raw data exactly as device sent it.
        /// Device controls format - could be CSV, JSON, or any other format.
        /// </summary>
        public async Task<byte[]> ExportToJsonAsync(Guid datasetId)
        {
            return await ExportRawDataAsync(datasetId);
        }

        /// <summary>
        /// Exports raw data from dataset without any transformation.
        /// </summary>
        private async Task<byte[]> ExportRawDataAsync(Guid datasetId)
        {
            try
            {
                _logger.LogInformation("Exporting data for dataset {DatasetId}", datasetId);

                // Get dataset with raw data
                var dataset = await _context.Datasets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == datasetId);

                if (dataset == null)
                {
                    throw new InvalidOperationException($"Dataset {datasetId} not found");
                }

                if (string.IsNullOrEmpty(dataset.RawDataJson))
                {
                    _logger.LogWarning("Dataset {DatasetId} has no data", datasetId);
                    return Encoding.UTF8.GetBytes("No data available");
                }

                return ExportFromRawDataJson(dataset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export dataset {DatasetId}", datasetId);
                throw;
            }
        }

        /// <summary>
        /// Exports from RawDataJson (device measurements)
        /// </summary>
        private byte[] ExportFromRawDataJson(DatasetEntity dataset)
        {
            var rawDataLines = JsonSerializer.Deserialize<List<string>>(dataset.RawDataJson!);

            if (rawDataLines == null || rawDataLines.Count == 0)
            {
                _logger.LogWarning("Dataset {DatasetId} has empty raw data", dataset.Id);
                return Encoding.UTF8.GetBytes("No data available");
            }

            // Join lines with newline - export exactly as device sent it
            var rawDataText = string.Join(Environment.NewLine, rawDataLines);
            var result = Encoding.UTF8.GetBytes(rawDataText);

            _logger.LogInformation("Exported {LineCount} raw lines from dataset {DatasetId} ({Size} bytes)",
                rawDataLines.Count, dataset.Id, result.Length);

            return result;
        }

    }
}
