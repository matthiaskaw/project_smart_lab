namespace SmartLab.Domains.Analysis.Models
{
    /// <summary>
    /// Metadata describing an analysis script.
    /// </summary>
    public class AnalysisScriptMetadata
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; }
        public DateTime LastModified { get; set; }
        public string Version { get; set; } = "1.0.0";
        public ScriptLanguage Language { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<ScriptParameter> Parameters { get; set; } = new();
        public ValidationStatus ValidationStatus { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ValidationWarnings { get; set; } = new();
        public bool IsShared { get; set; }
        public bool IsBuiltIn { get; set; }
        public int ExecutionCount { get; set; }
        public DateTime? LastExecuted { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }
}
