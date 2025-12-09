using SmartLab.Domains.Analysis.Models;

namespace SmartLab.Domains.Analysis.Interfaces
{
    /// <summary>
    /// Service for managing analysis scripts (CRUD operations).
    /// </summary>
    public interface IScriptManagementService
    {
        /// <summary>
        /// Get scripts uploaded by a specific user.
        /// </summary>
        Task<List<AnalysisScriptMetadata>> GetUserScriptsAsync(string userId);


        /// <summary>
        /// Get metadata for a specific script.
        /// </summary>
        Task<AnalysisScriptMetadata?> GetScriptMetadataAsync(Guid scriptId);

        /// <summary>
        /// Upload a new script.
        /// </summary>
        Task<ScriptUploadResult> UploadScriptAsync(
            string userId,
            string scriptContent,
            AnalysisScriptMetadata metadata);

        /// <summary>
        /// Update script metadata.
        /// </summary>
        Task<bool> UpdateScriptMetadataAsync(Guid scriptId, AnalysisScriptMetadata metadata);

        /// <summary>
        /// Delete a user script.
        /// </summary>
        Task<bool> DeleteUserScriptAsync(string userId, Guid scriptId);

        /// <summary>
        /// Get the file path for a script.
        /// </summary>
        Task<string?> GetScriptPathAsync(Guid scriptId);

        /// <summary>
        /// Get the content of a script.
        /// </summary>
        Task<string?> GetScriptContentAsync(Guid scriptId);
    }
}
