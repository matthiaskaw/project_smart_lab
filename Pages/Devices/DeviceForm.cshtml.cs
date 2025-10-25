using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using SmartLab.Domains.Measurement.Models;
using SmartLab.Domains.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Models;

namespace smarthome_webserver.Pages.Devices
{
    [BindProperties]
    public class DeviceFormModel : PageModel
    {
        private readonly IDeviceController _deviceController;
        
        public DeviceFormModel(IDeviceController deviceController)
        {
            _deviceController = deviceController;
        }
        
        public DeviceElement DeviceElement { get; set; } = new DeviceElement();
        public bool IsEditMode => DeviceElement.DeviceID != Guid.Empty;
        public string PageTitle => IsEditMode ? "Edit Device" : "Add New Device";
        
        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id.HasValue)
            {
                // Edit mode - load existing device
                DeviceElement = new DeviceElement 
                { 
                    DeviceID = id.Value, 
                    ButtonText = "Save Changes" 
                };
                
                var device = await _deviceController.GetDeviceAsync(id.Value);
                if (device == null)
                {
                    TempData["Error"] = "Device not found";
                    return RedirectToPage("/Devices/DevicesIndex");
                }
                
                DeviceElement.DeviceName = device.DeviceName ?? "";
                DeviceElement.DeviceExecutablePath = device.DeviceExecutablePath ?? "";
            }
            else
            {
                // Add mode - new device
                DeviceElement = new DeviceElement 
                { 
                    DeviceID = Guid.Empty, 
                    ButtonText = "Add Device" 
                };
            }
            
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(DeviceElement.DeviceName))
                {
                    TempData["Error"] = "Device name is required";
                    return Page();
                }
                
                // Validate ProxyDevice specific requirements
                if (string.IsNullOrWhiteSpace(DeviceElement.DeviceExecutablePath))
                {
                    TempData["Error"] = "Executable path is required";
                    return Page();
                }

                if (IsEditMode)
                {
                    // Update existing device
                    await UpdateDeviceAsync();
                    TempData["Message"] = $"Device '{DeviceElement.DeviceName}' updated successfully!";
                }
                else
                {
                    // Create new device
                    await CreateDeviceAsync();
                    TempData["Message"] = $"Device '{DeviceElement.DeviceName}' added successfully!";
                }
                
                return RedirectToPage("/Devices/DevicesIndex");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Error saving device: {ex.Message}");
                TempData["Error"] = $"Error saving device: {ex.Message}";
                return Page();
            }
        }
        
        private async Task CreateDeviceAsync()
        {
            var config = new DeviceConfiguration
            {
                DeviceID = Guid.NewGuid(),
                DeviceName = DeviceElement.DeviceName,
                DeviceExecutablePath = DeviceElement.DeviceExecutablePath ?? ""
            };
            
            await _deviceController.CreateDeviceAsync(config);
        }
        
        private async Task UpdateDeviceAsync()
        {
            var device = await _deviceController.GetDeviceAsync(DeviceElement.DeviceID);
            if (device == null)
            {
                throw new InvalidOperationException("Device not found");
            }

            // Update device properties
            device.DeviceName = DeviceElement.DeviceName;
            device.DeviceExecutablePath = DeviceElement.DeviceExecutablePath ?? "";

            // Save changes through controller
            await _deviceController.UpdateDeviceAsync(device);
        }
    }
}