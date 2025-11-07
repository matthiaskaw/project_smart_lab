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
                    await measurement.CancelAsync();
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
        public async Task<Guid> CreateMeasurementAsync(Guid deviceId, string name)
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
                measurement.DataAvailable += OnDataAvailable;//TEST

                await _registry.RegisterMeasurementAsync(measurement);
                _logger.LogInformation($"MeasurementController.CreateMeasurementAsync: Measurement id = {measurement.MeasurementID}");
                return measurement.MeasurementID;


            }
            catch (Exception e)
            {

                _logger.LogError($"MeasurementController.CreateMeasurementAsync: Exception {e}");
                return new Guid();
            }
        }

        public async Task<IMeasurement?> GetMeasurementAsync(Guid measurementID)
        {
            return await _registry.GetMeasurementAsync(measurementID);
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

        public async Task<Guid> StartMeasurementAsync(Guid measurementID, string name, CancellationToken cancellationToken = default)
        {

            IMeasurement measurement = await _registry.GetMeasurementAsync(measurementID);

            _ = measurement.RunAsync();
            return measurement.MeasurementID;

        }


        public async Task<IEnumerable<IMeasurement>> GetRunningMeasurementsAsync()
        {
            return await _registry.GetAllMeasurementsAsync();
        }

        public async Task<List<MeasurementParameter>> GetDeviceParametersAsync(Guid measurementID, CancellationToken cancellationToken = default)
        {

            IMeasurement measurement = await _registry.GetMeasurementAsync(measurementID);
            if (measurement == null) { throw new Exception("MeasurementController.GetMeasurementParameterAsync: measurement is null"); }

            return await measurement.Device.GetRequiredParametersAsync();

        }

        public async Task SetDeviceParametersAsync(Guid measurementID, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("SetDeviceParametersAsync called for measurement {MeasurementId} with {ParameterCount} parameters",
                measurementID, parameters.Count);

            // Log incoming parameters
            foreach (var kvp in parameters)
            {
                _logger.LogDebug("Incoming parameter: {Name} = {Value} (Type: {Type})",
                    kvp.Key, kvp.Value, kvp.Value?.GetType().Name ?? "null");
            }

            IMeasurement measurement = await _registry.GetMeasurementAsync(measurementID);
            if (measurement == null)
            {
                _logger.LogError("SetDeviceParametersAsync: measurement {MeasurementId} is null", measurementID);
                throw new Exception("MeasurementController.SetDeviceParametersAsync: measurement is null");
            }

            _logger.LogInformation("Retrieved measurement {MeasurementId}, Device: {DeviceName} ({DeviceId})",
                measurementID, measurement.Device.DeviceName, measurement.Device.DeviceID);

            // Get the required parameters template from the device
            var requiredParameters = await measurement.Device.GetRequiredParametersAsync();
            _logger.LogInformation("Device returned {RequiredParameterCount} required parameters", requiredParameters.Count);

            // Log required parameters before update
            foreach (var param in requiredParameters)
            {
                _logger.LogDebug("Required parameter BEFORE update: {Name} = {Value} (Type: {Type})",
                    param.Name, param.DefaultValue, param.Type);
            }

            // Update the values from the dictionary
            int updatedCount = 0;
            foreach (var param in requiredParameters)
            {
                if (parameters.ContainsKey(param.Name))
                {
                    var oldValue = param.DefaultValue;
                    param.DefaultValue = parameters[param.Name];
                    updatedCount++;
                    _logger.LogInformation("Updated parameter '{Name}': {OldValue} -> {NewValue}",
                        param.Name, oldValue, param.DefaultValue);
                }
                else
                {
                    _logger.LogWarning("Parameter '{Name}' not found in incoming parameters dictionary", param.Name);
                }
            }

            _logger.LogInformation("Updated {UpdatedCount} out of {TotalCount} parameters", updatedCount, requiredParameters.Count);

            // Log required parameters after update
            foreach (var param in requiredParameters)
            {
                _logger.LogDebug("Required parameter AFTER update: {Name} = {Value} (Type: {Type})",
                    param.Name, param.DefaultValue, param.Type);
            }

            // Set the parameters on the device
            _logger.LogInformation("Calling Device.SetRequiredParametersAsync with {ParameterCount} parameters", requiredParameters.Count);
            await measurement.Device.SetRequiredParametersAsync(requiredParameters);
            _logger.LogInformation("Successfully set parameters on device for measurement {MeasurementId}", measurementID);

        }
    
    }
}
