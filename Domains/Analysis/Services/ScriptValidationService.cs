using SmartLab.Domains.Analysis.Interfaces;
using SmartLab.Domains.Analysis.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartLab.Domains.Analysis.Services
{
    /// <summary>
    /// Service for validating analysis scripts.
    /// </summary>
    public class ScriptValidationService : IScriptValidationService
    {
        private readonly IPlatformHelper _platformHelper;
        private readonly ILogger<ScriptValidationService> _logger;
        private readonly string _validatorScriptPath;

        public ScriptValidationService(
            IPlatformHelper platformHelper,
            ILogger<ScriptValidationService> logger,
            IConfiguration configuration)
        {
            _platformHelper = platformHelper;
            _logger = logger;

            // Get validator script path from configuration or use default
            var baseDir = configuration["Analysis:ScriptsDirectory"] ?? "analysis-scripts";
            _validatorScriptPath = Path.Combine(baseDir, "_validators", "validate_python.py");
        }

        public async Task<ValidationResult> ValidateScriptAsync(
            string scriptContent,
            ScriptLanguage language)
        {
            var result = new ValidationResult { IsValid = true, Status = ValidationStatus.Passed };

            try
            {
                // Step 1: Syntax validation
                var syntaxResult = await ValidateSyntaxAsync(scriptContent, language);
                if (!syntaxResult.IsValid)
                {
                    return syntaxResult;
                }

                // Step 2: Security validation (AST-based)
                var securityResult = await ValidateSecurityAsync(scriptContent, language);
                result.Errors.AddRange(securityResult.Errors);
                result.Warnings.AddRange(securityResult.Warnings);

                // Step 3: Structure validation
                var structureResult = ValidateStructure(scriptContent, language);
                result.Warnings.AddRange(structureResult.Warnings);

                // Determine final status
                if (result.Errors.Count > 0)
                {
                    result.IsValid = false;
                    result.Status = ValidationStatus.Failed;
                }
                else if (result.Warnings.Count > 0)
                {
                    result.Status = ValidationStatus.Warning;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during script validation");
                return ValidationResult.Failed($"Validation error: {ex.Message}");
            }

            return result;
        }

        public async Task<ValidationResult> ValidateSyntaxAsync(
            string scriptContent,
            ScriptLanguage language)
        {
            if (language != ScriptLanguage.Python)
            {
                return ValidationResult.Success();  // Only Python syntax validation for now
            }

            try
            {
                var tempFile = Path.GetTempFileName() + ".py";
                await File.WriteAllTextAsync(tempFile, scriptContent);

                var psi = new ProcessStartInfo
                {
                    FileName = _platformHelper.GetPythonCommand(),
                    Arguments = $"-m py_compile \"{tempFile}\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return ValidationResult.Failed("Failed to start Python process");
                }

                await process.WaitForExitAsync();
                var error = await process.StandardError.ReadToEndAsync();

                File.Delete(tempFile);

                if (process.ExitCode != 0)
                {
                    return ValidationResult.Failed($"Syntax error: {error}");
                }

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Python syntax");
                return ValidationResult.Failed($"Syntax validation error: {ex.Message}");
            }
        }

        public async Task<ValidationResult> ValidateSecurityAsync(
            string scriptContent,
            ScriptLanguage language)
        {
            var result = new ValidationResult { IsValid = true, Status = ValidationStatus.Passed };

            if (language != ScriptLanguage.Python)
            {
                return result;  // Only Python security validation for now
            }

            try
            {
                // Check if validator script exists
                if (!File.Exists(_validatorScriptPath))
                {
                    _logger.LogWarning("Python validator script not found at {Path}", _validatorScriptPath);
                    return PerformBasicSecurityValidation(scriptContent);
                }

                // Run AST-based validator
                var psi = new ProcessStartInfo
                {
                    FileName = _platformHelper.GetPythonCommand(),
                    Arguments = $"\"{_validatorScriptPath}\"",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return PerformBasicSecurityValidation(scriptContent);
                }

                // Write script content to validator's stdin
                await process.StandardInput.WriteAsync(scriptContent);
                process.StandardInput.Close();

                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("Validator exited with code {Code}", process.ExitCode);
                    return PerformBasicSecurityValidation(scriptContent);
                }

                // Parse validator output
                var validatorResult = JsonSerializer.Deserialize<ValidatorOutput>(output);
                if (validatorResult != null)
                {
                    result.Errors = validatorResult.Violations.Select(v => new ValidationError
                    {
                        Code = v.Code ?? "SECURITY_VIOLATION",
                        Message = v.Message ?? "Unknown violation",
                        LineNumber = v.Line
                    }).ToList();

                    result.Warnings = validatorResult.Warnings.Select(w => new ValidationWarning
                    {
                        Code = w.Code ?? "SECURITY_WARNING",
                        Message = w.Message ?? "Unknown warning",
                        LineNumber = w.Line
                    }).ToList();

                    if (result.Errors.Count > 0)
                    {
                        result.IsValid = false;
                        result.Status = ValidationStatus.Failed;
                    }
                    else if (result.Warnings.Count > 0)
                    {
                        result.Status = ValidationStatus.Warning;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during AST-based security validation");
                return PerformBasicSecurityValidation(scriptContent);
            }

            return result;
        }

        public ValidationResult ValidateStructure(
            string scriptContent,
            ScriptLanguage language)
        {
            var result = new ValidationResult { IsValid = true, Status = ValidationStatus.Passed };

            if (language != ScriptLanguage.Python)
            {
                return result;
            }

            // Check for required imports
            if (!scriptContent.Contains("import json"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "MISSING_IMPORT",
                    Message = "Script should import 'json' for I/O"
                });
            }

            if (!scriptContent.Contains("import sys"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "MISSING_IMPORT",
                    Message = "Script should import 'sys' for stdin access"
                });
            }

            // Check for main guard
            if (!scriptContent.Contains("if __name__"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "MISSING_MAIN_GUARD",
                    Message = "Script should use 'if __name__ == \"__main__\"' guard"
                });
            }

            if (result.Warnings.Count > 0)
            {
                result.Status = ValidationStatus.Warning;
            }

            return result;
        }

        public ParameterValidationResult ValidateParameters(
            Dictionary<string, object> providedParameters,
            List<ScriptParameter> schemaParameters)
        {
            var result = new ParameterValidationResult { IsValid = true };

            foreach (var schema in schemaParameters)
            {
                if (!providedParameters.TryGetValue(schema.Name, out var value))
                {
                    if (schema.Required)
                    {
                        result.Errors.Add($"Required parameter '{schema.Name}' is missing");
                        result.IsValid = false;
                    }
                    continue;
                }

                // Type validation
                switch (schema.Type)
                {
                    case ParameterType.Number:
                        if (!IsNumeric(value))
                        {
                            result.Errors.Add($"Parameter '{schema.Name}' must be a number");
                            result.IsValid = false;
                        }
                        else
                        {
                            var numValue = Convert.ToDouble(value);
                            if (schema.MinValue.HasValue && numValue < schema.MinValue.Value)
                            {
                                result.Errors.Add($"Parameter '{schema.Name}' must be >= {schema.MinValue.Value}");
                                result.IsValid = false;
                            }
                            if (schema.MaxValue.HasValue && numValue > schema.MaxValue.Value)
                            {
                                result.Errors.Add($"Parameter '{schema.Name}' must be <= {schema.MaxValue.Value}");
                                result.IsValid = false;
                            }
                        }
                        break;

                    case ParameterType.String:
                        if (value is not string strValue)
                        {
                            result.Errors.Add($"Parameter '{schema.Name}' must be a string");
                            result.IsValid = false;
                        }
                        else if (!string.IsNullOrEmpty(schema.Pattern))
                        {
                            if (!Regex.IsMatch(strValue, schema.Pattern))
                            {
                                result.Errors.Add($"Parameter '{schema.Name}' does not match required pattern");
                                result.IsValid = false;
                            }
                        }
                        break;

                    case ParameterType.Boolean:
                        if (value is not bool)
                        {
                            result.Errors.Add($"Parameter '{schema.Name}' must be a boolean");
                            result.IsValid = false;
                        }
                        break;

                    case ParameterType.Select:
                        if (schema.AllowedValues != null && !schema.AllowedValues.Contains(value?.ToString() ?? ""))
                        {
                            result.Errors.Add($"Parameter '{schema.Name}' must be one of: {string.Join(", ", schema.AllowedValues)}");
                            result.IsValid = false;
                        }
                        break;
                }
            }

            return result;
        }

        private ValidationResult PerformBasicSecurityValidation(string scriptContent)
        {
            var result = new ValidationResult { IsValid = true, Status = ValidationStatus.Passed };

            // Fallback: Basic string pattern matching
            var dangerousPatterns = new[]
            {
                "os.system", "subprocess.call", "subprocess.run", "subprocess.Popen",
                "eval(", "exec(", "__import__", "compile(",
                "socket.", "urllib.", "requests."
            };

            foreach (var pattern in dangerousPatterns)
            {
                if (scriptContent.Contains(pattern))
                {
                    result.Errors.Add(new ValidationError
                    {
                        Code = "DANGEROUS_PATTERN",
                        Message = $"Dangerous pattern detected: {pattern}"
                    });
                    result.IsValid = false;
                    result.Status = ValidationStatus.Failed;
                }
            }

            return result;
        }

        private bool IsNumeric(object value)
        {
            return value is int || value is long || value is float || value is double || value is decimal;
        }

        // Helper class for deserializing validator output
        private class ValidatorOutput
        {
            public List<ValidatorViolation> Violations { get; set; } = new();
            public List<ValidatorViolation> Warnings { get; set; } = new();
        }

        private class ValidatorViolation
        {
            public string? Code { get; set; }
            public string? Message { get; set; }
            public int? Line { get; set; }
        }
    }
}
