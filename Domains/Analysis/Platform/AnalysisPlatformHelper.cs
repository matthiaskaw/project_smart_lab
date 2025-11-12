using SmartLab.Domains.Analysis.Interfaces;
using System.Runtime.InteropServices;

namespace SmartLab.Domains.Analysis.Platform
{
    /// <summary>
    /// Platform-specific helper for analysis script execution.
    /// </summary>
    public class AnalysisPlatformHelper : IPlatformHelper
    {
        public bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public bool IsMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public string GetPythonCommand()
        {
            if (IsWindows())
            {
                return "python";  // Windows typically uses 'python'
            }
            else
            {
                return "python3";  // Linux/macOS use 'python3'
            }
        }

        public string GetRCommand()
        {
            return "Rscript";  // Same across platforms
        }

        public Dictionary<string, string> GetScriptEnvironmentVariables(string scriptLanguage)
        {
            var envVars = new Dictionary<string, string>
            {
                ["PYTHONIOENCODING"] = "utf-8",
                ["PYTHONUNBUFFERED"] = "1"
            };

            // On Linux, matplotlib needs non-GUI backend
            if (IsLinux() && scriptLanguage.Equals("python", StringComparison.OrdinalIgnoreCase))
            {
                envVars["MPLBACKEND"] = "Agg";
            }

            return envVars;
        }
    }
}
