using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Models;

namespace SmartLab.Domains.Device.Interfaces
{
    public interface IDeviceFactory
    {
        IDevice CreateDevice(DeviceConfiguration config);
        bool CanCreateDevice(DeviceConfiguration config);
    }
}