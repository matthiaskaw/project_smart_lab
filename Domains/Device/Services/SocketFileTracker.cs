using SmartLab.Domains.Device.Interfaces;
using Microsoft.Extensions.Logging;

namespace SmartLab.Domains.Device.Services
{
    /// <summary>
    /// Tracks active socket files to enable cleanup after crashes.
    /// Maintains a persistent tracking file that survives application restarts.
    /// </summary>
    public class SocketFileTracker : ISocketFileTracker
    {
        private readonly string _trackingFilePath;
        private readonly ILogger<SocketFileTracker> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public SocketFileTracker(ILogger<SocketFileTracker> logger)
        {
            _logger = logger;
            // Store tracking file in temp directory with app-specific name
            _trackingFilePath = Path.Combine(Path.GetTempPath(), "smartlab_active_sockets.txt");
            _logger.LogInformation("SocketFileTracker initialized. Tracking file: {TrackingFile}", _trackingFilePath);
        }

        public async Task RegisterSocketFileAsync(string socketPath)
        {
            if (string.IsNullOrEmpty(socketPath))
            {
                _logger.LogWarning("Attempted to register null or empty socket path");
                return;
            }

            await _lock.WaitAsync();
            try
            {
                // Append the socket path to the tracking file
                await File.AppendAllLinesAsync(_trackingFilePath, new[] { socketPath });
                _logger.LogDebug("Registered socket file: {SocketPath}", socketPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register socket file: {SocketPath}", socketPath);
                // Don't throw - registration failure shouldn't break the app
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task UnregisterSocketFileAsync(string socketPath)
        {
            if (string.IsNullOrEmpty(socketPath))
            {
                return;
            }

            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(_trackingFilePath))
                {
                    _logger.LogDebug("Tracking file does not exist, nothing to unregister");
                    return;
                }

                // Read all lines, filter out the one to remove, write back
                var lines = await File.ReadAllLinesAsync(_trackingFilePath);
                var updatedLines = lines.Where(l => l.Trim() != socketPath.Trim()).ToArray();

                if (updatedLines.Length > 0)
                {
                    await File.WriteAllLinesAsync(_trackingFilePath, updatedLines);
                }
                else
                {
                    // If no lines left, delete the tracking file
                    File.Delete(_trackingFilePath);
                }

                _logger.LogDebug("Unregistered socket file: {SocketPath}", socketPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unregister socket file: {SocketPath}", socketPath);
                // Don't throw - unregistration failure is not critical
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task CleanupStaleSocketFilesAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(_trackingFilePath))
                {
                    _logger.LogInformation("No tracking file found - no stale socket files to clean up");
                    return;
                }

                _logger.LogInformation("Starting cleanup of stale socket files");

                var lines = await File.ReadAllLinesAsync(_trackingFilePath);
                var cleanedCount = 0;
                var errorCount = 0;

                foreach (var socketPath in lines)
                {
                    if (string.IsNullOrWhiteSpace(socketPath))
                        continue;

                    try
                    {
                        if (File.Exists(socketPath))
                        {
                            File.Delete(socketPath);
                            cleanedCount++;
                            _logger.LogInformation("Cleaned up stale socket file: {SocketPath}", socketPath);
                        }
                        else
                        {
                            _logger.LogDebug("Socket file already removed: {SocketPath}", socketPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogWarning(ex, "Failed to clean up stale socket file: {SocketPath}", socketPath);
                        // Continue with other files even if one fails
                    }
                }

                // Delete the tracking file after cleanup
                try
                {
                    File.Delete(_trackingFilePath);
                    _logger.LogInformation("Stale socket cleanup complete. Cleaned: {CleanedCount}, Errors: {ErrorCount}",
                        cleanedCount, errorCount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete tracking file after cleanup");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during stale socket file cleanup");
                // Don't throw - startup should continue even if cleanup fails
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
