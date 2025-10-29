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
        /// Falls back to DataPoints if RawDataJson is not available (e.g., imported files).
        /// </summary>
        private async Task<byte[]> ExportRawDataAsync(Guid datasetId)
        {
            try
            {
                _logger.LogInformation("Exporting data for dataset {DatasetId}", datasetId);

                // Get dataset with raw data and data points
                var dataset = await _context.Datasets
                    .Include(d => d.DataPoints)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == datasetId);

                if (dataset == null)
                {
                    throw new InvalidOperationException($"Dataset {datasetId} not found");
                }

                // Try raw data first (device measurements)
                if (!string.IsNullOrEmpty(dataset.RawDataJson))
                {
                    return ExportFromRawDataJson(dataset);
                }

                // Fall back to DataPoints (imported files, manual entry)
                if (dataset.DataPoints.Any())
                {
                    return ExportFromDataPoints(dataset);
                }

                _logger.LogWarning("Dataset {DatasetId} has no data", datasetId);
                return Encoding.UTF8.GetBytes("No data available");
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

        /// <summary>
        /// Exports from DataPoints (imported/manual data)
        /// Recreates CSV format: Timestamp,Parameter,Value,Unit,Notes
        /// </summary>
        private byte[] ExportFromDataPoints(DatasetEntity dataset)
        {
            var lines = new List<string>
            {
                "Timestamp,Parameter,Value,Unit,Notes" // CSV header
            };

            var dataPoints = dataset.DataPoints
                .OrderBy(dp => dp.Timestamp)
                .ThenBy(dp => dp.RowIndex);

            foreach (var dp in dataPoints)
            {
                var line = $"{dp.Timestamp:yyyy-MM-dd HH:mm:ss},{EscapeCsv(dp.ParameterName)},{EscapeCsv(dp.Value)},{EscapeCsv(dp.Unit ?? "")},{EscapeCsv(dp.Notes ?? "")}";
                lines.Add(line);
            }

            var csvText = string.Join(Environment.NewLine, lines);
            var result = Encoding.UTF8.GetBytes(csvText);

            _logger.LogInformation("Exported {Count} data points from dataset {DatasetId} ({Size} bytes)",
                dataset.DataPoints.Count, dataset.Id, result.Length);

            return result;
        }

        /// <summary>
        /// Escapes CSV values (quotes strings containing commas, quotes, or newlines)
        /// </summary>
        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
