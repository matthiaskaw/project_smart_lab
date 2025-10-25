using SmartLab.Domains.Measurement.Interfaces;

namespace SmartLab.Domains.Measurement.Interfaces
{
    public interface IMeasurementRegistry
    {
        Task RegisterMeasurementAsync(IMeasurement measurement);
        Task UnregisterMeasurementAsync(Guid measurementId);
        Task<IMeasurement?> GetMeasurementAsync(Guid measurementId);
        Task<IEnumerable<IMeasurement>> GetAllMeasurementsAsync();
    }
}