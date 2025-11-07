using System.ComponentModel.DataAnnotations;

namespace SmartLab.Domains.Data.Models
{
    public enum DataSource
    {
        Manual = 0,
        Import = 1,
        Device = 2
    }

    public enum EntryMethod
    {
        WebForm = 0,
        CsvUpload = 1,
        DirectDevice = 2,
        DeviceMeasurement = 3
    }

    public class DatasetEntity
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public DataSource DataSource { get; set; }

        public EntryMethod EntryMethod { get; set; }

        public Guid? DeviceId { get; set; }

        [MaxLength(500)]
        public string? OriginalFilename { get; set; }

        [MaxLength(1000)]
        public string? FilePath { get; set; }

        /// <summary>
        /// Raw data as received from device (JSON array of strings).
        /// Device controls the format - this is exported as-is.
        /// </summary>
        public string? RawDataJson { get; set; }

        public virtual ICollection<DataPointEntity> DataPoints { get; set; } = new List<DataPointEntity>();

        public virtual ICollection<ValidationErrorEntity> ValidationErrors { get; set; } = new List<ValidationErrorEntity>();
    }

    public class DataPointEntity
    {
        public long Id { get; set; }

        public Guid DatasetId { get; set; }

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

        public virtual DatasetEntity? Dataset { get; set; }
    }

    public class ValidationErrorEntity
    {
        public long Id { get; set; }

        public Guid DatasetId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ErrorType { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        public int? RowIndex { get; set; }

        [MaxLength(100)]
        public string? ParameterName { get; set; }

        public DateTime CreatedDate { get; set; }

        public virtual DatasetEntity? Dataset { get; set; }
    }

    public class DeviceConfigurationEntity
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string DeviceType { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime ModifiedDate { get; set; }

        [Required]
        public string ConfigurationJson { get; set; } = "{}";
    }
}
