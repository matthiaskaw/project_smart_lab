using System.Net;
using SmartLab.Domains.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.ComponentModel;
using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Models;

namespace SmartLab.Domains.Device.Controllers{

    public class DeviceController : IDeviceController
    {
        private readonly IDeviceFactory _factory;
        private readonly IDeviceRepository _repository;
        private readonly IDeviceRegistry _registry;
        private readonly ILogger<DeviceController> _logger;
        private static DeviceController? _instance;
        private static readonly object _lock = new object();

        public DeviceController(
            IDeviceFactory factory,
            IDeviceRepository repository,
            IDeviceRegistry registry,
            ILogger<DeviceController> logger)
        {
            _factory = factory;
            _repository = repository;
            _registry = registry;
            _logger = logger;
        }

        private DeviceController()
        {
            throw new InvalidOperationException("Use dependency injection constructor");
        }

        public async Task<IDevice> CreateDeviceAsync(DeviceConfiguration config)
        {
            try
            {
                if (!_factory.CanCreateDevice(config))
                {
                    throw new NotSupportedException($"Device configuration is invalid: {config.DeviceName}");
                }

                var device = _factory.CreateDevice(config);
                await _repository.SaveAsync(config);
                await _registry.RegisterDeviceAsync(device);
                
                _logger.LogInformation("Created and registered device {DeviceName} with ID {DeviceId}", 
                    config.DeviceName, config.DeviceID);
                
                return device;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create device {DeviceName}", config.DeviceName);
                throw;
            }
        }  
        public async Task LoadDevicesAsync()
        {
            try
            {
                var configurations = await _repository.GetAllAsync();
                foreach (var config in configurations)
                {
                    if (_factory.CanCreateDevice(config))
                    {
                        var device = _factory.CreateDevice(config);
                        await _registry.RegisterDeviceAsync(device);
                        _logger.LogInformation("Loaded device {DeviceName} with ID {DeviceId}", 
                            config.DeviceName, config.DeviceID);
                    }
                    else
                    {
                        _logger.LogWarning("Cannot create device {DeviceName}, invalid configuration", config.DeviceName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load devices");
                throw;
            }
        }


        public async Task<IDevice?> GetDeviceAsync(Guid id)
        {
            return await _registry.GetDeviceAsync(id);
        }

        public async Task<IEnumerable<IDevice>> GetAllDevicesAsync()
        {
            await Task.CompletedTask;
            return _registry.GetAllDevices();
        }

        [Obsolete("Use dependency injection instead of singleton pattern")]
        public static DeviceController Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            throw new InvalidOperationException("DeviceController must be configured through dependency injection. Use IDeviceController interface.");
                        }
                    }
                }
                return _instance;
            }
        }
        public async Task<IDevice?> RequestDeviceAsync(string deviceName)
        {
            try
            {
                var devices = _registry.GetAllDevices();
                var device = devices.FirstOrDefault(d => d.DeviceName == deviceName);
                if (device != null)
                {
                    _logger.LogInformation("Found device {DeviceName} with ID {DeviceId}", 
                        deviceName, device.DeviceID);
                }
                else
                {
                    _logger.LogWarning("No device found with name {DeviceName}", deviceName);
                }
                await Task.CompletedTask;
                return device;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting device with name {DeviceName}", deviceName);
                throw;
            }
        }

        public async Task RemoveDeviceAsync(Guid id)
        {
            try
            {
                await _registry.UnregisterDeviceAsync(id);
                await _repository.DeleteAsync(id);
                _logger.LogInformation("Removed device with ID {DeviceId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove device with ID {DeviceId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<IDevice>> GetDevicesByNameAsync(string deviceName)
        {
            await Task.CompletedTask;
            var devices = _registry.GetAllDevices();
            return devices.Where(d => d.DeviceName.Contains(deviceName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task UpdateDeviceAsync(IDevice device)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(device);
                
                // Update device configuration in repository
                var config = new DeviceConfiguration
                {
                    DeviceID = device.DeviceID,
                    DeviceName = device.DeviceName,
                    DeviceExecutablePath = device.DeviceExecutablePath ?? "",
                    DeviceIdentifier = device.DeviceIdentifier ?? ""
                };
                
                await _repository.SaveAsync(config);
                _logger.LogInformation("Updated device {DeviceName} with ID {DeviceId}", 
                    device.DeviceName, device.DeviceID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update device with ID {DeviceId}", device.DeviceID);
                throw;
            }
        }
    }
}