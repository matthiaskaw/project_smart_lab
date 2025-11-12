namespace SmartLab.Domains.Analysis.Models
{
    /// <summary>
    /// Defines a configurable parameter for an analysis script.
    /// </summary>
    public class ScriptParameter
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ParameterType Type { get; set; }
        public bool Required { get; set; }
        public object? DefaultValue { get; set; }

        // Validation constraints
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public string? Pattern { get; set; }  // Regex pattern for string validation
        public List<string>? AllowedValues { get; set; }  // For select/enum types
    }

    /// <summary>
    /// Result of parameter validation.
    /// </summary>
    public class ParameterValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();

        public static ParameterValidationResult Success() => new() { IsValid = true };

        public static ParameterValidationResult Failure(string error)
        {
            return new ParameterValidationResult
            {
                IsValid = false,
                Errors = new List<string> { error }
            };
        }
    }
}
