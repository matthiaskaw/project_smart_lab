using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Models;
using SmartLab.Domains.Data.Models;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Measurement.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartLab.Domains.Measurement.Controllers
{
    public class MeasurementController : IMeasurementController
    {
        private readonly IMeasurementRegistry _registry;
        private readonly IMeasurementFactory _factory;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<MeasurementController> _logger;

        public MeasurementController(
            IMeasurementRegistry registry,
            IMeasurementFactory factory,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<MeasurementController> logger)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CancelMeasurementAsync(Guid measurementID, CancellationToken cancellationToken = default)
        {
            try
            {
                IMeasurement measurement = await _registry.GetMeasurementAsync(measurementID);
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
                try
                {
                    // Create a new scope to get fresh instances of scoped services (DbContext, etc.)
                    await using (var scope = _serviceScopeFactory.CreateAsyncScope())
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

                            // Create dataset entity with raw data
                            // Store data exactly as device sent it - no transformation
                            var dataset = new DatasetEntity
                            {
                                Id = args.measurementID,
                                Name = measurement.MeasurementName,
                                Description = "Device measurement data",
                                CreatedDate = measurement.MeasurementDate,
                                DataSource = DataSource.Device,
                                EntryMethod = EntryMethod.DeviceMeasurement,
                                DeviceId = measurement.Device.DeviceID,
                                RawDataJson = JsonSerializer.Serialize(args.data) // Store raw data as-is
                            };

                            var datasetId = await dataService.CreateDatasetAsync(dataset);

                            _logger.LogInformation("Saved measurement data for {MeasurementId} with {DataPointCount} raw data entries",
                                args.measurementID, args.data.Count);

                            _logger.LogInformation("Unregistering completed measurement {MeasurementId}", args.measurementID);
                            await _registry.UnregisterMeasurementAsync(args.measurementID);
                        }
                        catch (ObjectDisposedException ex)
                        {
                            _logger.LogWarning(ex, "Service was disposed while processing measurement data for {MeasurementId}. This can happen during application shutdown.", args.measurementID);
                            
                            // Try to clean up measurement registry without database operations
                            try
                            {
                                await _registry.UnregisterMeasurementAsync(args.measurementID);
                            }
                            catch (Exception unregisterEx)
                            {
                                _logger.LogError(unregisterEx, "Failed to unregister measurement {MeasurementId} after disposal", args.measurementID);
                            }
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
                }
                catch (ObjectDisposedException ex)
                {
                    _logger.LogWarning(ex, "ServiceScopeFactory was disposed while processing measurement data for {MeasurementId}. This can happen during application shutdown.", args.measurementID);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Critical error in measurement data processing for {MeasurementId}", args.measurementID);
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
                DeviceConfiguration deviceConfig;
                IDevice device;
                
                // Get device configuration (minimal scope usage)
                await using (var scope = _serviceScopeFactory.CreateAsyncScope())
                {
                    var deviceRepository = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                    var deviceFactory = scope.ServiceProvider.GetRequiredService<IDeviceFactory>();

                    // Get device configuration from repository
                    deviceConfig = await deviceRepository.GetByIdAsync(deviceId);
                    if (deviceConfig == null)
                    {
                        throw new ArgumentException($"Device configuration with ID {deviceId} not found");
                    }

                    // Create a fresh device instance for this measurement
                    device = deviceFactory.CreateDevice(deviceConfig);
                }
                // Scope is disposed here, but device is now independent

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

                // Start the measurement - device lifecycle is now managed by measurement
                _ = measurement.RunAsync(); // Fire and forget, device disposal handled by measurement

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
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var deviceController = scope.ServiceProvider.GetRequiredService<IDeviceController>();

                // Get device from registry (reuse existing instance if available)
                var device = await deviceController.GetDeviceAsync(deviceId);
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
