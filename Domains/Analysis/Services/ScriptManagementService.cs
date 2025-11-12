using SmartLab.Domains.Analysis.Interfaces;
using SmartLab.Domains.Analysis.Models;
using SmartLab.Domains.Analysis.Database;
using SmartLab.Domains.Data.Database;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace SmartLab.Domains.Analysis.Services
{
    /// <summary>
    /// Service for managing analysis scripts (CRUD operations).
    /// Stores files on disk and metadata in database.
    /// </summary>
    public class ScriptManagementService : IScriptManagementService
    {
        private readonly SmartLabDbContext _dbContext;
        private readonly IScriptValidationService _validationService;
        private readonly ILogger<ScriptManagementService> _logger;
        private readonly string _scriptsBaseDirectory;
        private readonly string _builtInScriptsDirectory;
        private readonly string _userScriptsDirectory;

        public ScriptManagementService(
            SmartLabDbContext dbContext,
            IScriptValidationService validationService,
            ILogger<ScriptManagementService> logger,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _validationService = validationService;
            _logger = logger;

            _scriptsBaseDirectory = configuration["Analysis:ScriptsDirectory"] ?? "analysis-scripts";
            _builtInScriptsDirectory = Path.Combine(_scriptsBaseDirectory, "built-in");
            _userScriptsDirectory = Path.Combine(_scriptsBaseDirectory, "user-uploads");

            // Ensure directories exist
            Directory.CreateDirectory(_builtInScriptsDirectory);
            Directory.CreateDirectory(_userScriptsDirectory);
        }

        public async Task<List<AnalysisScriptMetadata>> GetBuiltInScriptsAsync()
        {
            try
            {
                return await _dbContext.ScriptMetadata
                    .Where(s => s.IsBuiltIn)
                    .OrderBy(s => s.DisplayName)
                    .Select(e => MapEntityToMetadata(e))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving built-in scripts");
                return new List<AnalysisScriptMetadata>();
            }
        }

        public async Task<List<AnalysisScriptMetadata>> GetUserScriptsAsync(string userId)
        {
            try
            {
                return await _dbContext.ScriptMetadata
                    .Where(s => s.UserId == userId && !s.IsBuiltIn)
                    .OrderByDescending(s => s.UploadDate)
                    .Select(e => MapEntityToMetadata(e))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user scripts for {UserId}", userId);
                return new List<AnalysisScriptMetadata>();
            }
        }

        public async Task<List<AnalysisScriptMetadata>> GetSharedScriptsAsync()
        {
            try
            {
                return await _dbContext.ScriptMetadata
                    .Where(s => s.IsShared && !s.IsBuiltIn)
                    .OrderBy(s => s.DisplayName)
                    .Select(e => MapEntityToMetadata(e))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving shared scripts");
                return new List<AnalysisScriptMetadata>();
            }
        }

        public async Task<AnalysisScriptMetadata?> GetScriptMetadataAsync(Guid scriptId)
        {
            try
            {
                var entity = await _dbContext.ScriptMetadata.FindAsync(scriptId);
                return entity != null ? MapEntityToMetadata(entity) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving script metadata for {ScriptId}", scriptId);
                return null;
            }
        }

        public async Task<ScriptUploadResult> UploadScriptAsync(
            string userId,
            string scriptContent,
            AnalysisScriptMetadata metadata)
        {
            var result = new ScriptUploadResult { Success = false };

            try
            {
                // Validate the script
                var validationResult = await _validationService.ValidateScriptAsync(
                    scriptContent,
                    metadata.Language);

                result.ValidationStatus = validationResult.Status;
                result.ValidationErrors = validationResult.Errors.Select(e => e.Message).ToList();
                result.ValidationWarnings = validationResult.Warnings.Select(w => w.Message).ToList();

                // Don't save if validation failed
                if (validationResult.Status == ValidationStatus.Failed)
                {
                    result.ErrorMessage = "Script validation failed";
                    return result;
                }

                // Generate script ID
                var scriptId = Guid.NewGuid();

                // Determine file extension
                var extension = metadata.Language switch
                {
                    ScriptLanguage.Python => ".py",
                    ScriptLanguage.R => ".R",
                    _ => ".txt"
                };

                // Create user directory if it doesn't exist
                var userDir = Path.Combine(_userScriptsDirectory, userId);
                Directory.CreateDirectory(userDir);

                // Save script to file
                var fileName = $"{scriptId}{extension}";
                var filePath = Path.Combine(userDir, fileName);
                await File.WriteAllTextAsync(filePath, scriptContent);

                // Create database entity
                var entity = new ScriptMetadataEntity
                {
                    Id = scriptId,
                    UserId = userId,
                    FileName = metadata.FileName,
                    DisplayName = metadata.DisplayName,
                    Description = metadata.Description,
                    Author = metadata.Author,
                    UploadDate = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Version = metadata.Version,
                    Language = metadata.Language.ToString(),
                    TagsJson = JsonSerializer.Serialize(metadata.Tags),
                    ParametersJson = JsonSerializer.Serialize(metadata.Parameters),
                    ValidationStatus = validationResult.Status.ToString(),
                    ValidationErrorsJson = JsonSerializer.Serialize(result.ValidationErrors),
                    ValidationWarningsJson = JsonSerializer.Serialize(result.ValidationWarnings),
                    IsShared = false,
                    IsBuiltIn = false,
                    ExecutionCount = 0,
                    FilePath = filePath
                };

                _dbContext.ScriptMetadata.Add(entity);
                await _dbContext.SaveChangesAsync();

                result.Success = true;
                result.ScriptId = scriptId;

                _logger.LogInformation(
                    "Script uploaded successfully: {ScriptId} by user {UserId}",
                    scriptId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading script for user {UserId}", userId);
                result.ErrorMessage = $"Upload failed: {ex.Message}";
            }

            return result;
        }

        public async Task<bool> UpdateScriptMetadataAsync(
            Guid scriptId,
            AnalysisScriptMetadata metadata)
        {
            try
            {
                var entity = await _dbContext.ScriptMetadata.FindAsync(scriptId);
                if (entity == null)
                {
                    return false;
                }

                // Update allowed fields
                entity.DisplayName = metadata.DisplayName;
                entity.Description = metadata.Description;
                entity.Version = metadata.Version;
                entity.TagsJson = JsonSerializer.Serialize(metadata.Tags);
                entity.ParametersJson = JsonSerializer.Serialize(metadata.Parameters);
                entity.LastModified = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Script metadata updated: {ScriptId}", scriptId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating script metadata for {ScriptId}", scriptId);
                return false;
            }
        }

        public async Task<bool> DeleteUserScriptAsync(string userId, Guid scriptId)
        {
            try
            {
                var entity = await _dbContext.ScriptMetadata
                    .FirstOrDefaultAsync(s => s.Id == scriptId && s.UserId == userId);

                if (entity == null)
                {
                    _logger.LogWarning(
                        "Script not found or access denied: {ScriptId} for user {UserId}",
                        scriptId, userId);
                    return false;
                }

                // Don't allow deletion of built-in scripts
                if (entity.IsBuiltIn)
                {
                    _logger.LogWarning(
                        "Attempted to delete built-in script: {ScriptId}",
                        scriptId);
                    return false;
                }

                // Delete the file
                if (File.Exists(entity.FilePath))
                {
                    File.Delete(entity.FilePath);
                }

                // Delete from database
                _dbContext.ScriptMetadata.Remove(entity);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Script deleted: {ScriptId} by user {UserId}",
                    scriptId, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting script {ScriptId}", scriptId);
                return false;
            }
        }

        public async Task<string?> GetScriptPathAsync(Guid scriptId)
        {
            try
            {
                var entity = await _dbContext.ScriptMetadata.FindAsync(scriptId);
                return entity?.FilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving script path for {ScriptId}", scriptId);
                return null;
            }
        }

        public async Task<string?> GetScriptContentAsync(Guid scriptId)
        {
            try
            {
                var filePath = await GetScriptPathAsync(scriptId);
                if (filePath == null || !File.Exists(filePath))
                {
                    return null;
                }

                return await File.ReadAllTextAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading script content for {ScriptId}", scriptId);
                return null;
            }
        }

        private AnalysisScriptMetadata MapEntityToMetadata(ScriptMetadataEntity entity)
        {
            return new AnalysisScriptMetadata
            {
                Id = entity.Id,
                UserId = entity.UserId,
                FileName = entity.FileName,
                DisplayName = entity.DisplayName,
                Description = entity.Description,
                Author = entity.Author,
                UploadDate = entity.UploadDate,
                LastModified = entity.LastModified,
                Version = entity.Version,
                Language = Enum.Parse<ScriptLanguage>(entity.Language),
                Tags = DeserializeJson<List<string>>(entity.TagsJson) ?? new List<string>(),
                Parameters = DeserializeJson<List<ScriptParameter>>(entity.ParametersJson) ?? new List<ScriptParameter>(),
                ValidationStatus = Enum.Parse<ValidationStatus>(entity.ValidationStatus),
                ValidationErrors = DeserializeJson<List<string>>(entity.ValidationErrorsJson) ?? new List<string>(),
                ValidationWarnings = DeserializeJson<List<string>>(entity.ValidationWarningsJson) ?? new List<string>(),
                IsShared = entity.IsShared,
                IsBuiltIn = entity.IsBuiltIn,
                ExecutionCount = entity.ExecutionCount,
                LastExecuted = entity.LastExecuted,
                FilePath = entity.FilePath
            };
        }

        private T? DeserializeJson<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return default;
            }
        }
    }
}
