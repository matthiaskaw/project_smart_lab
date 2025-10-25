using SmartLab.Domains.Device.Interfaces;

namespace SmartLab.Domains.Device.Interfaces
{
    public interface IDeviceRegistry
    {
        Task<IDevice?> GetDeviceAsync(Guid id);
        Task RegisterDeviceAsync(IDevice device);
        Task UnregisterDeviceAsync(Guid id);
        IEnumerable<IDevice> GetDevicesByName(string name);
        IEnumerable<IDevice> GetAllDevices();
        bool IsDeviceRegistered(Guid id);
    }
}