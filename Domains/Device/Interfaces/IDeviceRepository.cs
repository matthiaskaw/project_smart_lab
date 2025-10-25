using SmartLab.Domains.Device.Models;

namespace SmartLab.Domains.Device.Interfaces
{
    public interface IDeviceRepository
    {
        Task<IEnumerable<DeviceConfiguration>> GetAllAsync();
        Task<DeviceConfiguration?> GetByIdAsync(Guid id);
        Task SaveAsync(DeviceConfiguration config);
        Task DeleteAsync(Guid id);
        Task SaveAllAsync(IEnumerable<DeviceConfiguration> configurations);
    }
}