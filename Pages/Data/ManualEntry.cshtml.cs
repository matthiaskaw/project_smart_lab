using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Data.Models;

namespace SmartLab.Pages.Data
{
    [BindProperties]
    public class ManualEntryModel : PageModel
    {
        private readonly IDataService _dataService;
        private readonly ILogger<ManualEntryModel> _logger;

        public DateTime DatasetDateTime { get; set; }
        public string DatasetName { get; set; } = string.Empty;
        public string DatasetDescription { get; set; } = string.Empty;

        public ManualEntryModel(
            IDataService dataService,
            ILogger<ManualEntryModel> logger)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void OnGet()
        {
            DatasetDateTime = DateTime.Now;
            _logger.LogInformation("Manual entry page accessed");
        }

        public async Task<IActionResult> OnPostAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No file selected for upload");
                return BadRequest("No file selected!");
            }

            if (file.Length > 10 * 1024 * 1024)
            {
                _logger.LogWarning("File too large: {FileSize} bytes", file.Length);
                return BadRequest("File too large (max 10MB).");
            }

            var allowed = new[] { ".txt", ".csv" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
            {
                _logger.LogWarning("Invalid file extension: {Extension}", ext);
                return BadRequest("File extension not allowed. Only .txt and .csv files are supported.");
            }

            try
            {
                // Create import request
                var importRequest = new ImportRequest
                {
                    File = file,
                    DatasetName = string.IsNullOrWhiteSpace(DatasetName) ? Path.GetFileNameWithoutExtension(file.FileName) : DatasetName,
                    Description = DatasetDescription ?? string.Empty,
                    Options = new ImportOptions
                    {
                        HasHeader = ext == ".csv",
                        Delimiter = ext == ".csv" ? ',' : '\t',
                        TimestampFormat = "yyyy-MM-dd HH:mm:ss"
                    }
                };

                // Import the dataset
                var datasetId = await _dataService.ImportDatasetAsync(importRequest);

                _logger.LogInformation("Successfully imported dataset {DatasetId} from file {FileName}",
                    datasetId, file.FileName);

                return RedirectToPage("/Data/DataIndex");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import dataset from file {FileName}", file.FileName);
                ModelState.AddModelError(string.Empty, "Failed to import dataset. Please check the file format.");
                return Page();
            }
        }
    }
}
