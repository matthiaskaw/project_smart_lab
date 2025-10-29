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

    public async Task<IActionResult> OnPostDownloadAsync(Guid id, string format = "csv")
    {
        try
        {
            _logger.LogInformation("Downloading dataset {DatasetId} in {Format} format", id, format);

            // Get dataset info for filename
            var dataset = await _dataService.GetDatasetAsync(id);
            if (dataset == null)
            {
                _logger.LogWarning("Dataset {DatasetId} not found for download", id);
                return NotFound();
            }

            // Export based on format
            byte[] data;
            string contentType;
            string extension;

            switch (format.ToLower())
            {
                case "json":
                    data = await _exportService.ExportToJsonAsync(id);
                    contentType = "application/json";
                    extension = "json";
                    break;
                case "csv":
                default:
                    data = await _exportService.ExportToCsvAsync(id);
                    contentType = "text/csv";
                    extension = "csv";
                    break;
            }

            // Create safe filename
            var safeFileName = string.Join("_", dataset.Name.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{safeFileName}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";

            _logger.LogInformation("Downloaded dataset {DatasetId} as {FileName} ({Size} bytes)",
                id, fileName, data.Length);

            return File(data, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download dataset {DatasetId}", id);
            return StatusCode(500);
        }
    }
}
