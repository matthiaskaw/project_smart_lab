using SmartLab.Domains.Analysis.Models;

namespace SmartLab.Domains.Analysis.Interfaces
{
    /// <summary>
    /// Main service for orchestrating analysis script execution.
    /// </summary>
    public interface IAnalysisService
    {
        /// <summary>
        /// Execute an analysis script on a dataset.
        /// </summary>
        Task<AnalysisResult> ExecuteAnalysisAsync(
            Guid datasetId,
            Guid scriptId,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get analysis execution history for a dataset.
        /// </summary>
        Task<List<AnalysisResult>> GetAnalysisHistoryAsync(
            Guid datasetId,
            int page = 1,
            int pageSize = 10);

        /// <summary>
        /// Get a specific analysis result.
        /// </summary>
        Task<AnalysisResult?> GetAnalysisResultAsync(Guid resultId);

        /// <summary>
        /// Delete an analysis result and its associated files.
        /// </summary>
        Task<bool> DeleteAnalysisResultAsync(Guid resultId);

        /// <summary>
        /// Get available scripts for the current user.
        /// </summary>
        Task<List<AnalysisScriptMetadata>> GetAvailableScriptsAsync(string userId);
    }
}
