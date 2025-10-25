using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Measurement.Models;

namespace SmartLab.Domains.Measurement.Interfaces
{
    public interface IMeasurementController
    {
        Task<Guid> StartMeasurementAsync(Guid deviceId, string name, CancellationToken cancellationToken = default);
        Task<Guid> StartMeasurementAsync(Guid deviceId, string name, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
        Task CancelMeasurementAsync(Guid measurementID, CancellationToken cancellationToken = default);
        Task<IMeasurement?> GetMeasurementAsync(Guid measurementID);
        Task<IEnumerable<IMeasurement>> GetRunningMeasurementsAsync();
        Task<List<MeasurementParameter>> GetDeviceParametersAsync(Guid deviceId, CancellationToken cancellationToken = default);
    }
}