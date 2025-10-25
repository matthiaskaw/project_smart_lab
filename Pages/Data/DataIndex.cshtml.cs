using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.IO;
using System.Collections.Concurrent;
using System.Text.Json;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Data.Services;
using SmartLab.Domains.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;

public class IndexDatasetsModel : PageModel
{
    private readonly IDataController _dataController;
    private readonly ILogger<IndexDatasetsModel> _logger;

    // Constructor injection - ASP.NET Core will provide these automatically
    public IndexDatasetsModel(
        IDataController dataController,
        ILogger<IndexDatasetsModel> logger)
    {
        _dataController = dataController ?? throw new ArgumentNullException(nameof(dataController));
        _logger = logger;
    }

    public ConcurrentDictionary<Guid, IDataset> Datasets { get; set; } = new();

    public async Task OnGetAsync()
    {
        try
        {
            Datasets = await _dataController.GetAllDatasetsAsync();
            _logger.LogInformation("Loaded {Count} datasets", Datasets.Count);

            foreach (KeyValuePair<Guid, IDataset> kvpair in Datasets)
            {
                _logger.LogInformation("DataIndex.OnGet: {DatasetId}", kvpair.Key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load datasets");
            Datasets = new ConcurrentDictionary<Guid, IDataset>();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        try
        {
            // TODO: Add DeleteDatasetAsync method to IDataController interface
            await _dataController.DeleteDataAsync(id);
            //_logger.LogWarning("Delete functionality not yet implemented for dataset {DatasetId}", id);
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
        _logger.LogInformation("IndexDatasetsModel.OnPostView: Viewing dataset with ID = {DatasetId}", id);
        return RedirectToPage();
    }
}
