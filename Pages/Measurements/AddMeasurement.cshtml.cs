using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Measurement.Services;
using SmartLab.Domains.Measurement.Models;
using SmartLab.Domains.Core.Services;

namespace smarthome_webserver.Pages.Measurements
{
    [BindProperties]
    public class AddMeasurementModel : PageModel
    {
        private readonly IDeviceController _deviceController;
        private readonly IConfiguredMeasurementService _configuredMeasurementService;

        public AddMeasurementModel(IDeviceController deviceController, IConfiguredMeasurementService configuredMeasurementService)
        {
            _deviceController = deviceController;
            _configuredMeasurementService = configuredMeasurementService;
        }

        public string MeasurementName { get; set; } = string.Empty;
        public Guid SelectedDeviceId { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<SelectListItem> AvailableDevices { get; set; } = new();

        public async Task OnGet()
        {
            await LoadDevices();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(MeasurementName))
                {
                    ModelState.AddModelError(nameof(MeasurementName), "Measurement name is required");
                    await LoadDevices();
                    return Page();
                }

                if (SelectedDeviceId == Guid.Empty)
                {
                    ModelState.AddModelError(nameof(SelectedDeviceId), "Please select a device");
                    await LoadDevices();
                    return Page();
                }

                var device = await _deviceController.GetDeviceAsync(SelectedDeviceId);
                if (device == null)
                {
                    ModelState.AddModelError(nameof(SelectedDeviceId), "Selected device not found");
                    await LoadDevices();
                    return Page();
                }

                var configuredMeasurement = new ConfiguredMeasurement
                {
                    MeasurementName = MeasurementName,
                    DeviceId = SelectedDeviceId,
                    DeviceName = device.DeviceName,
                    Description = Description
                };

                await _configuredMeasurementService.AddAsync(configuredMeasurement);

                Logger.Instance.LogInfo($"AddMeasurement: Created measurement config '{MeasurementName}' for device '{device.DeviceName}'");
                
                return RedirectToPage("/Measurements/MeasurementIndex");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"AddMeasurement: Error creating measurement: {ex.Message}");
                ModelState.AddModelError("", "An error occurred while creating the measurement");
                await LoadDevices();
                return Page();
            }
        }

        private async Task LoadDevices()
        {
            var devices = await _deviceController.GetAllDevicesAsync();
            AvailableDevices = devices.Select(device => new SelectListItem
            {
                Value = device.DeviceID.ToString(),
                Text = $"{device.DeviceName} (ProxyDevice)"
            }).ToList();
        }
    }
}