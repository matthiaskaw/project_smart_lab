using SmartLab.Domains.Analysis.Models;

namespace SmartLab.Domains.Analysis.Interfaces
{
    /// <summary>
    /// Interface for executing scripts in different languages.
    /// </summary>
    public interface IScriptExecutor
    {
        /// <summary>
        /// The language this executor supports.
        /// </summary>
        ScriptLanguage SupportedLanguage { get; }

        /// <summary>
        /// Execute a script with provided input data.
        /// </summary>
        /// <param name="scriptPath">Full path to the script file</param>
        /// <param name="inputJson">JSON data to pass via stdin</param>
        /// <param name="outputDirectory">Directory where script should save output files</param>
        /// <param name="parameters">User-provided parameters</param>
        /// <param name="timeoutSeconds">Maximum execution time in seconds</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<ProcessResult> ExecuteScriptAsync(
            string scriptPath,
            string inputJson,
            string outputDirectory,
            Dictionary<string, object> parameters,
            int timeoutSeconds = 300,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if the required interpreter is available on the system.
        /// </summary>
        bool IsInterpreterAvailable();
    }
}
