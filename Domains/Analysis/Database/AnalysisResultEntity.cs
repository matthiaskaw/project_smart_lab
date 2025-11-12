using System.ComponentModel.DataAnnotations;
using SmartLab.Domains.Data.Models;

namespace SmartLab.Domains.Analysis.Database
{
    /// <summary>
    /// Database entity for storing analysis execution results.
    /// </summary>
    public class AnalysisResultEntity
    {
        public Guid Id { get; set; }

        public Guid DatasetId { get; set; }

        public Guid? ScriptId { get; set; }  // Nullable - script may be deleted

        [Required]
        [MaxLength(255)]
        public string ScriptName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ScriptLanguage { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? ScriptVersion { get; set; }

        public DateTime ExecutionDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// User-provided parameters as JSON.
        /// </summary>
        public string? ParametersJson { get; set; }

        [MaxLength(500)]
        public string? ResultImagePath { get; set; }

        /// <summary>
        /// Script output statistics as JSON.
        /// </summary>
        public string? ResultDataJson { get; set; }

        public string? ErrorMessage { get; set; }

        public int ExecutionTimeMs { get; set; }

        /// <summary>
        /// Optional snapshot of script content for reproducibility.
        /// </summary>
        public string? ScriptContentSnapshot { get; set; }

        // Navigation property
        public virtual DatasetEntity? Dataset { get; set; }
    }
}
