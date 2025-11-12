namespace SmartLab.Domains.Analysis.Interfaces
{
    /// <summary>
    /// Platform-specific helper for script execution.
    /// </summary>
    public interface IPlatformHelper
    {
        /// <summary>
        /// Check if running on Windows.
        /// </summary>
        bool IsWindows();

        /// <summary>
        /// Check if running on Linux.
        /// </summary>
        bool IsLinux();

        /// <summary>
        /// Check if running on macOS.
        /// </summary>
        bool IsMacOS();

        /// <summary>
        /// Get the Python command for the current platform.
        /// </summary>
        string GetPythonCommand();

        /// <summary>
        /// Get the R command for the current platform.
        /// </summary>
        string GetRCommand();

        /// <summary>
        /// Get environment variables needed for script execution.
        /// </summary>
        Dictionary<string, string> GetScriptEnvironmentVariables(string scriptLanguage);
    }
}
