namespace SmartLab.Domains.Device.Interfaces
{
    /// <summary>
    /// Tracks active socket files to enable cleanup after crashes.
    /// On Linux, named pipes create socket files in /tmp that persist after crashes.
    /// This service maintains a tracking file to clean up stale sockets on startup.
    /// </summary>
    public interface ISocketFileTracker
    {
        /// <summary>
        /// Registers a socket file path to the tracking file.
        /// Called immediately after creating a named pipe.
        /// </summary>
        /// <param name="socketPath">Full path to the socket file (e.g., /tmp/CoreFxPipe_serverToClient_guid)</param>
        Task RegisterSocketFileAsync(string socketPath);

        /// <summary>
        /// Removes a socket file path from the tracking file.
        /// Called when the socket is properly disposed.
        /// </summary>
        /// <param name="socketPath">Full path to the socket file</param>
        Task UnregisterSocketFileAsync(string socketPath);

        /// <summary>
        /// Cleans up all stale socket files listed in the tracking file.
        /// Should be called once during application startup.
        /// Deletes the socket files and clears the tracking file.
        /// </summary>
        Task CleanupStaleSocketFilesAsync();
    }
}
