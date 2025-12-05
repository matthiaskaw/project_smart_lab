using SmartLab.Domains.Analysis.Interfaces;
using SmartLab.Domains.Analysis.Models;
using SmartLab.Domains.Analysis.Database;
using SmartLab.Domains.Data.Database;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Diagnostics;

namespace SmartLab.Domains.Analysis.Services
{
    /// <summary>
    /// Main orchestration service for analysis execution.
    /// Coordinates between data loading, script execution, and result storage.
    /// </summary>
    public class AnalysisService : IAnalysisService
    {
        private readonly SmartLabDbContext _dbContext;
        private readonly IDataService _dataService;
        private readonly IScriptManagementService _scriptManagementService;
        private readonly IScriptValidationService _validationService;
        private readonly IScriptExecutor _pythonExecutor;
        private readonly ILogger<AnalysisService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _outputDirectory;

        public AnalysisService(
            SmartLabDbContext dbContext,
            IDataService dataService,
            IScriptManagementService scriptManagementService,
            IScriptValidationService validationService,
            IScriptExecutor pythonExecutor,
            ILogger<AnalysisService> logger,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _dataService = dataService;
            _scriptManagementService = scriptManagementService;
            _validationService = validationService;
            _pythonExecutor = pythonExecutor;
            _logger = logger;
            _configuration = configuration;

            var configOutputDir = configuration["Analysis:OutputDirectory"] ?? "wwwroot/analysis-results";
            // Ensure output directory is rooted in the app directory for security
            var appDirectory = Directory.GetCurrentDirectory();

            // Strip any root from the config path to ensure it stays within app directory
            if (Path.IsPathRooted(configOutputDir))
            {
                // Remove the root (e.g., C:\ or /) to make it relative
                configOutputDir = configOutputDir.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (configOutputDir.Length > 1 && configOutputDir[1] == ':')
                {
                    // Windows absolute path like C:\path - skip drive letter and colon
                    configOutputDir = configOutputDir.Substring(2).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
            }

            _outputDirectory = Path.GetFullPath(Path.Combine(appDirectory, configOutputDir));
            Directory.CreateDirectory(_outputDirectory);
        }

        public async Task<AnalysisResult> ExecuteAnalysisAsync(
            Guid datasetId,
            Guid scriptId,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new AnalysisResult
            {
                Id = Guid.NewGuid(),
                DatasetId = datasetId,
                ScriptId = scriptId,
                ExecutionDate = DateTime.UtcNow,
                Parameters = parameters
            };

            try
            {
                _logger.LogInformation(
                    "Starting analysis execution: Dataset={DatasetId}, Script={ScriptId}",
                    datasetId, scriptId);

                // Step 1: Load script metadata
                var scriptMetadata = await _scriptManagementService.GetScriptMetadataAsync(scriptId);
                if (scriptMetadata == null)
                {
                    throw new InvalidOperationException($"Script not found: {scriptId}");
                }

                result.ScriptName = scriptMetadata.DisplayName;
                result.ScriptLanguage = scriptMetadata.Language;
                result.ScriptVersion = scriptMetadata.Version;

                // Step 2: Validate parameters
                var paramValidation = _validationService.ValidateParameters(
                    parameters,
                    scriptMetadata.Parameters);

                if (!paramValidation.IsValid)
                {
                    result.Status = AnalysisStatus.Failed;
                    result.ErrorMessage = string.Join("; ", paramValidation.Errors);
                    result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
                    await SaveResultAsync(result);
                    return result;
                }

                // Step 3: Load dataset from database
                var dataset = await _dataService.GetDatasetAsync(datasetId);
                if (dataset == null)
                {
                    throw new InvalidOperationException($"Dataset not found: {datasetId}");
                }

                // Step 4: Transform dataset to JSON format for script
                var inputJson = await PrepareScriptInputAsync(dataset, parameters);

                // Step 5: Get script file path
                var scriptPath = await _scriptManagementService.GetScriptPathAsync(scriptId);
                if (scriptPath == null || !File.Exists(scriptPath))
                {
                    throw new InvalidOperationException($"Script file not found: {scriptId}");
                }

                // Step 6: Create output directory for this execution
                var executionOutputDir = CreateOutputDirectory(datasetId, result.Id);

                // Step 7: Execute script
                _logger.LogInformation("Executing script: {ScriptPath}", scriptPath);
                result.Status = AnalysisStatus.Running;

                var executionResult = await _pythonExecutor.ExecuteScriptAsync(
                    scriptPath,
                    inputJson,
                    executionOutputDir,
                    parameters,
                    timeoutSeconds: 300,
                    cancellationToken: cancellationToken);

                result.ExecutionTimeMs = executionResult.ExecutionTimeMs;

                // Step 8: Process execution result
                if (executionResult.Success)
                {
                    await ProcessSuccessfulExecutionAsync(result, executionResult, executionOutputDir);
                }
                else
                {
                    result.Status = AnalysisStatus.Failed;
                    result.ErrorMessage = executionResult.Error;
                    _logger.LogWarning(
                        "Script execution failed: {ScriptId}, Error: {Error}",
                        scriptId, executionResult.Error);
                }

                // Step 9: Update execution count
                await UpdateScriptExecutionCountAsync(scriptId);
            }
            catch (OperationCanceledException)
            {
                result.Status = AnalysisStatus.Cancelled;
                result.ErrorMessage = "Analysis was cancelled";
                result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
                _logger.LogWarning("Analysis cancelled: {ResultId}", result.Id);
            }
            catch (Exception ex)
            {
                result.Status = AnalysisStatus.Failed;
                result.ErrorMessage = $"Error: {ex.Message}";
                result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
                _logger.LogError(ex, "Error executing analysis: Dataset={DatasetId}, Script={ScriptId}",
                    datasetId, scriptId);
            }
            finally
            {
                // Save result to database
                await SaveResultAsync(result);
            }

            return result;
        }

        public async Task<List<AnalysisResult>> GetAnalysisHistoryAsync(
            Guid datasetId,
            int page = 1,
            int pageSize = 10)
        {
            try
            {
                var entities = await _dbContext.AnalysisResults
                    .Where(r => r.DatasetId == datasetId)
                    .OrderByDescending(r => r.ExecutionDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return entities.Select(e => MapEntityToResult(e)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving analysis history for dataset {DatasetId}", datasetId);
                return new List<AnalysisResult>();
            }
        }

        public async Task<AnalysisResult?> GetAnalysisResultAsync(Guid resultId)
        {
            try
            {
                var entity = await _dbContext.AnalysisResults.FindAsync(resultId);
                return entity != null ? MapEntityToResult(entity) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving analysis result {ResultId}", resultId);
                return null;
            }
        }

        public async Task<bool> DeleteAnalysisResultAsync(Guid resultId)
        {
            try
            {
                var entity = await _dbContext.AnalysisResults.FindAsync(resultId);
                if (entity == null)
                {
                    return false;
                }

                // Delete image file if exists
                if (!string.IsNullOrEmpty(entity.ResultImagePath))
                {
                    var fullPath = Path.Combine(_outputDirectory, entity.ResultImagePath);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }

                _dbContext.AnalysisResults.Remove(entity);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Analysis result deleted: {ResultId}", resultId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting analysis result {ResultId}", resultId);
                return false;
            }
        }

        public async Task<List<AnalysisScriptMetadata>> GetAvailableScriptsAsync(string userId)
        {
            var builtInScripts = await _scriptManagementService.GetBuiltInScriptsAsync();
            var userScripts = await _scriptManagementService.GetUserScriptsAsync(userId);
            var sharedScripts = await _scriptManagementService.GetSharedScriptsAsync();

            return builtInScripts
                .Concat(userScripts)
                .Concat(sharedScripts)
                .ToList();
        }

        private async Task<string> PrepareScriptInputAsync(
            DatasetEntity dataset,
            Dictionary<string, object> parameters)
        {
            // Parse data points directly from RawDataJson
            var dataPoints = ParseRawDataToDataPoints(dataset.RawDataJson);

            // Parse measurement parameters if available
            Dictionary<string, object>? measurementParameters = null;
            if (!string.IsNullOrEmpty(dataset.ParametersJson))
            {
                try
                {
                    measurementParameters = JsonSerializer.Deserialize<Dictionary<string, object>>(dataset.ParametersJson);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse measurement parameters for dataset {DatasetId}", dataset.Id);
                }
            }

            var inputData = new
            {
                datasetId = dataset.Id.ToString(),
                datasetName = dataset.Name ?? "Unknown",
                createdDate = dataset.CreatedDate,
                dataSource = dataset.DataSource.ToString(),
                parameters = parameters, // Script execution parameters
                measurementParameters = measurementParameters, // Original measurement parameters
                dataPoints = dataPoints
            };

            return await Task.FromResult(JsonSerializer.Serialize(inputData, new JsonSerializerOptions
            {
                WriteIndented = false
            }));
        }

        private List<object> ParseRawDataToDataPoints(string? rawDataJson)
        {
            var result = new List<object>();

            if (string.IsNullOrEmpty(rawDataJson))
            {
                return result;
            }

            try
            {
                // Just pass the raw lines - scripts do their own parsing
                var lines = JsonSerializer.Deserialize<List<string>>(rawDataJson);
                if (lines == null || lines.Count == 0)
                {
                    return result;
                }

                // Return raw lines as strings - scripts handle parsing
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        result.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse RawDataJson");
            }

            return result;
        }

        private string CreateOutputDirectory(Guid datasetId, Guid resultId)
        {
            var now = DateTime.UtcNow;

            // _outputDirectory is already absolute (ensured in constructor)
            var subDir = Path.Combine(
                _outputDirectory,
                now.Year.ToString(),
                now.Month.ToString("00"),
                now.Day.ToString("00")
            );

            Directory.CreateDirectory(subDir);
            return Path.GetFullPath(subDir); // Normalize the path
        }

        private async Task ProcessSuccessfulExecutionAsync(
            AnalysisResult result,
            ProcessResult executionResult,
            string outputDirectory)
        {
            try
            {
                // Extract JSON from script output
                // The JSON result should be the last line that starts with '{'
                var lines = executionResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var jsonLine = lines.LastOrDefault(line => line.TrimStart().StartsWith('{'));

                if (string.IsNullOrWhiteSpace(jsonLine))
                {
                    throw new InvalidOperationException("No JSON output found in script result");
                }

                // Parse script output as JSON
                var outputJson = JsonDocument.Parse(jsonLine);
                var root = outputJson.RootElement;

                if (root.TryGetProperty("status", out var status) &&
                    status.GetString() == "success")
                {
                    result.Status = AnalysisStatus.Success;

                    // Get image path
                    if (root.TryGetProperty("imagePath", out var imagePath))
                    {
                        var imageFileName = imagePath.GetString();
                        if (!string.IsNullOrEmpty(imageFileName))
                        {
                            // Store relative path
                            var relativePath = Path.GetRelativePath(_outputDirectory,
                                Path.Combine(outputDirectory, imageFileName));
                            result.ResultImagePath = relativePath.Replace('\\', '/');
                        }
                    }

                    // Get statistics
                    if (root.TryGetProperty("statistics", out var stats))
                    {
                        result.Statistics = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            stats.GetRawText());
                    }
                }
                else if (root.TryGetProperty("status", out var errorStatus) &&
                         errorStatus.GetString() == "error")
                {
                    result.Status = AnalysisStatus.Failed;
                    if (root.TryGetProperty("errorMessage", out var errorMsg))
                    {
                        result.ErrorMessage = errorMsg.GetString();
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse script output as JSON");
                result.Status = AnalysisStatus.Failed;
                result.ErrorMessage = "Script output is not valid JSON";
            }
        }

        private async Task SaveResultAsync(AnalysisResult result)
        {
            try
            {
                var entity = new AnalysisResultEntity
                {
                    Id = result.Id,
                    DatasetId = result.DatasetId,
                    ScriptId = result.ScriptId,
                    ScriptName = result.ScriptName,
                    ScriptLanguage = result.ScriptLanguage.ToString(),
                    ScriptVersion = result.ScriptVersion,
                    ExecutionDate = result.ExecutionDate,
                    Status = result.Status.ToString(),
                    ParametersJson = JsonSerializer.Serialize(result.Parameters),
                    ResultImagePath = result.ResultImagePath,
                    ResultDataJson = result.Statistics != null
                        ? JsonSerializer.Serialize(result.Statistics)
                        : null,
                    ErrorMessage = result.ErrorMessage,
                    ExecutionTimeMs = result.ExecutionTimeMs
                };

                _dbContext.AnalysisResults.Add(entity);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving analysis result: {ResultId}", result.Id);
            }
        }

        private async Task UpdateScriptExecutionCountAsync(Guid scriptId)
        {
            try
            {
                var script = await _dbContext.ScriptMetadata.FindAsync(scriptId);
                if (script != null)
                {
                    script.ExecutionCount++;
                    script.LastExecuted = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating script execution count for {ScriptId}", scriptId);
            }
        }

        private AnalysisResult MapEntityToResult(AnalysisResultEntity entity)
        {
            return new AnalysisResult
            {
                Id = entity.Id,
                DatasetId = entity.DatasetId,
                ScriptId = entity.ScriptId,
                ScriptName = entity.ScriptName,
                ScriptLanguage = Enum.Parse<ScriptLanguage>(entity.ScriptLanguage),
                ScriptVersion = entity.ScriptVersion ?? string.Empty,
                ExecutionDate = entity.ExecutionDate,
                Status = Enum.Parse<AnalysisStatus>(entity.Status),
                Parameters = DeserializeJson<Dictionary<string, object>>(entity.ParametersJson)
                    ?? new Dictionary<string, object>(),
                ResultImagePath = entity.ResultImagePath,
                Statistics = DeserializeJson<Dictionary<string, object>>(entity.ResultDataJson),
                ErrorMessage = entity.ErrorMessage,
                ExecutionTimeMs = entity.ExecutionTimeMs
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
