using SmartLab.Domains.Data.Models;

namespace SmartLab.Domains.Data.Interfaces
{
    public interface IDataService
    {
        // Dataset operations
        Task<Guid> CreateDatasetAsync(DatasetEntity dataset);
        Task<DatasetEntity?> GetDatasetAsync(Guid id);
        Task<List<DatasetSummary>> GetDatasetSummariesAsync();
        Task<bool> DeleteDatasetAsync(Guid id);
        Task<bool> UpdateDatasetAsync(DatasetEntity dataset);

        // Data point operations
        Task<bool> AddDataPointsAsync(Guid datasetId, List<DataPointEntity> dataPoints);
        Task<List<DataPointEntity>> GetDataPointsAsync(Guid datasetId);
        Task<List<DataPointEntity>> GetDataPointsByParameterAsync(Guid datasetId, string parameterName);

        // Manual entry operations
        Task<Guid> CreateManualDatasetAsync(ManualDatasetRequest request);
        Task<DataValidationResult> ValidateManualDataAsync(List<ManualDataPoint> dataPoints);

        // Import operations
        Task<ImportPreview> PreviewImportAsync(Stream fileStream, ImportOptions options);
        Task<Guid> ImportDatasetAsync(ImportRequest request);
        Task<DataValidationResult> ValidateImportDataAsync(Stream fileStream, ImportOptions options);

        // Validation operations
        Task<bool> AddValidationErrorsAsync(Guid datasetId, List<ValidationError> errors);
        Task<List<ValidationErrorEntity>> GetValidationErrorsAsync(Guid datasetId);
    }

    public interface IDataImportService
    {
        Task<ImportPreview> PreviewCsvAsync(Stream fileStream, ImportOptions options);
        Task<List<ManualDataPoint>> ImportCsvAsync(Stream fileStream, ImportOptions options);
        Task<ImportPreview> PreviewJsonAsync(Stream fileStream);
        Task<List<ManualDataPoint>> ImportJsonAsync(Stream fileStream);
        Task<DataValidationResult> ValidateDataAsync(List<ManualDataPoint> dataPoints);
    }

    public interface IDataValidationService
    {
        DataValidationResult ValidateDataPoints(List<ManualDataPoint> dataPoints);
        List<ValidationError> ValidateTimestamps(List<ManualDataPoint> dataPoints);
        List<ValidationError> ValidateNumericValues(List<ManualDataPoint> dataPoints);
        List<ValidationError> ValidateDuplicates(List<ManualDataPoint> dataPoints);
        List<ValidationError> ValidateParameterConsistency(List<ManualDataPoint> dataPoints);
    }
}