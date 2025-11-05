using Microsoft.Extensions.Logging;
using SmartLab.Domains.Measurement.Models;
using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Device.Interfaces;

namespace SmartLab.Domains.Measurement.Services
{
    public class MeasurementFactory : IMeasurementFactory
    {
        private readonly ILogger<MeasurementFactory> _logger;

        public MeasurementFactory(ILogger<MeasurementFactory> logger)
        {
            _logger = logger;
        }

        public IMeasurement CreateMeasurement(IDevice device)
        {
            _logger.LogInformation("Creating measurement for device {DeviceName}", 
                device.DeviceName);
            

                var measurement = new DeviceMeasurement(device);
                _logger.LogInformation("Created parameterized measurement with ID {MeasurementId} for device {DeviceName}", 
                    measurement.MeasurementID, device.DeviceName);
                return measurement;
        }
    }
}