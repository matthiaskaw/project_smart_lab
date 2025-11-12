namespace SmartLab.Domains.Analysis.Models
{
    /// <summary>
    /// Result of script validation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public ValidationStatus Status { get; set; }
        public List<ValidationError> Errors { get; set; } = new();
        public List<ValidationWarning> Warnings { get; set; } = new();

        public static ValidationResult Success()
        {
            return new ValidationResult
            {
                IsValid = true,
                Status = ValidationStatus.Passed
            };
        }

        public static ValidationResult Failed(string errorMessage)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = ValidationStatus.Failed,
                Errors = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Code = "VALIDATION_FAILED",
                        Message = errorMessage
                    }
                }
            };
        }
    }

    public class ValidationError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? LineNumber { get; set; }
    }

    public class ValidationWarning
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? LineNumber { get; set; }
    }
}
