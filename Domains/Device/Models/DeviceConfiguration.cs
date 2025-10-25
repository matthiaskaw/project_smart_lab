using SmartLab.Domains.Device.Interfaces;

namespace SmartLab.Domains.Device.Models
{
    public class DeviceConfiguration
    {
        public Guid DeviceID { get; set; } = Guid.NewGuid();
        public string? DeviceName { get; set; } = string.Empty;
        public string? DeviceIdentifier { get; set; } = string.Empty;
        public string DeviceExecutablePath { get; set; } = string.Empty;

        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }
}