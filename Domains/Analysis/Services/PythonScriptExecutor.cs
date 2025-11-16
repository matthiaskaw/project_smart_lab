using SmartLab.Domains.Analysis.Interfaces;
using SmartLab.Domains.Analysis.Models;
using System.Diagnostics;
using System.Text;

namespace SmartLab.Domains.Analysis.Services
{
    /// <summary>
    /// Executor for Python analysis scripts with process isolation.
    /// </summary>
    public class PythonScriptExecutor : IScriptExecutor
    {
        private readonly IPlatformHelper _platformHelper;
        private readonly ILogger<PythonScriptExecutor> _logger;

        public ScriptLanguage SupportedLanguage => ScriptLanguage.Python;

        public PythonScriptExecutor(
            IPlatformHelper platformHelper,
            ILogger<PythonScriptExecutor> logger)
        {
            _platformHelper = platformHelper;
            _logger = logger;
        }

        public async Task<ProcessResult> ExecuteScriptAsync(
            string scriptPath,
            string inputJson,
            string outputDirectory,
            Dictionary<string, object> parameters,
            int timeoutSeconds = 300,
            CancellationToken cancellationToken = default)
        {
            var startTime = Stopwatch.StartNew();
            var result = new ProcessResult();

            try
            {
                if (!File.Exists(scriptPath))
                {
                    return new ProcessResult
                    {
                        Success = false,
                        ExitCode = -1,
                        Error = $"Script file not found: {scriptPath}"
                    };
                }

                // Ensure output directory exists
                Directory.CreateDirectory(outputDirectory);

                // Create process start info
                var psi = new ProcessStartInfo
                {
                    FileName = _platformHelper.GetPythonCommand(),
                    Arguments = $"\"{scriptPath}\" \"{outputDirectory}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = outputDirectory
                };

                // Set environment variables
                var envVars = _platformHelper.GetScriptEnvironmentVariables("python");
                foreach (var (key, value) in envVars)
                {
                    psi.Environment[key] = value;
                }

                _logger.LogInformation(
                    "Starting Python script execution: {ScriptPath} with timeout {Timeout}s",
                    scriptPath, timeoutSeconds);

                // Start process
                using var process = new Process { StartInfo = psi };
                process.Start();

                // Write input JSON to stdin
                await process.StandardInput.WriteAsync(inputJson);
                process.StandardInput.Close();

                // Setup cancellation
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                try
                {
                    // Read output asynchronously
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    // Wait for process to exit with timeout
                    await process.WaitForExitAsync(linkedCts.Token);

                    result.Output = await outputTask;
                    result.Error = await errorTask;
                    result.ExitCode = process.ExitCode;
                    result.Success = process.ExitCode == 0;
                }
                catch (OperationCanceledException)
                {
                    // Timeout or cancellation occurred
                    _logger.LogWarning("Script execution timeout or cancelled: {ScriptPath}", scriptPath);

                    try
                    {
                        // Kill the process and all child processes
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception killEx)
                    {
                        _logger.LogError(killEx, "Error killing process after timeout");
                    }

                    result.Success = false;
                    result.ExitCode = -1;
                    result.Error = cancellationToken.IsCancellationRequested
                        ? "Script execution was cancelled"
                        : $"Script execution timeout (exceeded {timeoutSeconds} seconds)";
                }

                result.ExecutionTimeMs = (int)startTime.ElapsedMilliseconds;

                _logger.LogInformation(
                    "Script execution completed: ExitCode={ExitCode}, Duration={Duration}ms",
                    result.ExitCode, result.ExecutionTimeMs);

                // Log stdout output (for debugging)
                if (!string.IsNullOrWhiteSpace(result.Output))
                {
                    _logger.LogInformation("Script stdout output: {Output}", result.Output);
                }

                // Log errors if any
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    _logger.LogWarning("Script stderr output: {Error}", result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Python script: {ScriptPath}", scriptPath);
                result.Success = false;
                result.ExitCode = -1;
                result.Error = $"Execution error: {ex.Message}";
                result.ExecutionTimeMs = (int)startTime.ElapsedMilliseconds;
            }

            return result;
        }

        public bool IsInterpreterAvailable()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _platformHelper.GetPythonCommand(),
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return false;
                }

                process.WaitForExit(5000);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
