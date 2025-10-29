using Microsoft.EntityFrameworkCore;
using SmartLab.Domains.Data.Database;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Data.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SmartLab.Domains.Data.Services
{
    public class DataService : IDataService
    {
        private readonly SmartLabDbContext _context;
        private readonly IDataImportService _importService;
        private readonly IDataValidationService _validationService;
        private readonly ILogger<DataService> _logger;

        public DataService(
            SmartLabDbContext context,
            IDataImportService importService,
            IDataValidationService validationService,
            ILogger<DataService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Guid> CreateDatasetAsync(DatasetEntity dataset)
        {
            try
            {
                dataset.Id = Guid.NewGuid();
                dataset.CreatedDate = DateTime.UtcNow;

                _context.Datasets.Add(dataset);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created dataset {DatasetId} with name '{Name}'", dataset.Id, dataset.Name);
                return dataset.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create dataset with name '{Name}'", dataset.Name);
                throw;
            }
        }

        public async Task<DatasetEntity?> GetDatasetAsync(Guid id)
        {
            try
            {
                return await _context.Datasets
                    .Include(d => d.DataPoints)
                    .Include(d => d.ValidationErrors)
                    .FirstOrDefaultAsync(d => d.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get dataset {DatasetId}", id);
                throw;
            }
        }

        public async Task<List<DatasetSummary>> GetDatasetSummariesAsync()
        {
            try
            {
                var datasets = await _context.Datasets
                    .Include(d => d.ValidationErrors)
                    .ToListAsync();

                return datasets.Select(d => new DatasetSummary
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    CreatedDate = d.CreatedDate,
                    DataSource = d.DataSource,
                    EntryMethod = d.EntryMethod,
                    HasValidationErrors = d.ValidationErrors.Any(),
                    ValidationErrorCount = d.ValidationErrors.Count
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get dataset summaries");
                throw;
            }
        }

        public async Task<bool> DeleteDatasetAsync(Guid id)
        {
            try
            {
                var dataset = await _context.Datasets.FindAsync(id);
                if (dataset == null) return false;

                _context.Datasets.Remove(dataset);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted dataset {DatasetId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete dataset {DatasetId}", id);
                throw;
            }
        }

        public async Task<bool> UpdateDatasetAsync(DatasetEntity dataset)
        {
            try
            {
                _context.Datasets.Update(dataset);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated dataset {DatasetId}", dataset.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update dataset {DatasetId}", dataset.Id);
                throw;
            }
        }

        public async Task<bool> AddDataPointsAsync(Guid datasetId, List<DataPointEntity> dataPoints)
        {
            try
            {
                foreach (var point in dataPoints)
                {
                    point.DatasetId = datasetId;
                }

                _context.DataPoints.AddRange(dataPoints);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Added {Count} data points to dataset {DatasetId}", dataPoints.Count, datasetId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add data points to dataset {DatasetId}", datasetId);
                throw;
            }
        }

        public async Task<List<DataPointEntity>> GetDataPointsAsync(Guid datasetId)
        {
            try
            {
                return await _context.DataPoints
                    .Where(dp => dp.DatasetId == datasetId)
                    .OrderBy(dp => dp.Timestamp)
                    .ThenBy(dp => dp.RowIndex)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get data points for dataset {DatasetId}", datasetId);
                throw;
            }
        }

        public async Task<List<DataPointEntity>> GetDataPointsByParameterAsync(Guid datasetId, string parameterName)
        {
            try
            {
                return await _context.DataPoints
                    .Where(dp => dp.DatasetId == datasetId && dp.ParameterName == parameterName)
                    .OrderBy(dp => dp.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get data points for parameter '{Parameter}' in dataset {DatasetId}", 
                    parameterName, datasetId);
                throw;
            }
        }

        public async Task<Guid> CreateManualDatasetAsync(ManualDatasetRequest request)
        {
            try
            {
                // Convert manual entry to CSV-formatted lines
                var lines = new List<string>
                {
                    "Timestamp,Parameter,Value,Unit,Notes" // CSV header
                };

                foreach (var dp in request.DataPoints.OrderBy(d => d.RowIndex))
                {
                    var line = $"{dp.Timestamp:yyyy-MM-dd HH:mm:ss},{EscapeCsv(dp.ParameterName)},{EscapeCsv(dp.Value)},{EscapeCsv(dp.Unit ?? "")},{EscapeCsv(dp.Notes ?? "")}";
                    lines.Add(line);
                }

                // Store as raw data - format-agnostic
                var dataset = new DatasetEntity
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Description = request.Description,
                    CreatedDate = request.MeasurementDate ?? DateTime.UtcNow,
                    DataSource = DataSource.Manual,
                    EntryMethod = EntryMethod.WebForm,
                    RawDataJson = JsonSerializer.Serialize(lines)
                };

                _context.Datasets.Add(dataset);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created manual dataset {DatasetId} with {LineCount} lines",
                    dataset.Id, lines.Count);

                return dataset.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create manual dataset '{Name}'", request.Name);
                throw;
            }
        }

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

        public async Task<DataValidationResult> ValidateManualDataAsync(List<ManualDataPoint> dataPoints)
        {
            return await Task.FromResult(_validationService.ValidateDataPoints(dataPoints));
        }

        public async Task<ImportPreview> PreviewImportAsync(Stream fileStream, ImportOptions options)
        {
            try
            {
                return await _importService.PreviewCsvAsync(fileStream, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to preview import file");
                throw;
            }
        }

        public async Task<Guid> ImportDatasetAsync(ImportRequest request)
        {
            try
            {
                // Read file as raw text lines - no parsing, no interpretation
                request.File.OpenReadStream().Position = 0;
                using var reader = new StreamReader(request.File.OpenReadStream());
                var rawLines = new List<string>();
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    rawLines.Add(line);
                }

                // Store exactly as uploaded - format-agnostic
                var dataset = new DatasetEntity
                {
                    Id = Guid.NewGuid(),
                    Name = request.DatasetName,
                    Description = request.Description,
                    CreatedDate = DateTime.UtcNow,
                    DataSource = DataSource.Import,
                    EntryMethod = EntryMethod.CsvUpload,
                    OriginalFilename = request.File.FileName,
                    RawDataJson = JsonSerializer.Serialize(rawLines)
                };

                _context.Datasets.Add(dataset);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Imported dataset {DatasetId} from file '{FileName}' with {LineCount} lines",
                    dataset.Id, request.File.FileName, rawLines.Count);

                return dataset.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import dataset from file '{FileName}'", request.File.FileName);
                throw;
            }
        }

        public async Task<DataValidationResult> ValidateImportDataAsync(Stream fileStream, ImportOptions options)
        {
            try
            {
                return await _importService.ValidateDataAsync(await _importService.ImportCsvAsync(fileStream, options));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate import data");
                throw;
            }
        }

        public async Task<bool> AddValidationErrorsAsync(Guid datasetId, List<ValidationError> errors)
        {
            try
            {
                var errorEntities = errors.Select(e => new ValidationErrorEntity
                {
                    DatasetId = datasetId,
                    ErrorType = e.ErrorType,
                    Message = e.Message,
                    RowIndex = e.RowIndex,
                    ParameterName = e.ParameterName,
                    CreatedDate = DateTime.UtcNow
                }).ToList();

                _context.ValidationErrors.AddRange(errorEntities);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add validation errors for dataset {DatasetId}", datasetId);
                throw;
            }
        }

        public async Task<List<ValidationErrorEntity>> GetValidationErrorsAsync(Guid datasetId)
        {
            try
            {
                return await _context.ValidationErrors
                    .Where(ve => ve.DatasetId == datasetId)
                    .OrderBy(ve => ve.RowIndex)
                    .ThenBy(ve => ve.ParameterName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get validation errors for dataset {DatasetId}", datasetId);
                throw;
            }
        }

    }
}