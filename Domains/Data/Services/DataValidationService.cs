using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Data.Models;
using Microsoft.Extensions.Logging;

namespace SmartLab.Domains.Data.Services
{
    public class DataValidationService : IDataValidationService
    {
        private readonly ILogger<DataValidationService> _logger;

        public DataValidationService(ILogger<DataValidationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public DataValidationResult ValidateDataPoints(List<ManualDataPoint> dataPoints)
        {
            try
            {
                var result = new DataValidationResult
                {
                    TotalRows = dataPoints.Count,
                    DetectedParameters = dataPoints.Select(dp => dp.ParameterName).Distinct().ToList()
                };

                var allErrors = new List<ValidationError>();
                var allWarnings = new List<ValidationError>();

                // Run all validation checks
                allErrors.AddRange(ValidateTimestamps(dataPoints));
                allErrors.AddRange(ValidateNumericValues(dataPoints));
                allErrors.AddRange(ValidateParameterConsistency(dataPoints));
                
                allWarnings.AddRange(ValidateDuplicates(dataPoints));
                allWarnings.AddRange(ValidateDataRanges(dataPoints));
                allWarnings.AddRange(ValidateDataCompleteness(dataPoints));

                result.Errors = allErrors;
                result.Warnings = allWarnings;
                result.IsValid = !allErrors.Any();
                result.ValidRows = dataPoints.Count - allErrors.Where(e => e.RowIndex.HasValue).Select(e => e.RowIndex).Distinct().Count();

                _logger.LogInformation("Validation completed: {TotalRows} rows, {ErrorCount} errors, {WarningCount} warnings", 
                    result.TotalRows, result.Errors.Count, result.Warnings.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate data points");
                throw;
            }
        }

        public List<ValidationError> ValidateTimestamps(List<ManualDataPoint> dataPoints)
        {
            var errors = new List<ValidationError>();

            foreach (var dataPoint in dataPoints)
            {
                // Check for future timestamps
                if (dataPoint.Timestamp > DateTime.UtcNow.AddDays(1))
                {
                    errors.Add(new ValidationError
                    {
                        ErrorType = "FutureTimestamp",
                        Message = $"Timestamp is in the future: {dataPoint.Timestamp:yyyy-MM-dd HH:mm:ss}",
                        RowIndex = dataPoint.RowIndex,
                        ParameterName = dataPoint.ParameterName,
                        Value = dataPoint.Timestamp.ToString()
                    });
                }

                // Check for very old timestamps (older than 50 years)
                if (dataPoint.Timestamp < DateTime.UtcNow.AddYears(-50))
                {
                    errors.Add(new ValidationError
                    {
                        ErrorType = "VeryOldTimestamp",
                        Message = $"Timestamp is very old: {dataPoint.Timestamp:yyyy-MM-dd HH:mm:ss}",
                        RowIndex = dataPoint.RowIndex,
                        ParameterName = dataPoint.ParameterName,
                        Value = dataPoint.Timestamp.ToString()
                    });
                }

                // Check for invalid timestamps (default DateTime)
                if (dataPoint.Timestamp == default(DateTime))
                {
                    errors.Add(new ValidationError
                    {
                        ErrorType = "InvalidTimestamp",
                        Message = "Timestamp is not set or invalid",
                        RowIndex = dataPoint.RowIndex,
                        ParameterName = dataPoint.ParameterName,
                        Value = dataPoint.Timestamp.ToString()
                    });
                }
            }

            return errors;
        }

        public List<ValidationError> ValidateNumericValues(List<ManualDataPoint> dataPoints)
        {
            var errors = new List<ValidationError>();

            // Group by parameter to check for numeric consistency
            var parameterGroups = dataPoints.GroupBy(dp => dp.ParameterName);

            foreach (var group in parameterGroups)
            {
                var numericValues = new List<double>();
                var hasNumericValues = false;
                var hasNonNumericValues = false;

                foreach (var dataPoint in group)
                {
                    if (double.TryParse(dataPoint.Value, out var numericValue))
                    {
                        numericValues.Add(numericValue);
                        hasNumericValues = true;

                        // Check for extreme values
                        if (double.IsInfinity(numericValue) || double.IsNaN(numericValue))
                        {
                            errors.Add(new ValidationError
                            {
                                ErrorType = "InvalidNumericValue",
                                Message = $"Invalid numeric value: {dataPoint.Value}",
                                RowIndex = dataPoint.RowIndex,
                                ParameterName = dataPoint.ParameterName,
                                Value = dataPoint.Value
                            });
                        }
                        else if (Math.Abs(numericValue) > 1e15)
                        {
                            errors.Add(new ValidationError
                            {
                                ErrorType = "ExtremeValue",
                                Message = $"Extremely large value: {dataPoint.Value}",
                                RowIndex = dataPoint.RowIndex,
                                ParameterName = dataPoint.ParameterName,
                                Value = dataPoint.Value
                            });
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(dataPoint.Value))
                    {
                        hasNonNumericValues = true;
                    }
                }

                // If parameter has both numeric and non-numeric values, flag as inconsistent
                if (hasNumericValues && hasNonNumericValues)
                {
                    errors.Add(new ValidationError
                    {
                        ErrorType = "InconsistentDataType",
                        Message = $"Parameter '{group.Key}' has mixed numeric and non-numeric values",
                        ParameterName = group.Key
                    });
                }
            }

            return errors;
        }

        public List<ValidationError> ValidateDuplicates(List<ManualDataPoint> dataPoints)
        {
            var warnings = new List<ValidationError>();

            // Group by timestamp and parameter to find exact duplicates
            var duplicateGroups = dataPoints
                .GroupBy(dp => new { dp.Timestamp, dp.ParameterName })
                .Where(g => g.Count() > 1);

            foreach (var group in duplicateGroups)
            {
                var duplicates = group.ToList();
                for (int i = 1; i < duplicates.Count; i++) // Skip first occurrence
                {
                    warnings.Add(new ValidationError
                    {
                        ErrorType = "DuplicateEntry",
                        Message = $"Duplicate entry for parameter '{group.Key.ParameterName}' at timestamp {group.Key.Timestamp:yyyy-MM-dd HH:mm:ss}",
                        RowIndex = duplicates[i].RowIndex,
                        ParameterName = group.Key.ParameterName,
                        Value = duplicates[i].Value
                    });
                }
            }

            return warnings;
        }

        public List<ValidationError> ValidateParameterConsistency(List<ManualDataPoint> dataPoints)
        {
            var errors = new List<ValidationError>();

            foreach (var dataPoint in dataPoints)
            {
                // Check for empty parameter names
                if (string.IsNullOrWhiteSpace(dataPoint.ParameterName))
                {
                    errors.Add(new ValidationError
                    {
                        ErrorType = "EmptyParameterName",
                        Message = "Parameter name is empty or null",
                        RowIndex = dataPoint.RowIndex,
                        Value = dataPoint.Value
                    });
                }

                // Check for empty values
                if (string.IsNullOrWhiteSpace(dataPoint.Value))
                {
                    errors.Add(new ValidationError
                    {
                        ErrorType = "EmptyValue",
                        Message = "Value is empty or null",
                        RowIndex = dataPoint.RowIndex,
                        ParameterName = dataPoint.ParameterName
                    });
                }

                // Check for very long parameter names
                if (dataPoint.ParameterName?.Length > 100)
                {
                    errors.Add(new ValidationError
                    {
                        ErrorType = "ParameterNameTooLong",
                        Message = $"Parameter name is too long ({dataPoint.ParameterName.Length} characters, max 100)",
                        RowIndex = dataPoint.RowIndex,
                        ParameterName = dataPoint.ParameterName,
                        Value = dataPoint.Value
                    });
                }

                // Check for very long values
                if (dataPoint.Value?.Length > 1000)
                {
                    errors.Add(new ValidationError
                    {
                        ErrorType = "ValueTooLong",
                        Message = $"Value is too long ({dataPoint.Value.Length} characters, max 1000)",
                        RowIndex = dataPoint.RowIndex,
                        ParameterName = dataPoint.ParameterName,
                        Value = dataPoint.Value?.Substring(0, 50) + "..."
                    });
                }

                // Check for invalid characters in parameter names
                if (!string.IsNullOrEmpty(dataPoint.ParameterName) && 
                    dataPoint.ParameterName.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != ' ' && c != '.'))
                {
                    errors.Add(new ValidationError
                    {
                        ErrorType = "InvalidParameterName",
                        Message = $"Parameter name contains invalid characters: {dataPoint.ParameterName}",
                        RowIndex = dataPoint.RowIndex,
                        ParameterName = dataPoint.ParameterName,
                        Value = dataPoint.Value
                    });
                }
            }

            return errors;
        }

        private List<ValidationError> ValidateDataRanges(List<ManualDataPoint> dataPoints)
        {
            var warnings = new List<ValidationError>();

            // Group by parameter to analyze ranges
            var parameterGroups = dataPoints.GroupBy(dp => dp.ParameterName);

            foreach (var group in parameterGroups)
            {
                var numericValues = group
                    .Select(dp => double.TryParse(dp.Value, out var val) ? (double?)val : null)
                    .Where(v => v.HasValue)
                    .Select(v => v.Value)
                    .ToList();

                if (numericValues.Count > 1)
                {
                    var min = numericValues.Min();
                    var max = numericValues.Max();
                    var range = max - min;
                    var mean = numericValues.Average();
                    var stdDev = Math.Sqrt(numericValues.Select(v => Math.Pow(v - mean, 2)).Average());

                    // Check for outliers (values more than 3 standard deviations from mean)
                    if (stdDev > 0)
                    {
                        foreach (var dataPoint in group)
                        {
                            if (double.TryParse(dataPoint.Value, out var value))
                            {
                                var zScore = Math.Abs((value - mean) / stdDev);
                                if (zScore > 3)
                                {
                                    warnings.Add(new ValidationError
                                    {
                                        ErrorType = "PossibleOutlier",
                                        Message = $"Value {value} appears to be an outlier for parameter '{group.Key}' (z-score: {zScore:F2})",
                                        RowIndex = dataPoint.RowIndex,
                                        ParameterName = dataPoint.ParameterName,
                                        Value = dataPoint.Value
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return warnings;
        }

        private List<ValidationError> ValidateDataCompleteness(List<ManualDataPoint> dataPoints)
        {
            var warnings = new List<ValidationError>();

            // Check for parameters with very few data points
            var parameterGroups = dataPoints.GroupBy(dp => dp.ParameterName);
            var totalDataPoints = dataPoints.Count;

            foreach (var group in parameterGroups)
            {
                var parameterCount = group.Count();
                var parameterCoverage = (double)parameterCount / totalDataPoints;

                // Warn if a parameter has less than 10% coverage
                if (parameterCoverage < 0.1 && totalDataPoints > 10)
                {
                    warnings.Add(new ValidationError
                    {
                        ErrorType = "LowDataCoverage",
                        Message = $"Parameter '{group.Key}' has low data coverage ({parameterCoverage:P1} of total data points)",
                        ParameterName = group.Key
                    });
                }
            }

            // Check for large gaps in timestamps
            var sortedDataPoints = dataPoints.OrderBy(dp => dp.Timestamp).ToList();
            for (int i = 1; i < sortedDataPoints.Count; i++)
            {
                var timeDiff = sortedDataPoints[i].Timestamp - sortedDataPoints[i - 1].Timestamp;
                
                // Check for gaps larger than 24 hours
                if (timeDiff.TotalHours > 24)
                {
                    warnings.Add(new ValidationError
                    {
                        ErrorType = "LargeTimeGap",
                        Message = $"Large time gap detected: {timeDiff.TotalHours:F1} hours between measurements",
                        RowIndex = sortedDataPoints[i].RowIndex
                    });
                }
            }

            return warnings;
        }
    }
}