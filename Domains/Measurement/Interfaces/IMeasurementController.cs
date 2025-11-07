using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Measurement.Models;

namespace SmartLab.Domains.Measurement.Interfaces
{
    public interface IMeasurementController
    {
        Task<Guid> StartMeasurementAsync(Guid measurementID, string name, string description = "", CancellationToken cancellationToken = default);
        public Task<Guid> CreateMeasurementAsync(Guid deviceId, string name);
        Task CancelMeasurementAsync(Guid measurementID, CancellationToken cancellationToken = default);
        Task<IMeasurement?> GetMeasurementAsync(Guid measurementID);
        Task<IEnumerable<IMeasurement>> GetRunningMeasurementsAsync();
        Task<List<MeasurementParameter>> GetDeviceParametersAsync(Guid measurementID, CancellationToken cancellationToken = default);
        Task SetDeviceParametersAsync(Guid measurementID, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
    }
}