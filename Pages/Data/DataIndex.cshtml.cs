using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Data.Models;

public class IndexDatasetsModel : PageModel
{
    private readonly IDataService _dataService;
    private readonly ILogger<IndexDatasetsModel> _logger;

    public IndexDatasetsModel(
        IDataService dataService,
        ILogger<IndexDatasetsModel> logger)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
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
}
