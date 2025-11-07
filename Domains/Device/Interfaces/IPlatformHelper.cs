namespace SmartLab.Domains.Device.Interfaces
{
    /// <summary>
    /// Provides platform-specific utilities for cross-platform named pipe support.
    /// Handles differences between Windows named pipes and Linux UNIX domain sockets.
    /// </summary>
    public interface IPlatformHelper
    {
        /// <summary>
        /// Gets whether the current platform is Windows.
        /// </summary>
        bool IsWindows { get; }

        /// <summary>
        /// Gets whether the current platform is Linux.
        /// </summary>
        bool IsLinux { get; }

        /// <summary>
        /// Gets whether the current platform is macOS.
        /// </summary>
        bool IsMacOS { get; }

        /// <summary>
        /// Gets the platform name for logging purposes.
        /// </summary>
        string PlatformName { get; }

        /// <summary>
        /// Gets the path that the server uses to create a named pipe.
        /// On Windows: returns the pipe name without path prefix.
        /// On Linux: returns the pipe name (actual path handled by .NET runtime).
        /// </summary>
        /// <param name="pipeName">The logical name of the pipe</param>
        /// <returns>The server pipe name</returns>
        string GetPipeServerName(string pipeName);

        /// <summary>
        /// Gets the full path that a client should use to connect to a named pipe.
        /// On Windows: returns "\\.\pipe\{pipeName}" or "." for local connections.
        /// On Linux: returns the UNIX domain socket path (e.g., "/tmp/CoreFxPipe_{pipeName}").
        /// </summary>
        /// <param name="pipeName">The logical name of the pipe</param>
        /// <returns>The full path for client connections</returns>
        string GetPipeClientPath(string pipeName);

        /// <summary>
        /// Gets the socket file path for cleanup purposes (Linux only).
        /// Returns null on Windows.
        /// </summary>
        /// <param name="pipeName">The logical name of the pipe</param>
        /// <returns>The socket file path or null if not applicable</returns>
        string? GetSocketFilePath(string pipeName);

        /// <summary>
        /// Cleans up socket files if they exist (Linux only).
        /// Safe to call on Windows (no-op).
        /// </summary>
        /// <param name="pipeName">The logical name of the pipe</param>
        void CleanupSocketFile(string pipeName);
    }
}
