using SmartLab.Domains.Device.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
namespace SmartLab.Domains.Device.Services
{
    public class ProxyDeviceProcessManager : IProxyDeviceProcessManager, IDisposable
    {
        private readonly ILogger<ProxyDeviceProcessManager> _logger;
        private Process? _process;
        private bool _disposed;

        public ProxyDeviceProcessManager(ILogger<ProxyDeviceProcessManager> logger)
        {
            _logger = logger;
        }

        public bool IsRunning => _process?.HasExited == false;
        public int? ProcessId => _process?.Id;
        public async Task StartProcessAsync(string executablePath, Guid deviceID, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new ArgumentException("Executable path cannot be null or empty", nameof(executablePath));

            if (!File.Exists(executablePath))
                throw new FileNotFoundException($"Executable not found: {executablePath}");

            if (IsRunning)
            {
                _logger.LogWarning("Process is already running, stopping existing process first");
                await StopProcessAsync(cancellationToken);
            }

            try
            {
                _logger.LogInformation("Starting process: {ExecutablePath}", executablePath);

                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        CreateNoWindow = false,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Normal,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true

                    }
                };

                // Enable process events
                _process.EnableRaisingEvents = true;
                _process.Exited += OnProcessExited;

                // Capture process output for debugging
                _process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        _logger.LogInformation("[ProxyDevice Output] {Data}", args.Data);
                    }
                };
                _process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        _logger.LogError("[ProxyDevice Error] {Data}", args.Data);
                    }
                };

                _process.StartInfo.Arguments = $"{deviceID}";

                if (!_process.Start())
                {
                    throw new InvalidOperationException("Failed to start the process");
                }

                _logger.LogInformation("Process started successfully with ID: {ProcessId}", _process.Id);

                // Start reading output streams immediately
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                // Give the process a brief moment to initialize
                await Task.Delay(500, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start process: {ExecutablePath}", executablePath);
                _process?.Dispose();
                _process = null;
                throw;
            }
        }

        public async Task StopProcessAsync(CancellationToken cancellationToken = default)
        {
            if (_process == null || _process.HasExited)
            {
                _logger.LogInformation("Process is not running or already exited");
                return;
            }

            try
            {
                _logger.LogInformation("Stopping process with ID: {ProcessId}", _process.Id);

                // Try graceful shutdown first
                _process.CloseMainWindow();

                // Wait for graceful shutdown
                if (await WaitForExitAsync(TimeSpan.FromSeconds(5), cancellationToken))
                {
                    _logger.LogInformation("Process exited gracefully");
                    return;
                }

                // Force kill if graceful shutdown failed
                _logger.LogWarning("Process did not exit gracefully, forcing termination");
                _process.Kill();

                await WaitForExitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                _logger.LogInformation("Process terminated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping process");
                throw;
            }
            finally
            {
                _process?.Dispose();
                _process = null;
            }
        }

        private async Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (_process == null) return true;

            var timeoutTask = Task.Delay(timeout, cancellationToken);
            var exitTask = Task.Run(() =>
            {
                try
                {
                    _process.WaitForExit();
                    return true;
                }
                catch
                {
                    return false;
                }
            }, cancellationToken);

            var completedTask = await Task.WhenAny(timeoutTask, exitTask);
            return completedTask == exitTask && await exitTask;
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            if (_process != null)
            {
                _logger.LogInformation("Process exited with code: {ExitCode}", _process.ExitCode);
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _logger?.LogInformation("Disposing process manager");

            try
            {
                if (IsRunning)
                {
                    await StopProcessAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error during process disposal");
            }

            _process?.Dispose();
        }
    }
}