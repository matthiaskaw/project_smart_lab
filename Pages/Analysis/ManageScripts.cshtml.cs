using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLab.Domains.Analysis.Interfaces;
using SmartLab.Domains.Analysis.Models;

namespace SmartLab.Pages.Analysis
{
    public class ManageScriptsModel : PageModel
    {
        private readonly IScriptManagementService _scriptService;
        private readonly ILogger<ManageScriptsModel> _logger;

        public List<AnalysisScriptMetadata> BuiltInScripts { get; set; } = new();
        public List<AnalysisScriptMetadata> UserScripts { get; set; } = new();
        public List<AnalysisScriptMetadata> SharedScripts { get; set; } = new();

        public ManageScriptsModel(
            IScriptManagementService scriptService,
            ILogger<ManageScriptsModel> logger)
        {
            _scriptService = scriptService;
            _logger = logger;
        }

        public async Task OnGetAsync()
        {
            var currentUserId = GetCurrentUserId();

            BuiltInScripts = await _scriptService.GetBuiltInScriptsAsync();
            UserScripts = await _scriptService.GetUserScriptsAsync(currentUserId);
            SharedScripts = await _scriptService.GetSharedScriptsAsync();
        }

        public async Task<IActionResult> OnPostUploadAsync(
            IFormFile scriptFile,
            string displayName,
            string? description,
            string? tags,
            string? version)
        {
            try
            {
                // Validation
                if (scriptFile == null || scriptFile.Length == 0)
                {
                    TempData["ErrorMessage"] = "Please select a file";
                    return RedirectToPage();
                }

                if (scriptFile.Length > 1048576) // 1 MB
                {
                    TempData["ErrorMessage"] = "File size exceeds 1 MB limit";
                    return RedirectToPage();
                }

                if (!scriptFile.FileName.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "Only Python (.py) files are allowed";
                    return RedirectToPage();
                }

                var currentUserId = GetCurrentUserId();

                // Read file content
                using var stream = scriptFile.OpenReadStream();
                using var reader = new StreamReader(stream);
                var scriptContent = await reader.ReadToEndAsync();

                // Create metadata
                var metadata = new AnalysisScriptMetadata
                {
                    FileName = scriptFile.FileName,
                    DisplayName = displayName,
                    Description = description ?? string.Empty,
                    Author = currentUserId,
                    Tags = tags?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                               .Select(t => t.Trim())
                               .ToList() ?? new List<string>(),
                    Version = version ?? "1.0.0",
                    Language = ScriptLanguage.Python
                };

                // Upload and validate
                var result = await _scriptService.UploadScriptAsync(
                    currentUserId,
                    scriptContent,
                    metadata
                );

                if (!result.Success)
                {
                    TempData["ErrorMessage"] = $"Upload failed: {result.ErrorMessage}";
                    return RedirectToPage();
                }

                _logger.LogInformation(
                    "User {UserId} uploaded script {FileName} (validation: {Status})",
                    currentUserId, scriptFile.FileName, result.ValidationStatus
                );

                if (result.ValidationStatus == ValidationStatus.Passed)
                {
                    TempData["SuccessMessage"] = "Script uploaded and validated successfully!";
                }
                else if (result.ValidationStatus == ValidationStatus.Warning)
                {
                    TempData["SuccessMessage"] = $"Script uploaded with warnings: {string.Join(", ", result.ValidationWarnings)}";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Script validation failed: {string.Join(", ", result.ValidationErrors)}";
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload script");
                TempData["ErrorMessage"] = "An error occurred during upload";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var success = await _scriptService.DeleteUserScriptAsync(currentUserId, id);

                if (success)
                {
                    TempData["SuccessMessage"] = "Script deleted successfully";
                }
                else
                {
                    TempData["ErrorMessage"] = "Script not found or access denied";
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete script {ScriptId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the script";
                return RedirectToPage();
            }
        }

        private string GetCurrentUserId()
        {
            // TODO: Integrate with authentication system
            // For now, use a default user ID
            return User.Identity?.Name ?? "default_user";
        }
    }
}
