namespace SmartLab.Domains.Analysis.Models
{
    /// <summary>
    /// Supported script languages for data analysis.
    /// </summary>
    public enum ScriptLanguage
    {
        Python = 0,
        R = 1,
        Julia = 2,
        Shell = 3
    }

    /// <summary>
    /// Status of script validation.
    /// </summary>
    public enum ValidationStatus
    {
        Passed = 0,    // No issues, safe to execute
        Warning = 1,   // Potential issues but can execute
        Failed = 2     // Security or syntax errors, cannot execute
    }

    /// <summary>
    /// Status of analysis execution.
    /// </summary>
    public enum AnalysisStatus
    {
        Queued = 0,
        Running = 1,
        Success = 2,
        Failed = 3,
        Timeout = 4,
        Cancelled = 5
    }

    /// <summary>
    /// Type of script parameter for validation and UI rendering.
    /// </summary>
    public enum ParameterType
    {
        Number = 0,
        String = 1,
        Boolean = 2,
        Select = 3,
        Color = 4
    }
}
