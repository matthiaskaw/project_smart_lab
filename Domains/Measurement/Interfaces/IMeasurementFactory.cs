using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Device.Interfaces;

namespace SmartLab.Domains.Measurement.Interfaces
{
    public interface IMeasurementFactory
    {
        IMeasurement CreateMeasurement(IDevice device);
    }
}