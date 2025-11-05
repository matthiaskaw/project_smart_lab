using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Measurement.Models;

namespace SmartLab.Domains.Measurement.Interfaces
{
    public interface IMeasurementController
    {
        Task<Guid> StartMeasurementAsync(Guid measurementID, string name, CancellationToken cancellationToken = default);
        public Task<Guid> CreateMeasurementAsync(Guid deviceId, string name);
        Task CancelMeasurementAsync(Guid measurementID, CancellationToken cancellationToken = default);
        Task<IMeasurement?> GetMeasurementAsync(Guid measurementID);
        Task<IEnumerable<IMeasurement>> GetRunningMeasurementsAsync();
        Task<List<MeasurementParameter>> GetDeviceParametersAsync(Guid deviceId, CancellationToken cancellationToken = default);
    }
}