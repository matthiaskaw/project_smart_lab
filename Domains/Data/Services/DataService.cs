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
                    .Include(d => d.DataPoints)
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
                    DataPointCount = d.DataPoints.Count,
                    ParameterCount = d.DataPoints.Select(dp => dp.ParameterName).Distinct().Count(),
                    FirstTimestamp = d.DataPoints.OrderBy(dp => dp.Timestamp).FirstOrDefault()?.Timestamp,
                    LastTimestamp = d.DataPoints.OrderByDescending(dp => dp.Timestamp).FirstOrDefault()?.Timestamp,
                    ParameterNames = d.DataPoints.Select(dp => dp.ParameterName).Distinct().ToList(),
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
                var validationResult = _validationService.ValidateDataPoints(request.DataPoints);
                
                var dataset = new DatasetEntity
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Description = request.Description,
                    CreatedDate = request.MeasurementDate ?? DateTime.UtcNow,
                    DataSource = DataSource.Manual,
                    EntryMethod = EntryMethod.WebForm
                };

                _context.Datasets.Add(dataset);

                var dataPoints = request.DataPoints.Select(dp => new DataPointEntity
                {
                    DatasetId = dataset.Id,
                    Timestamp = dp.Timestamp,
                    ParameterName = dp.ParameterName,
                    Value = dp.Value,
                    Unit = dp.Unit,
                    Notes = dp.Notes,
                    RowIndex = dp.RowIndex
                }).ToList();

                _context.DataPoints.AddRange(dataPoints);

                if (!validationResult.IsValid)
                {
                    var errorEntities = validationResult.Errors.Select(e => new ValidationErrorEntity
                    {
                        DatasetId = dataset.Id,
                        ErrorType = e.ErrorType,
                        Message = e.Message,
                        RowIndex = e.RowIndex,
                        ParameterName = e.ParameterName,
                        CreatedDate = DateTime.UtcNow
                    }).ToList();

                    _context.ValidationErrors.AddRange(errorEntities);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Created manual dataset {DatasetId} with {DataPointCount} data points", 
                    dataset.Id, dataPoints.Count);

                return dataset.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create manual dataset '{Name}'", request.Name);
                throw;
            }
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
                var dataPoints = await _importService.ImportCsvAsync(request.File.OpenReadStream(), request.Options);
                var validationResult = _validationService.ValidateDataPoints(dataPoints);

                var dataset = new DatasetEntity
                {
                    Id = Guid.NewGuid(),
                    Name = request.DatasetName,
                    Description = request.Description,
                    CreatedDate = DateTime.UtcNow,
                    DataSource = DataSource.Import,
                    EntryMethod = EntryMethod.CsvUpload,
                    OriginalFilename = request.File.FileName
                };

                _context.Datasets.Add(dataset);

                var dataPointEntities = dataPoints.Select(dp => new DataPointEntity
                {
                    DatasetId = dataset.Id,
                    Timestamp = dp.Timestamp,
                    ParameterName = dp.ParameterName,
                    Value = dp.Value,
                    Unit = dp.Unit,
                    Notes = dp.Notes,
                    RowIndex = dp.RowIndex
                }).ToList();

                _context.DataPoints.AddRange(dataPointEntities);

                if (!validationResult.IsValid)
                {
                    var errorEntities = validationResult.Errors.Concat(validationResult.Warnings)
                        .Select(e => new ValidationErrorEntity
                        {
                            DatasetId = dataset.Id,
                            ErrorType = e.ErrorType,
                            Message = e.Message,
                            RowIndex = e.RowIndex,
                            ParameterName = e.ParameterName,
                            CreatedDate = DateTime.UtcNow
                        }).ToList();

                    _context.ValidationErrors.AddRange(errorEntities);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Imported dataset {DatasetId} from file '{FileName}' with {DataPointCount} data points", 
                    dataset.Id, request.File.FileName, dataPointEntities.Count);

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