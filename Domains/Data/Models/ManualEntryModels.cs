using System.ComponentModel.DataAnnotations;

namespace SmartLab.Domains.Data.Models
{
    public class ManualDatasetRequest
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;
        
        public DateTime? MeasurementDate { get; set; }
        
        public List<ManualDataPoint> DataPoints { get; set; } = new();
        
        public List<string> ParameterNames { get; set; } = new();
        
        public Dictionary<string, string> ParameterUnits { get; set; } = new();
    }

    public class ManualDataPoint
    {
        public DateTime Timestamp { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ParameterName { get; set; } = string.Empty;
        
        [Required]
        public string Value { get; set; } = string.Empty;
        
        [MaxLength(50)]
        public string? Unit { get; set; }
        
        [MaxLength(500)]
        public string? Notes { get; set; }
        
        public int RowIndex { get; set; }
    }

    public class ImportRequest
    {
        [Required]
        public IFormFile File { get; set; } = null!;
        
        [Required]
        [MaxLength(255)]
        public string DatasetName { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;
        
        public ImportOptions Options { get; set; } = new();
    }

    public class ImportOptions
    {
        public bool HasHeader { get; set; } = true;

        public string TimestampColumn { get; set; } = "Timestamp";

        public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

        public DateTime? DefaultTimestamp { get; set; }

        public Dictionary<string, string> ColumnMapping { get; set; } = new();

        public char Delimiter { get; set; } = ',';

        public bool SkipEmptyRows { get; set; } = true;

        public int SkipRows { get; set; } = 0;
    }

    public class ValidationError
    {
        public string ErrorType { get; set; } = string.Empty;
        
        public string Message { get; set; } = string.Empty;
        
        public int? RowIndex { get; set; }
        
        public string? ParameterName { get; set; }
        
        public string? Value { get; set; }
    }

    public class DataValidationResult
    {
        public bool IsValid { get; set; }
        
        public List<ValidationError> Errors { get; set; } = new();
        
        public List<ValidationError> Warnings { get; set; } = new();
        
        public int TotalRows { get; set; }
        
        public int ValidRows { get; set; }
        
        public List<string> DetectedParameters { get; set; } = new();
    }

    public class ImportPreview
    {
        public List<string> Headers { get; set; } = new();
        
        public List<Dictionary<string, string>> SampleRows { get; set; } = new();
        
        public int TotalRows { get; set; }
        
        public List<string> DetectedParameters { get; set; } = new();
        
        public string? DetectedTimestampColumn { get; set; }
        
        public string? DetectedTimestampFormat { get; set; }
    }

    public class DatasetSummary
    {
        public Guid Id { get; set; }
        
        public string Name { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public DateTime CreatedDate { get; set; }
        
        public DataSource DataSource { get; set; }
        
        public EntryMethod EntryMethod { get; set; }
        
        public int DataPointCount { get; set; }
        
        public int ParameterCount { get; set; }
        
        public DateTime? FirstTimestamp { get; set; }
        
        public DateTime? LastTimestamp { get; set; }
        
        public List<string> ParameterNames { get; set; } = new();
        
        public bool HasValidationErrors { get; set; }
        
        public int ValidationErrorCount { get; set; }
    }
}