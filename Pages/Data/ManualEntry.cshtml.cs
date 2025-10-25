using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartHome.Domains.Data.Interfaces;
using SmartHome.Domains.Data.Models;

namespace SmartHome.Pages.Data
{
    [BindProperties]
    public class ManualEntryModel : PageModel
    {


        public DateTime DateTime { get; set; }
        private readonly IDataController _dataController;
        private readonly ILogger<ManualEntryModel> _logger;
        public DateTime DatasetDateTime { get; set; }
        public string DatasetName { get; set; }
        public string DatasetDescription { get; set; }

        private Microsoft.AspNetCore.Hosting.IWebHostEnvironment _hostingEnviroment { get; set; }


        public ManualEntryModel(
            IDataController dataController,
            ILogger<ManualEntryModel> logger)
        {
            _dataController = dataController ?? throw new ArgumentNullException(nameof(dataController));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void OnGet()
        {
            _logger.LogInformation("Manual entry page accessed");
        }

        public async Task<IActionResult> OnPostAsync(IFormFile file)
        {

            // Logger.Instance.LogInfo($"ManualEntryModel.OnPostAsync: DatasetName = {DatasetName}");
            // Logger.Instance.LogInfo($"ManualEntryModel.OnPostAsync: DatasetName = {DatasetDateTime}");
            // Logger.Instance.LogInfo($"ManualEntryModel.OnPostAsync: DatasetName = {DatasetDescription}");
            // Logger.Instance.LogInfo($"ManualEntryModel.OnPostAsync: DatasetName = {DatasetFilepath}");

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file selected!");
            }

            if (file.Length > 10 * 1024 * 1024)
            {
                return BadRequest("Datei zu groß.");
            }

            var allowed = new[] { ".txt", ".csv" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
            {
                return BadRequest("File extension not allowed!");
            }
        
            Dataset ds = new Dataset();
            ds.DatasetID = Guid.NewGuid();
            ds.DatasetDate = DatasetDateTime;
            ds.DatasetName = DatasetName;
            await ds.SaveDatasetFromUpload(file);
            await _dataController.AddDatasetAsync(ds);
            await _dataController.WriteDatasetsAsync();

            //File upload
            return RedirectToPage("/Data/DataIndex");

        }

    }
}