using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using SmartLab.Domains.Measurement.Models;
using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Measurement.Services;
using SmartLab.Domains.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace smarthome_webserver.Pages.Measurements
{
    [BindProperties]

    public class MeasurementIndex : PageModel
    {
        private readonly IMeasurementController _measurementController;
        private readonly IDeviceController _deviceController;

        public MeasurementIndex(IMeasurementController measurementController, IDeviceController deviceController)
        {
            _measurementController = measurementController;
            _deviceController = deviceController;
        }

        public string Name { get; set; }
        public Guid SelectedDeviceId { get; set; }
        public List<SelectListItem> AvailableDevices { get; set; } = new List<SelectListItem>();
        public List<Guid> SelectedItems { get; set; } = new();
        public IEnumerable<IMeasurement> RunningMeasurements { get; private set; } = Enumerable.Empty<IMeasurement>();
        public IEnumerable<ConfiguredMeasurement> ConfiguredMeasurements { get; private set; } = Enumerable.Empty<ConfiguredMeasurement>();

        private static DateTime _lastUpdateCheck = DateTime.Now;
        private static int _lastRunningCount = 0;

        public async Task OnGet()
        {
            var devices = await _deviceController.GetAllDevicesAsync();
            foreach (var device in devices)
            {
                SelectListItem item = new SelectListItem()
                {
                    Value = device.DeviceID.ToString(),
                    Text = $"{device.DeviceName} (ProxyDevice)"
                };
                AvailableDevices.Add(item);
            }

            RunningMeasurements = await _measurementController.GetRunningMeasurementsAsync();
            // ConfiguredMeasurements = await _configuredMeasurementService.GetAllAsync();

            // Update tracking for auto-refresh
            _lastRunningCount = RunningMeasurements.Count();
            _lastUpdateCheck = DateTime.Now;
        }


        public async Task<IActionResult> OnPostStartMeasurement(string deviceId, string name)
        {
            Logger.Instance.LogInfo($"OnPostStartMeasurement: {name}");
            Guid deviceID;
            Guid.TryParse(deviceId, out deviceID);

            try
            {

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                }

                Logger.Instance.LogInfo($"MeasurementIndex.OnPostStartConfiguredMeasurement: Starting measurement '{name}' (config: '{name}') on device {deviceID.ToString()}");
                Guid measurementID = await _measurementController.CreateMeasurementAsync(deviceID, name);
                // List<MeasurementParameter> parameters = await _measurementController.GetDeviceParametersAsync(measurementID);
                return Redirect($"/Measurements/ConfigureParameters?measurementID={measurementID}");
                //Display parameters and wait for user input... then start measuremnt
                //await _measurementController.StartMeasurementAsync(measurementID, name);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"MeasurementIndex.OnPostStartConfiguredMeasurement: Error starting measurement: {ex.Message}");
            }

            return RedirectToPage();

        }
        public async Task<IActionResult> OnPostCancelMeasurement(Guid id)
        {
            Console.WriteLine($"Received id: {id}");  // Debugging to check if id is passed correctly
            Logger.Instance.LogInfo($"MeasurementIndex.OnPostCancelMeasurement: Trying to cancel {id}");
            await _measurementController.CancelMeasurementAsync(id);
            return RedirectToPage();
        }


        public async Task<IActionResult> OnGetCheckUpdates()
        {
            try
            {
                var currentRunningMeasurements = await _measurementController.GetRunningMeasurementsAsync();
                var currentCount = currentRunningMeasurements.Count();

                // If running count decreased, measurements likely completed
                bool shouldRefresh = currentCount < _lastRunningCount;

                // Update tracking
                _lastRunningCount = currentCount;
                _lastUpdateCheck = DateTime.Now;

                return new JsonResult(new { shouldRefresh });
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"MeasurementIndex.OnGetCheckUpdates: Error checking for updates: {ex.Message}");
                return new JsonResult(new { shouldRefresh = false });
            }
        }

    }
}