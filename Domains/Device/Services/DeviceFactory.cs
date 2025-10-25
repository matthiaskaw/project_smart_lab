using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SmartLab.Domains.Device.Services
{
    public class DeviceFactory : IDeviceFactory
    {
        private readonly ILogger<DeviceFactory> _logger;
        private readonly IServiceProvider _serviceProvider;

        public DeviceFactory(ILogger<DeviceFactory> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public IDevice CreateDevice(DeviceConfiguration config)
        {
            try
            {
                var device = CreateProxyDevice(config);
                ConfigureDevice(device, config);
                _logger.LogInformation("Created ProxyDevice with ID {DeviceId}", config.DeviceID);
                
                return device;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create device with ID {DeviceId}", config.DeviceID);
                throw;
            }
        }

        public bool CanCreateDevice(DeviceConfiguration config)
        {
            // All devices are ProxyDevices now - just check if executable path is provided
            return !string.IsNullOrWhiteSpace(config.DeviceExecutablePath);
        }

        private IDevice CreateProxyDevice(DeviceConfiguration config)
        {
            var communication = _serviceProvider.GetService<IProxyDeviceCommunication>();
            var processManager = _serviceProvider.GetService<IProxyDeviceProcessManager>();
            var logger = _serviceProvider.GetService<ILogger<ProxyDevice>>();
            
            if (communication == null || processManager == null || logger == null)
            {
                throw new InvalidOperationException("Required services for ProxyDevice are not registered");
            }
            
            return new ProxyDevice(communication, processManager, logger);
        }


        private void ConfigureDevice(IDevice device, DeviceConfiguration config)
        {
            device.DeviceID = config.DeviceID;
            device.DeviceName = config.DeviceName ?? string.Empty;
            device.DeviceIdentifier = config.DeviceIdentifier ?? string.Empty;
            device.DeviceExecutablePath = config.DeviceExecutablePath;
        }
    }
}