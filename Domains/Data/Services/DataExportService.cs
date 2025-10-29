using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartLab.Domains.Data.Database;
using SmartLab.Domains.Data.Interfaces;
using System.Globalization;
using System.Text.Json;

namespace SmartLab.Domains.Data.Services
{
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

        public async Task<byte[]> ExportToCsvAsync(Guid datasetId)
        {
            try
            {
                _logger.LogInformation("Exporting dataset {DatasetId} to CSV", datasetId);

                // Get dataset with all data points
                var dataset = await _context.Datasets
                    .Include(d => d.DataPoints)
                    .FirstOrDefaultAsync(d => d.Id == datasetId);

                if (dataset == null)
                {
                    throw new InvalidOperationException($"Dataset {datasetId} not found");
                }

                // Create memory stream for CSV data
                using var memoryStream = new MemoryStream();
                using var writer = new StreamWriter(memoryStream);
                using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true
                });

                // Write CSV records
                var records = dataset.DataPoints
                    .OrderBy(dp => dp.Timestamp)
                    .ThenBy(dp => dp.RowIndex)
                    .Select(dp => new DataPointExportRecord
                    {
                        Timestamp = dp.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        Parameter = dp.ParameterName,
                        Value = dp.Value,
                        Unit = dp.Unit ?? string.Empty,
                        Notes = dp.Notes ?? string.Empty
                    });

                await csv.WriteRecordsAsync(records);
                await writer.FlushAsync();

                var result = memoryStream.ToArray();
                _logger.LogInformation("Exported {Count} data points from dataset {DatasetId} to CSV ({Size} bytes)",
                    dataset.DataPoints.Count, datasetId, result.Length);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export dataset {DatasetId} to CSV", datasetId);
                throw;
            }
        }

        public async Task<byte[]> ExportToJsonAsync(Guid datasetId)
        {
            try
            {
                _logger.LogInformation("Exporting dataset {DatasetId} to JSON", datasetId);

                // Get dataset with all data points
                var dataset = await _context.Datasets
                    .Include(d => d.DataPoints)
                    .FirstOrDefaultAsync(d => d.Id == datasetId);

                if (dataset == null)
                {
                    throw new InvalidOperationException($"Dataset {datasetId} not found");
                }

                // Create export object
                var exportData = new
                {
                    dataset = new
                    {
                        id = dataset.Id,
                        name = dataset.Name,
                        description = dataset.Description,
                        createdDate = dataset.CreatedDate,
                        dataSource = dataset.DataSource.ToString(),
                        entryMethod = dataset.EntryMethod.ToString(),
                        deviceId = dataset.DeviceId,
                        originalFilename = dataset.OriginalFilename
                    },
                    dataPoints = dataset.DataPoints
                        .OrderBy(dp => dp.Timestamp)
                        .ThenBy(dp => dp.RowIndex)
                        .Select(dp => new
                        {
                            timestamp = dp.Timestamp,
                            parameter = dp.ParameterName,
                            value = dp.Value,
                            unit = dp.Unit,
                            notes = dp.Notes,
                            rowIndex = dp.RowIndex
                        })
                };

                // Serialize to JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(exportData, options);
                var result = System.Text.Encoding.UTF8.GetBytes(json);

                _logger.LogInformation("Exported {Count} data points from dataset {DatasetId} to JSON ({Size} bytes)",
                    dataset.DataPoints.Count, datasetId, result.Length);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export dataset {DatasetId} to JSON", datasetId);
                throw;
            }
        }

        /// <summary>
        /// Record format for CSV export
        /// </summary>
        private class DataPointExportRecord
        {
            public string Timestamp { get; set; } = string.Empty;
            public string Parameter { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;
        }
    }
}
