using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using SmartLab.Domains.Measurement.Models;
using SmartLab.Domains.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Models;

namespace smarthome_webserver.Pages.Devices{
[BindProperties]

public class DevicesIndexModel : PageModel
{
    private readonly IDeviceController _deviceController;
    
    public DevicesIndexModel(IDeviceController deviceController)
    {
        _deviceController = deviceController;
    }    

    public IEnumerable<IDevice> Devices { get; private set; } = Enumerable.Empty<IDevice>();
    
    public async Task OnGet()
    {
        Devices = await _deviceController.GetAllDevicesAsync();
    }


    public async Task<IActionResult> OnPostRemoveDevice(Guid id)
    {
        try
        {
            Logger.Instance.LogInfo($"DevicesIndexModel.OnPostDeleteDevice: Trying to delete device with id {id}");
            
            // Get device info for feedback message
            var device = await _deviceController.GetDeviceAsync(id);
            var deviceName = device?.DeviceName ?? "Unknown";
            
            await _deviceController.RemoveDeviceAsync(id);
            
            TempData["Message"] = $"Device '{deviceName}' removed successfully!";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"Error removing device: {ex.Message}");
            TempData["Error"] = $"Error removing device: {ex.Message}";
            return RedirectToPage();
        }
    }
}
}