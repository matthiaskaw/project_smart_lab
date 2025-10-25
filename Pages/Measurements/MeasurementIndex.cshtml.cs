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

namespace smarthome_webserver.Pages.Measurements{
[BindProperties]

public class MeasurementIndex : PageModel
{
    private readonly IMeasurementController _measurementController;
    private readonly IDeviceController _deviceController;
    private readonly IConfiguredMeasurementService _configuredMeasurementService;
    
    public MeasurementIndex(IMeasurementController measurementController, IDeviceController deviceController, IConfiguredMeasurementService configuredMeasurementService)
    {
        _measurementController = measurementController;
        _deviceController = deviceController;
        _configuredMeasurementService = configuredMeasurementService;
    }

    public string Name {get; set;}
    public Guid SelectedDeviceId {get; set;}
    public List<SelectListItem> AvailableDevices {get; set;} = new List<SelectListItem>();
    public List<Guid> SelectedItems { get; set; } = new();
    public IEnumerable<IMeasurement> RunningMeasurements { get; private set; } = Enumerable.Empty<IMeasurement>();
    public IEnumerable<ConfiguredMeasurement> ConfiguredMeasurements { get; private set; } = Enumerable.Empty<ConfiguredMeasurement>();
    
    private static DateTime _lastUpdateCheck = DateTime.Now;
    private static int _lastRunningCount = 0;

    public async Task OnGet()
    {
        var devices = await _deviceController.GetAllDevicesAsync();
        foreach(var device in devices){
            SelectListItem item = new SelectListItem(){
                Value = device.DeviceID.ToString(), 
                Text = $"{device.DeviceName} (ProxyDevice)"
            };
            AvailableDevices.Add(item);
        }
        
        RunningMeasurements = await _measurementController.GetRunningMeasurementsAsync();
        ConfiguredMeasurements = await _configuredMeasurementService.GetAllAsync();
        
        // Update tracking for auto-refresh
        _lastRunningCount = RunningMeasurements.Count();
        _lastUpdateCheck = DateTime.Now;
    }



    public async Task<IActionResult> OnPostCancelMeasurement(Guid id)
    {   
        Console.WriteLine($"Received id: {id}");  // Debugging to check if id is passed correctly
        Logger.Instance.LogInfo($"MeasurementIndex.OnPostCancelMeasurement: Trying to cancel {id}");
        await _measurementController.CancelMeasurementAsync(id);
        return RedirectToPage();         
    }

    public async Task<IActionResult> OnPostStartConfiguredMeasurement(Guid configId, string instanceName)
    {
        try
        {
            var configuredMeasurement = await _configuredMeasurementService.GetByIdAsync(configId);
            if (configuredMeasurement == null)
            {
                Logger.Instance.LogError($"MeasurementIndex.OnPostStartConfiguredMeasurement: Configured measurement {configId} not found");
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(instanceName))
            {
                instanceName = configuredMeasurement.MeasurementName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }

            Logger.Instance.LogInfo($"MeasurementIndex.OnPostStartConfiguredMeasurement: Starting measurement '{instanceName}' (config: '{configuredMeasurement.MeasurementName}') on device {configuredMeasurement.DeviceId}");
            await _measurementController.StartMeasurementAsync(configuredMeasurement.DeviceId, instanceName);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"MeasurementIndex.OnPostStartConfiguredMeasurement: Error starting measurement: {ex.Message}");
        }
        
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveConfiguredMeasurement(Guid configId)
    {
        try
        {
            Logger.Instance.LogInfo($"MeasurementIndex.OnPostRemoveConfiguredMeasurement: Removing configured measurement {configId}");
            await _configuredMeasurementService.RemoveAsync(configId);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"MeasurementIndex.OnPostRemoveConfiguredMeasurement: Error removing measurement: {ex.Message}");
        }
        
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
public class Item
{
    public Guid Guid { get; set; }
    public DateTime Date {get; set;}
    public string Name { get; set; }
}}