using SmartLab.Domains.Device.Interfaces;

namespace SmartLab.Domains.Measurement.Models
{
    public class ConfiguredMeasurement
    {
        public Guid MeasurementConfigId { get; set; }
        public string MeasurementName { get; set; } = string.Empty;
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        // Removed DeviceType - all devices are ProxyDevices now
        public DateTime CreatedDate { get; set; }
        public string Description { get; set; } = string.Empty;

        public ConfiguredMeasurement()
        {
            MeasurementConfigId = Guid.NewGuid();
            CreatedDate = DateTime.Now;
        }
    }
}