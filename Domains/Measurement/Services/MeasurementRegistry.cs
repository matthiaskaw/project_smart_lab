using Microsoft.Extensions.Logging;
using SmartLab.Domains.Measurement.Interfaces;
using System.Collections.Concurrent;

namespace SmartLab.Domains.Measurement.Services
{
    public class MeasurementRegistry : IMeasurementRegistry, IDisposable, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<Guid, IMeasurement> _measurements = new();
        private readonly ILogger<MeasurementRegistry> _logger;
        private bool _disposed;

        public MeasurementRegistry(ILogger<MeasurementRegistry> logger)
        {
            _logger = logger;
        }

        public Task RegisterMeasurementAsync(IMeasurement measurement)
        {
            ArgumentNullException.ThrowIfNull(measurement);
            
            if (_measurements.TryAdd(measurement.MeasurementID, measurement))
            {
                _logger.LogInformation("Registered measurement with ID {MeasurementId}", measurement.MeasurementID);
            }
            else
            {
                _logger.LogWarning("Measurement with ID {MeasurementId} is already registered", measurement.MeasurementID);
            }
            
            return Task.CompletedTask;
        }

        public Task UnregisterMeasurementAsync(Guid measurementId)
        {
            if (_measurements.TryRemove(measurementId, out var measurement))
            {
                _logger.LogInformation("Unregistered measurement with ID {MeasurementId}", measurementId);
                
                // Cancel the measurement if it's still running
                if (measurement is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            else
            {
                _logger.LogWarning("Measurement with ID {MeasurementId} was not found for unregistration", measurementId);
            }
            
            return Task.CompletedTask;
        }

        public Task<IMeasurement?> GetMeasurementAsync(Guid measurementId)
        {
            _measurements.TryGetValue(measurementId, out var measurement);
            return Task.FromResult(measurement);
        }

        public Task<IEnumerable<IMeasurement>> GetAllMeasurementsAsync()
        {
            return Task.FromResult<IEnumerable<IMeasurement>>(_measurements.Values.ToList());
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _logger?.LogInformation("Disposing measurement registry with {Count} measurements", _measurements.Count);

            foreach (var measurement in _measurements.Values)
            {
                try
                {
                    if (measurement is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else if (measurement is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error disposing measurement {MeasurementId}", measurement.MeasurementID);
                }
            }

            _measurements.Clear();
        }
    }
}