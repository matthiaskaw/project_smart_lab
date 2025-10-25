using SmartLab.Domains.Device.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace SmartLab.Domains.Device.Services
{
    public class DeviceRegistry : IDeviceRegistry
    {
        private readonly ConcurrentDictionary<Guid, IDevice> _devices;
        private readonly ILogger<DeviceRegistry> _logger;

        public DeviceRegistry(ILogger<DeviceRegistry> logger)
        {
            _devices = new ConcurrentDictionary<Guid, IDevice>();
            _logger = logger;
        }

        public async Task<IDevice?> GetDeviceAsync(Guid id)
        {
            await Task.CompletedTask; // Make async for future enhancements
            _devices.TryGetValue(id, out var device);
            return device;
        }

        public async Task RegisterDeviceAsync(IDevice device)
        {
            await Task.CompletedTask; // Make async for future enhancements
            
            if (_devices.TryAdd(device.DeviceID, device))
            {
                _logger.LogInformation("Registered device {DeviceName} with ID {DeviceId}", 
                    device.DeviceName, device.DeviceID);
            }
            else
            {
                _logger.LogWarning("Device with ID {DeviceId} is already registered", device.DeviceID);
                throw new InvalidOperationException($"Device with ID {device.DeviceID} is already registered");
            }
        }

        public async Task UnregisterDeviceAsync(Guid id)
        {
            await Task.CompletedTask; // Make async for future enhancements
            
            if (_devices.TryRemove(id, out var device))
            {
                _logger.LogInformation("Unregistered device {DeviceName} with ID {DeviceId}", 
                    device.DeviceName, device.DeviceID);
                
                // Dispose the device if it implements IAsyncDisposable or IDisposable
                if (device is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (device is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            else
            {
                _logger.LogWarning("Device with ID {DeviceId} was not found for unregistration", id);
            }
        }

        public IEnumerable<IDevice> GetDevicesByName(string name)
        {
            return _devices.Values.Where(d => d.DeviceName.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<IDevice> GetAllDevices()
        {
            return _devices.Values.ToList(); // Return a copy to avoid collection modification issues
        }

        public bool IsDeviceRegistered(Guid id)
        {
            return _devices.ContainsKey(id);
        }
    }
}