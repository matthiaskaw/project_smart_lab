using SmartLab.Domains.Analysis.Models;

namespace SmartLab.Domains.Analysis.Interfaces
{
    /// <summary>
    /// Service for validating analysis scripts for security and correctness.
    /// </summary>
    public interface IScriptValidationService
    {
        /// <summary>
        /// Validate a script comprehensively (syntax, security, structure).
        /// </summary>
        Task<ValidationResult> ValidateScriptAsync(
            string scriptContent,
            ScriptLanguage language);

        /// <summary>
        /// Validate script syntax only.
        /// </summary>
        Task<ValidationResult> ValidateSyntaxAsync(
            string scriptContent,
            ScriptLanguage language);

        /// <summary>
        /// Validate script security (dangerous patterns, AST analysis).
        /// </summary>
        Task<ValidationResult> ValidateSecurityAsync(
            string scriptContent,
            ScriptLanguage language);

        /// <summary>
        /// Validate script structure (required imports, I/O patterns).
        /// </summary>
        ValidationResult ValidateStructure(
            string scriptContent,
            ScriptLanguage language);

        /// <summary>
        /// Validate user-provided parameters against script schema.
        /// </summary>
        ParameterValidationResult ValidateParameters(
            Dictionary<string, object> providedParameters,
            List<ScriptParameter> schemaParameters);
    }
}
