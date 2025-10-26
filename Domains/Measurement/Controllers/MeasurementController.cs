using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Models;
using SmartLab.Domains.Data.Models;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Measurement.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace SmartLab.Domains.Measurement.Controllers
{
    public class MeasurementController : IMeasurementController
    {
        private readonly IMeasurementFactory _factory;
        private readonly IMeasurementRegistry _registry;
        private readonly IDeviceController _deviceController;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MeasurementController> _logger;

        public MeasurementController(
            IMeasurementFactory factory,
            IMeasurementRegistry registry,
            IDeviceController deviceController,
            IServiceProvider serviceProvider,
            ILogger<MeasurementController> logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _deviceController = deviceController ?? throw new ArgumentNullException(nameof(deviceController));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CancelMeasurementAsync(Guid measurementID, CancellationToken cancellationToken = default)
        {
            try
            {
                var measurement = await _registry.GetMeasurementAsync(measurementID);
                if (measurement != null)
                {
                    await measurement.Cancel();
                    await _registry.UnregisterMeasurementAsync(measurementID);
                    _logger.LogInformation("Cancelled measurement {MeasurementId}", measurementID);
                }
                else
                {
                    _logger.LogWarning("Measurement {MeasurementId} not found for cancellation", measurementID);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel measurement {MeasurementId}", measurementID);
                throw;
            }
        }

        private void OnDataAvailable(object? invoker, (Guid measurementID, List<string> data) args)
        {
            // Fire and forget - handle in background with new scope
            _ = Task.Run(async () =>
            {
                // Create a new scope to get fresh instances of scoped services (DbContext, etc.)
                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    try
                    {
                        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();

                        _logger.LogInformation("Measurement ended: {MeasurementId}", args.measurementID);

                        var measurement = await _registry.GetMeasurementAsync(args.measurementID);
                        if (measurement == null)
                        {
                            _logger.LogWarning("Measurement {MeasurementId} not found for data processing", args.measurementID);
                            return;
                        }

                        // Create dataset entity
                        var dataset = new DatasetEntity
                        {
                            Id = args.measurementID,
                            Name = measurement.MeasurementName,
                            Description = "Device measurement data",
                            CreatedDate = measurement.MeasurementDate,
                            DataSource = DataSource.Device,
                            EntryMethod = EntryMethod.DeviceMeasurement,
                            DeviceId = measurement.Device.DeviceID
                        };

                        var datasetId = await dataService.CreateDatasetAsync(dataset);

                        // Convert string data to data points
                        var dataPoints = new List<DataPointEntity>();
                        for (int i = 0; i < args.data.Count; i++)
                        {
                            dataPoints.Add(new DataPointEntity
                            {
                                DatasetId = datasetId,
                                Timestamp = dataset.CreatedDate.AddSeconds(i),
                                ParameterName = "Value",
                                Value = args.data[i],
                                RowIndex = i
                            });
                        }

                        await dataService.AddDataPointsAsync(datasetId, dataPoints);

                        _logger.LogInformation("Saved measurement data for {MeasurementId} with {DataPointCount} data points",
                            args.measurementID, dataPoints.Count);

                        _logger.LogInformation("Unregistering completed measurement {MeasurementId}", args.measurementID);
                        await _registry.UnregisterMeasurementAsync(args.measurementID);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing measurement data for {MeasurementId}", args.measurementID);

                        // Ensure measurement is removed from registry even on error
                        try
                        {
                            await _registry.UnregisterMeasurementAsync(args.measurementID);
                        }
                        catch (Exception unregisterEx)
                        {
                            _logger.LogError(unregisterEx, "Failed to unregister measurement {MeasurementId} after error", args.measurementID);
                        }
                    }
                }
            });
        }

        public async Task<Guid> StartMeasurementAsync(Guid deviceId, string name, CancellationToken cancellationToken = default)
        {
            return await StartMeasurementAsync(deviceId, name, new Dictionary<string, object>(), cancellationToken);
        }

        public async Task<Guid> StartMeasurementAsync(Guid deviceId, string name, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        {
            try
            {
                var device = await _deviceController.GetDeviceAsync(deviceId);
                if (device == null)
                {
                    throw new ArgumentException($"Device with ID {deviceId} not found");
                }

                var measurement = _factory.CreateMeasurement(device);
                measurement.MeasurementName = name;
                measurement.MeasurementDate = DateTime.Now;
                measurement.DataAvailable += OnDataAvailable;

                // Set parameters if the measurement supports them
                if (measurement is ParameterizedDeviceMeasurement paramMeasurement)
                {
                    paramMeasurement.Parameters = parameters;
                }

                await _registry.RegisterMeasurementAsync(measurement);

                // Start the measurement
                _ = measurement.RunAsync(); // Fire and forget, but handle completion via event

                _logger.LogInformation("Started measurement on device {DeviceName} with ID {MeasurementId} and {ParameterCount} parameters",
                    device.DeviceName, measurement.MeasurementID, parameters.Count);

                return measurement.MeasurementID;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start measurement on device {DeviceId}", deviceId);
                throw;
            }
        }

        public async Task<IMeasurement?> GetMeasurementAsync(Guid measurementID)
        {
            return await _registry.GetMeasurementAsync(measurementID);
        }

        public async Task<IEnumerable<IMeasurement>> GetRunningMeasurementsAsync()
        {
            return await _registry.GetAllMeasurementsAsync();
        }

        public async Task<List<MeasurementParameter>> GetDeviceParametersAsync(Guid deviceId, CancellationToken cancellationToken = default)
        {
            try
            {
                var device = await _deviceController.GetDeviceAsync(deviceId);
                if (device == null)
                {
                    throw new ArgumentException($"Device with ID {deviceId} not found");
                }

                // Check if device supports parameter discovery
                if (device is IParameterizedDevice paramDevice && paramDevice.SupportsParameterDiscovery)
                {
                    _logger.LogInformation("Getting parameters for device {DeviceName}", device.DeviceName);

                    // Use cached parameters if available
                    var parameters = await paramDevice.GetRequiredParametersAsync();
                    _logger.LogInformation("Retrieved {Count} parameters for device {DeviceName}",
                        parameters.Count, device.DeviceName);
                    return parameters;
                }

                _logger.LogInformation("Device {DeviceName} does not support parameter discovery", device.DeviceName);
                return new List<MeasurementParameter>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get parameters for device {DeviceId}", deviceId);
                throw;
            }
        }
    }
}
