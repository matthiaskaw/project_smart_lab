namespace SmartLab.Domains.Analysis.Models
{
    /// <summary>
    /// Result of an analysis script execution.
    /// </summary>
    public class AnalysisResult
    {
        public Guid Id { get; set; }
        public Guid DatasetId { get; set; }
        public Guid? ScriptId { get; set; }  // Nullable - script may be deleted
        public string ScriptName { get; set; } = string.Empty;
        public ScriptLanguage ScriptLanguage { get; set; }
        public string ScriptVersion { get; set; } = string.Empty;
        public DateTime ExecutionDate { get; set; }
        public AnalysisStatus Status { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string? ResultImagePath { get; set; }
        public Dictionary<string, object>? Statistics { get; set; }
        public string? ErrorMessage { get; set; }
        public int ExecutionTimeMs { get; set; }
        public string? ScriptContentSnapshot { get; set; }  // Optional: preserve script code
    }

    /// <summary>
    /// Result of script execution process.
    /// </summary>
    public class ProcessResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public int ExecutionTimeMs { get; set; }
    }

    /// <summary>
    /// Result of script upload operation.
    /// </summary>
    public class ScriptUploadResult
    {
        public bool Success { get; set; }
        public Guid? ScriptId { get; set; }
        public ValidationStatus ValidationStatus { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ValidationWarnings { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }
}
