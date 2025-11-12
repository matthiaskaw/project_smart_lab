using System.ComponentModel.DataAnnotations;

namespace SmartLab.Domains.Analysis.Database
{
    /// <summary>
    /// Database entity for storing script metadata.
    /// </summary>
    public class ScriptMetadataEntity
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(255)]
        public string Author { get; set; } = string.Empty;

        public DateTime UploadDate { get; set; }

        public DateTime LastModified { get; set; }

        [MaxLength(50)]
        public string Version { get; set; } = "1.0.0";

        [Required]
        [MaxLength(50)]
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Tags as JSON array.
        /// </summary>
        public string? TagsJson { get; set; }

        /// <summary>
        /// Script parameters schema as JSON.
        /// </summary>
        public string? ParametersJson { get; set; }

        [Required]
        [MaxLength(50)]
        public string ValidationStatus { get; set; } = string.Empty;

        /// <summary>
        /// Validation errors as JSON array.
        /// </summary>
        public string? ValidationErrorsJson { get; set; }

        /// <summary>
        /// Validation warnings as JSON array.
        /// </summary>
        public string? ValidationWarningsJson { get; set; }

        public bool IsShared { get; set; }

        public bool IsBuiltIn { get; set; }

        public int ExecutionCount { get; set; }

        public DateTime? LastExecuted { get; set; }

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;
    }
}
