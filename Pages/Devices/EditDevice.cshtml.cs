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

public class EditDeviceModel : PageModel
{
    private readonly IDeviceController _deviceController;
    
    public EditDeviceModel(IDeviceController deviceController)
    {
        _deviceController = deviceController;
    }
    
    public DeviceElement DeviceElement{ get; set; } = new DeviceElement();
    public async Task OnGet(Guid id){
        DeviceElement = new DeviceElement(){DeviceID=id, ButtonText="Edit"};
        
        var device = await _deviceController.GetDeviceAsync(id);
        if (device != null)
        {
            DeviceElement.DeviceName = device.DeviceName;
            DeviceElement.DeviceExecutablePath = device.DeviceExecutablePath;
        }
        
        Logger.Instance.LogInfo($"EditDeviceModel.OnGet: Device.Element.DeviceID is {DeviceElement.DeviceID}");        
    }

    public async Task<IActionResult> OnPostEditDevice(){
        try
        {
            var device = await _deviceController.GetDeviceAsync(DeviceElement.DeviceID);
            if (device == null)
            {
                Logger.Instance.LogError($"Device with ID {DeviceElement.DeviceID} not found");
                return RedirectToPage("/Settings/SettingsIndex");
            }

            device.DeviceName = DeviceElement.DeviceName;
            device.DeviceExecutablePath = DeviceElement.DeviceExecutablePath;

            // Update the device through the controller
            await _deviceController.UpdateDeviceAsync(device);

            return RedirectToPage("/Settings/SettingsIndex");
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"Error editing device: {ex.Message}");
            return RedirectToPage("/Settings/SettingsIndex");
        }
    }
}}