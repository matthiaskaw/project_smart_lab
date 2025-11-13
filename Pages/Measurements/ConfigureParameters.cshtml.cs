using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Measurement.Models;
using SmartLab.Domains.Core.Services;
using SmartLab.Domains.Device.Interfaces;

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

        public Guid DeviceID { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string MeasurementName { get; set; } = string.Empty;
        public string MeasurementDescription { get; set; } = string.Empty;
        public List<MeasurementParameter> Parameters { get; set; } = new List<MeasurementParameter>();
        public List<string> ParameterValues { get; set; } = new List<string>();
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string deviceId, string? name = null)
        {
            Logger.Instance.LogInfo($"ConfigureParameter.OnGet: deviceId passed = {deviceId}");
            DeviceID = new Guid(deviceId);
            MeasurementName = name ?? DateTime.Now.ToString("yyyyMMdd_HHmmss");

            try
            {
                // Get device to fetch parameters - no measurement created yet
                var device = await _deviceController.GetDeviceAsync(DeviceID);
                if (device == null)
                {
                    ErrorMessage = $"Device {deviceId} not found";
                    return Page();
                }

                DeviceName = device.DeviceName;
                Parameters = await device.GetRequiredParametersAsync();

                // Initialize parameter values list to match parameters count
                ParameterValues = new List<string>();
                for (int i = 0; i < Parameters.Count; i++)
                {
                    ParameterValues.Add(Parameters[i].DefaultValue?.ToString() ?? string.Empty);
                    foreach(var str in Parameters[i].ValidationRules) {
                        Logger.Instance.LogInfo($"ValidationRule: {str.Key} = {str.Value}");
                    }
                }

                Logger.Instance.LogInfo($"ConfigureParameters: Retrieved {Parameters.Count} parameters for device {DeviceID}");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"ConfigureParameters: Error getting parameters for device {DeviceID}: {ex.Message}");
                ErrorMessage = $"Error loading device parameters: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Logger.Instance.LogInfo($"ConfigureParameters POST: DeviceId={DeviceID}, MeasurementName='{MeasurementName}', Parameters.Count={Parameters?.Count ?? 0}, ParameterValues.Count={ParameterValues?.Count ?? 0}");

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

                Logger.Instance.LogInfo($"ConfigureParameters: Creating measurement '{MeasurementName}' on device {DeviceID} with {parameterDict.Count} parameters");

                // Create the measurement
                Guid measurementID = await _measurementController.CreateMeasurementAsync(DeviceID, MeasurementName);
                Logger.Instance.LogInfo($"ConfigureParameters: Created measurement {measurementID}");

                // Set parameters on the measurement
                Logger.Instance.LogInfo($"ConfigureParameters: Setting {parameterDict.Count} parameters on measurement {measurementID}");
                await _measurementController.SetDeviceParametersAsync(measurementID, parameterDict);
                Logger.Instance.LogInfo($"ConfigureParameters: Parameters set successfully on measurement {measurementID}");

                // Start the measurement
                await _measurementController.StartMeasurementAsync(measurementID, MeasurementName, MeasurementDescription);

                Logger.Instance.LogInfo($"ConfigureParameters: Measurement started successfully with ID {measurementID}");

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
                Logger.Instance.LogInfo($"ConfigureParameters: Creating and starting measurement '{MeasurementName}' on device {DeviceID} without parameters");

                // Create the measurement
                Guid measurementID = await _measurementController.CreateMeasurementAsync(DeviceID, MeasurementName);
                Logger.Instance.LogInfo($"ConfigureParameters: Created measurement {measurementID}");

                // Start measurement immediately (no parameters to set)
                await _measurementController.StartMeasurementAsync(measurementID, MeasurementName, MeasurementDescription);

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