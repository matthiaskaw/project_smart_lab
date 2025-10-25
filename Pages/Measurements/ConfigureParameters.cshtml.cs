using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartHome.Domains.Measurement.Interfaces;
using SmartHome.Domains.Measurement.Models;
using SmartHome.Domains.Core.Services;
using SmartHome.Domains.Device.Interfaces;

namespace smarthome_webserver.Pages.Measurements
{
    [BindProperties]
    public class ConfigureParametersModel : PageModel
    {
        private readonly IMeasurementController _measurementController;
        private readonly IDeviceController _deviceController;

        public ConfigureParametersModel(IMeasurementController measurementController, IDeviceController deviceController)
        {
            _measurementController = measurementController;
            _deviceController = deviceController;
        }

        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string MeasurementName { get; set; } = string.Empty;
        public List<MeasurementParameter> Parameters { get; set; } = new List<MeasurementParameter>();
        public List<string> ParameterValues { get; set; } = new List<string>();
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid deviceId, string? name = null)
        {
            DeviceId = deviceId;
            MeasurementName = name ?? $"Measurement_{DateTime.Now:yyyyMMdd_HHmmss}";

            try
            {
                Parameters = await _measurementController.GetDeviceParametersAsync(deviceId);
                
                // Initialize parameter values list to match parameters count
                ParameterValues = new List<string>();
                for (int i = 0; i < Parameters.Count; i++)
                {
                    ParameterValues.Add(Parameters[i].DefaultValue?.ToString() ?? string.Empty);
                }

                // Get device name
                var device = await _deviceController.GetDeviceAsync(deviceId);
                DeviceName = device?.DeviceName ?? $"Device {deviceId:N}";

                Logger.Instance.LogInfo($"ConfigureParameters: Retrieved {Parameters.Count} parameters for device {DeviceId}");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"ConfigureParameters: Error getting parameters for device {DeviceId}: {ex.Message}");
                ErrorMessage = $"Error loading device parameters: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Logger.Instance.LogInfo($"ConfigureParameters POST: DeviceId={DeviceId}, MeasurementName='{MeasurementName}', Parameters.Count={Parameters?.Count ?? 0}, ParameterValues.Count={ParameterValues?.Count ?? 0}");
            
            if (!ModelState.IsValid)
            {
                foreach (var modelError in ModelState)
                {
                    foreach (var error in modelError.Value.Errors)
                    {
                        Logger.Instance.LogError($"ModelState Error - {modelError.Key}: {error.ErrorMessage}");
                    }
                }
                Logger.Instance.LogInfo("ConfigureParameters POST: ModelState invalid, returning to page");
                return Page();
            }

            try
            {
                // Convert parameter values to dictionary with proper types
                var parameterDict = new Dictionary<string, object>();
                
                for (int i = 0; i < Parameters.Count && i < ParameterValues.Count; i++)
                {
                    var param = Parameters[i];
                    var value = ParameterValues[i];

                    if (string.IsNullOrEmpty(value) && param.IsRequired)
                    {
                        ModelState.AddModelError($"ParameterValues[{i}]", $"{param.DisplayName} is required");
                        continue;
                    }

                    if (!string.IsNullOrEmpty(value))
                    {
                        try
                        {
                            object convertedValue = param.Type switch
                            {
                                ParameterType.String => value,
                                ParameterType.Integer => int.Parse(value),
                                ParameterType.Double => double.Parse(value),
                                ParameterType.Boolean => bool.Parse(value) || value.Equals("true", StringComparison.OrdinalIgnoreCase),
                                ParameterType.DateTime => DateTime.Parse(value),
                                _ => value
                            };

                            parameterDict[param.Name] = convertedValue;
                        }
                        catch (Exception ex)
                        {
                            ModelState.AddModelError($"ParameterValues[{i}]", $"Invalid value for {param.DisplayName}: {ex.Message}");
                        }
                    }
                }

                if (!ModelState.IsValid)
                {
                    return Page();
                }

                Logger.Instance.LogInfo($"ConfigureParameters: Starting measurement '{MeasurementName}' on device {DeviceId} with {parameterDict.Count} parameters");

                var measurementId = await _measurementController.StartMeasurementAsync(DeviceId, MeasurementName, parameterDict);
                
                Logger.Instance.LogInfo($"ConfigureParameters: Measurement started successfully with ID {measurementId}");
                
                return RedirectToPage("/Measurements/MeasurementIndex");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"ConfigureParameters: Error starting measurement: {ex.Message}");
                ErrorMessage = $"Error starting measurement: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostStartWithoutParametersAsync()
        {
            if (string.IsNullOrWhiteSpace(MeasurementName))
            {
                ModelState.AddModelError(nameof(MeasurementName), "Measurement name is required");
                return Page();
            }

            try
            {
                Logger.Instance.LogInfo($"ConfigureParameters: Starting measurement '{MeasurementName}' on device {DeviceId} without parameters");
                
                await _measurementController.StartMeasurementAsync(DeviceId, MeasurementName);
                
                return RedirectToPage("/Measurements/MeasurementIndex");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"ConfigureParameters: Error starting measurement without parameters: {ex.Message}");
                ErrorMessage = $"Error starting measurement: {ex.Message}";
                return Page();
            }
        }
    }
}