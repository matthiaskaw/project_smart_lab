using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Models;

namespace SmartLab.Domains.Device.Interfaces
{
    public interface IDeviceController
    {
        Task<IDevice> CreateDeviceAsync(DeviceConfiguration config);
        Task<IDevice?> GetDeviceAsync(Guid id);
        Task<IEnumerable<IDevice>> GetAllDevicesAsync();
        Task<IDevice?> RequestDeviceAsync(string deviceName);
        Task RemoveDeviceAsync(Guid id);
        Task<IEnumerable<IDevice>> GetDevicesByNameAsync(string deviceName);
        Task UpdateDeviceAsync(IDevice device);
    }
}