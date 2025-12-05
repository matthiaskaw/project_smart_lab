using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Data.Models;
using SmartLab.Domains.Analysis.Interfaces;
using SmartLab.Domains.Analysis.Models;
using System.Text.Json;

namespace SmartLab.Pages.Data
{
    public class ViewDatasetModel : PageModel
    {
        private readonly IDataService _dataService;
        private readonly IAnalysisService _analysisService;
        private readonly ILogger<ViewDatasetModel> _logger;

        [BindProperty(SupportsGet = true)]
        public Guid Id { get; set; }

        [BindProperty]
        public Guid DatasetId { get; set; }

        public DatasetEntity Dataset { get; set; } = null!;
        public List<DataPointEntity> DataPoints { get; set; } = new();
        public List<AnalysisScriptMetadata> AvailableScripts { get; set; } = new();
        public List<AnalysisResult> AnalysisResults { get; set; } = new();
        public Dictionary<string, object>? MeasurementParameters { get; set; }

        public ViewDatasetModel(
            IDataService dataService,
            IAnalysisService analysisService,
            ILogger<ViewDatasetModel> logger)
        {
            _dataService = dataService;
            _analysisService = analysisService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Load dataset
                var dataset = await _dataService.GetDatasetAsync(Id);
                if (dataset == null)
                {
                    _logger.LogWarning("Dataset not found: {DatasetId}", Id);
                    return NotFound();
                }

                Dataset = dataset;
                DatasetId = Id;

                // Parse measurement parameters if available
                if (!string.IsNullOrEmpty(dataset.ParametersJson))
                {
                    try
                    {
                        MeasurementParameters = JsonSerializer.Deserialize<Dictionary<string, object>>(dataset.ParametersJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse measurement parameters for dataset {DatasetId}", Id);
                        MeasurementParameters = null;
                    }
                }

                // Parse data points directly from RawDataJson
                DataPoints = ParseRawDataToDataPoints(dataset.RawDataJson);
                _logger.LogInformation(
                    "ViewDatasetModel.OnGetAsync: Parsed {DataPointCount} data points from RawDataJson for dataset {DatasetId}",
                    DataPoints.Count, Id);
                // Load available scripts
                var currentUserId = GetCurrentUserId(); //WHY DO WE HAVE A USER ID YET

                AvailableScripts = await _analysisService.GetAvailableScriptsAsync(currentUserId);

                // Load analysis history
                AnalysisResults = await _analysisService.GetAnalysisHistoryAsync(Id);

                _logger.LogInformation(
                    "Loaded dataset {DatasetId} with {PointCount} points, {ScriptCount} scripts, {ResultCount} results",
                    Id, DataPoints.Count, AvailableScripts.Count, AnalysisResults.Count);

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dataset {DatasetId}", Id);
                TempData["ErrorMessage"] = "Failed to load dataset";
                return RedirectToPage("/Data/DataIndex");
            }
        }

        public async Task<IActionResult> OnPostRunAnalysisAsync(Guid scriptId)
        {
            try
            {
                _logger.LogInformation(
                    "Running analysis: Dataset={DatasetId}, Script={ScriptId}",
                    DatasetId, scriptId);

                // Execute analysis
                var result = await _analysisService.ExecuteAnalysisAsync(
                    DatasetId,
                    scriptId,
                    new Dictionary<string, object>() // No parameters for now
                );

                if (result.Status == AnalysisStatus.Success)
                {
                    TempData["SuccessMessage"] = $"Analysis completed successfully in {result.ExecutionTimeMs}ms";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Analysis failed: {result.ErrorMessage}";
                }

                return RedirectToPage(new { id = DatasetId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running analysis on dataset {DatasetId}", DatasetId);
                TempData["ErrorMessage"] = "An error occurred while running the analysis";
                return RedirectToPage(new { id = DatasetId });
            }
        }

        public async Task<IActionResult> OnPostDeleteResultAsync(Guid resultId)
        {
            try
            {
                var success = await _analysisService.DeleteAnalysisResultAsync(resultId);

                if (success)
                {
                    TempData["SuccessMessage"] = "Analysis result deleted successfully";
                }
                else
                {
                    TempData["ErrorMessage"] = "Analysis result not found";
                }

                return RedirectToPage(new { id = DatasetId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting analysis result {ResultId}", resultId);
                TempData["ErrorMessage"] = "An error occurred while deleting the result";
                return RedirectToPage(new { id = DatasetId });
            }
        }

        private string GetCurrentUserId()
        {
            return User.Identity?.Name ?? "default_user";
        }

        private List<DataPointEntity> ParseRawDataToDataPoints(string? rawDataJson)
        {
            var result = new List<DataPointEntity>();

            if (string.IsNullOrEmpty(rawDataJson))
            {
                return result;
            }

            try
            {
                // Just deserialize the raw lines - don't overthink it
                var lines = System.Text.Json.JsonSerializer.Deserialize<List<string>>(rawDataJson);
                if (lines == null || lines.Count == 0)
                {
                    return result;
                }

                // Simply display each line as a data point - minimal parsing just for display
                int rowIndex = 0;
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Just show the raw line - no complex parsing
                    result.Add(new DataPointEntity
                    {
                        Timestamp = DateTime.UtcNow.AddSeconds(rowIndex),
                        ParameterName = $"Line {rowIndex + 1}",
                        Value = line,
                        Unit = null,
                        Notes = null,
                        RowIndex = rowIndex++
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse RawDataJson");
            }

            return result;
        }
    }
}
