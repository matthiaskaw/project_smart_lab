using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Data.Models;

public class IndexDatasetsModel : PageModel
{
    private readonly IDataService _dataService;
    private readonly IDataExportService _exportService;
    private readonly ILogger<IndexDatasetsModel> _logger;

    public IndexDatasetsModel(
        IDataService dataService,
        IDataExportService exportService,
        ILogger<IndexDatasetsModel> logger)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _logger = logger;
    }

    public List<DatasetSummary> Datasets { get; set; } = new();

    public async Task OnGetAsync()
    {
        try
        {
            Datasets = await _dataService.GetDatasetSummariesAsync();
            _logger.LogInformation("Loaded {Count} datasets from database", Datasets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load datasets");
            Datasets = new List<DatasetSummary>();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        try
        {
            var result = await _dataService.DeleteDatasetAsync(id);
            if (result)
            {
                _logger.LogInformation("Successfully deleted dataset {DatasetId}", id);
            }
            else
            {
                _logger.LogWarning("Dataset {DatasetId} not found for deletion", id);
            }
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete dataset {DatasetId}", id);
            return Page();
        }
    }

    public IActionResult OnPostView(Guid id)
    {
        _logger.LogInformation("Viewing dataset with ID = {DatasetId}", id);
        return RedirectToPage("ViewDataset", new { id });
    }

    public async Task<IActionResult> OnPostDownloadAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Downloading dataset {DatasetId}", id);

            // Get dataset info for filename
            var dataset = await _dataService.GetDatasetAsync(id);
            if (dataset == null)
            {
                _logger.LogWarning("Dataset {DatasetId} not found for download", id);
                return NotFound();
            }

            // Export raw data (device controls format)
            var data = await _exportService.ExportToCsvAsync(id);

            // Create safe filename (use .txt extension - device controls actual format)
            var safeFileName = string.Join("_", dataset.Name.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{safeFileName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

            _logger.LogInformation("Downloaded dataset {DatasetId} as {FileName} ({Size} bytes)",
                id, fileName, data.Length);

            return File(data, "text/plain", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download dataset {DatasetId}", id);
            return StatusCode(500);
        }
    }

    public async Task<IActionResult> OnPostRepairDataPointsAsync()
    {
        try
        {
            _logger.LogInformation("Starting DataPoints repair for all datasets");
            var repairedCount = await _dataService.RepairDataPointsForAllDatasetsAsync();

            TempData["SuccessMessage"] = $"Successfully repaired {repairedCount} dataset(s)";
            _logger.LogInformation("Repaired {Count} datasets", repairedCount);

            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to repair DataPoints");
            TempData["ErrorMessage"] = "Failed to repair datasets";
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnGetDebugDataAsync(Guid id)
    {
        try
        {
            var dataset = await _dataService.GetDatasetAsync(id);
            if (dataset == null)
            {
                return Content("Dataset not found");
            }

            var debug = $"Dataset: {dataset.Name}\n";
            debug += $"DataSource: {dataset.DataSource}\n";
            debug += $"EntryMethod: {dataset.EntryMethod}\n";
            debug += $"DataPoints count: {dataset.DataPoints.Count}\n\n";
            debug += $"RawDataJson:\n{dataset.RawDataJson}\n\n";
            debug += $"ParametersJson:\n{dataset.ParametersJson}";

            return Content(debug, "text/plain");
        }
        catch (Exception ex)
        {
            return Content($"Error: {ex.Message}");
        }
    }
}
