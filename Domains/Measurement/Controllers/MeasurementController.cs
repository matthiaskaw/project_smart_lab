using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Models;
using SmartLab.Domains.Data.Models;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Measurement.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace SmartLab.Domains.Measurement.Controllers
{
    public class MeasurementController : IMeasurementController
    {
        private readonly IMeasurementFactory _factory;
        private readonly IMeasurementRegistry _registry;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDeviceController _deviceController;
        private readonly IDeviceFactory _deviceFactory;
        private readonly ILogger<MeasurementController> _logger;

        public MeasurementController(
            IMeasurementFactory factory,
            IMeasurementRegistry registry, 
            IServiceScopeFactory scopeFactory,
            IDeviceController deviceController,
            IDeviceFactory deviceFactory,
            ILogger<MeasurementController> logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _deviceController = deviceController ?? throw new ArgumentNullException(nameof(deviceController));
            _deviceFactory = deviceFactory ?? throw new ArgumentNullException(nameof(deviceFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CancelMeasurementAsync(Guid measurementID, CancellationToken cancellationToken = default)
        {
            try
            {
                var measurement = await _registry.GetMeasurementAsync(measurementID);
                if (measurement != null)
                {
                    measurement.Cancel();
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
            // Fire and forget - handle in background task to avoid blocking
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateAsyncScope();
                var dataController = scope.ServiceProvider.GetRequiredService<IDataController>();
                
                try
                {
                    _logger.LogInformation("Measurement ended: {MeasurementId}", args.measurementID);

                    var measurement = await _registry.GetMeasurementAsync(args.measurementID);
                    if (measurement == null)
                    {
                        _logger.LogWarning("Measurement {MeasurementId} not found for data processing", args.measurementID);
                        return;
                    }

                    // Create and save dataset
                    IDataset dataset = new Dataset();
                    dataset.DatasetID = args.measurementID;
                    dataset.DatasetName = measurement.MeasurementName;
                    dataset.DatasetDate = DateTime.Now;
                    dataset.DatasetDiscription = "Dataset for testing";
                    dataset.SaveDataset(args.data);

                    await dataController.AddDatasetAsync(dataset);
                    await dataController.WriteDatasetsAsync();

                    _logger.LogInformation("Saved measurement data for {MeasurementId} with {DataPointCount} data points",
                        args.measurementID, args.data.Count);

                    _logger.LogInformation("Unregistering completed measurement {MeasurementId}", args.measurementID);
                    await _registry.UnregisterMeasurementAsync(args.measurementID);
                }
                catch (ObjectDisposedException ex)
                {
                    _logger.LogWarning(ex, "Service was disposed while processing measurement data for {MeasurementId}. This can happen during application shutdown.", args.measurementID);
                    
                    // Try to clean up measurement registry without data operations
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
                var registryDevice = await _deviceController.GetDeviceAsync(deviceId);
                if (registryDevice == null)
                {
                    throw new ArgumentException($"Device with ID {deviceId} not found");
                }

                // Create a fresh device instance for this measurement
                // This prevents disposal issues when multiple measurements run
                var deviceConfig = new DeviceConfiguration
                {
                    DeviceID = registryDevice.DeviceID,
                    DeviceName = registryDevice.DeviceName,
                    DeviceExecutablePath = registryDevice.DeviceExecutablePath ?? "",
                    DeviceIdentifier = registryDevice.DeviceIdentifier ?? ""
                };
                
                var measurementDevice = _deviceFactory.CreateDevice(deviceConfig);
                var measurement = _factory.CreateMeasurement(measurementDevice);
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
                    registryDevice.DeviceName, measurement.MeasurementID, parameters.Count);
                
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
