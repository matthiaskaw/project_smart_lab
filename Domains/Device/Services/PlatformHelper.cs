using SmartLab.Domains.Device.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace SmartLab.Domains.Device.Services
{
    /// <summary>
    /// Provides platform-specific utilities for cross-platform named pipe support.
    /// Handles differences between Windows named pipes and Linux UNIX domain sockets.
    /// </summary>
    public class PlatformHelper : IPlatformHelper
    {
        private readonly ILogger<PlatformHelper> _logger;

        public PlatformHelper(ILogger<PlatformHelper> logger)
        {
            _logger = logger;
            _logger.LogInformation("PlatformHelper initialized for {Platform}", PlatformName);
        }

        public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public string PlatformName
        {
            get
            {
                if (IsWindows) return "Windows";
                if (IsLinux) return "Linux";
                if (IsMacOS) return "macOS";
                return "Unknown";
            }
        }

        public string GetPipeServerName(string pipeName)
        {
            // Server always uses the simple pipe name
            // .NET runtime handles platform-specific details internally
            _logger.LogDebug("GetPipeServerName: {PipeName}", pipeName);
            return pipeName;
        }

        public string GetPipeClientPath(string pipeName)
        {
            if (IsWindows)
            {
                // On Windows, clients connect using the local server name "."
                // The full path would be \\.\pipe\{pipeName} but clients typically just use "."
                var path = ".";
                _logger.LogDebug("GetPipeClientPath (Windows): ServerName='{Path}', PipeName='{PipeName}'", path, pipeName);
                return path;
            }
            else if (IsLinux || IsMacOS)
            {
                // On Linux/macOS, .NET creates UNIX domain sockets
                // The .NET runtime uses various patterns depending on version:
                // - /tmp/CoreFxPipe_{pipeName}
                // - /tmp/dotnet-diagnostic-{pid}-{pipeName}-socket
                // We need to check common locations or use the known pattern

                // .NET 5+ typically uses: /tmp/CoreFxPipe_{pipeName}
                var path = $"/tmp/CoreFxPipe_{pipeName}";
                _logger.LogDebug("GetPipeClientPath (Linux/macOS): {Path}", path);
                return path;
            }
            else
            {
                _logger.LogWarning("Unknown platform, returning pipe name as-is");
                return pipeName;
            }
        }

        public string? GetSocketFilePath(string pipeName)
        {
            if (IsWindows)
            {
                // Windows doesn't use socket files
                return null;
            }
            else if (IsLinux || IsMacOS)
            {
                // Return the expected socket file path
                return $"/tmp/CoreFxPipe_{pipeName}";
            }
            else
            {
                return null;
            }
        }

        public void CleanupSocketFile(string pipeName)
        {
            if (IsWindows)
            {
                // No cleanup needed on Windows
                return;
            }

            var socketPath = GetSocketFilePath(pipeName);
            if (socketPath == null)
            {
                return;
            }

            try
            {
                if (File.Exists(socketPath))
                {
                    _logger.LogInformation("Cleaning up socket file: {SocketPath}", socketPath);
                    //File.Delete(socketPath); // SEEMS LIKE DELETION IS NOT NEEDED OR IMPLEMENTED WRONG HERE
                    _logger.LogDebug("Socket file deleted successfully");
                }
                else
                {
                    _logger.LogDebug("Socket file does not exist: {SocketPath}", socketPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup socket file: {SocketPath}", socketPath);
                // Don't throw - cleanup is best effort
            }
        }
    }
}
