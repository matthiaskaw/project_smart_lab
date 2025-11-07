using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Core.Services;
using SmartLab.Domains.Data.Interfaces;

namespace SmartLab.Domains.Measurement.Models
{
    public class DeviceMeasurement : IMeasurement
    {
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        private bool _isCancelled = false;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public Guid MeasurementID { get; }
        public DateTime MeasurementDate { get; set; }
        public string MeasurementName { get; set; } = string.Empty;
        public string MeasurementDescription { get; set; } = string.Empty;
        public bool IsCancelled => _isCancelled;
        public IDevice Device { get; }
        public event EventHandler<(Guid measurementID, List<string> data)>? DataAvailable;

        protected virtual void OnDataAvailable(List<string> data)
        {
            DataAvailable?.Invoke(this, (MeasurementID, data));
        }

        public DeviceMeasurement(IDevice device)
        {

            Device = device ?? throw new ArgumentNullException(nameof(device));
            MeasurementID = Guid.NewGuid();
            MeasurementDate = DateTime.Now;
        }

        public async Task RunAsync()
        {
            try
            {
                Logger.Instance.LogInfo($"ParameterizedDeviceMeasurement.RunAsync: Starting measurement {MeasurementName} with device {Device.DeviceName} and {Parameters.Count} parameters");

                // await Device.InitializeAsync(); Initialization is done when measurement is created

                if (IsCancelled)
                {
                    Logger.Instance.LogInfo($"ParameterizedDeviceMeasurement.RunAsync: Measurement {MeasurementName} was cancelled before data collection");
                    return;
                }
                Logger.Instance.LogInfo($"DeviceMeasurement.RunAsync: Got parameters {Parameters}");
                List<string> data;
                StructuredMeasurementData structureddata = await Device.GetDataAsync();
                data = structureddata.RawData;
                // Check if device supports structured data with parameters
                // if (Device is IParameterizedDevice paramDevice)
                // {
                //     Logger.Instance.LogInfo($"ParameterizedDeviceMeasurement.RunAsync: Getting structured data with parameters");
                //     var structuredData = await paramDevice.GetStructuredDataAsync(Parameters);
                //     data = structuredData.RawData;
                // }
                // else
                // {
                //     Logger.Instance.LogInfo($"ParameterizedDeviceMeasurement.RunAsync: Device doesn't support parameters, using standard data collection");
                //     data = await Device.GetDataAsync();
                // }

                if (!IsCancelled)
                {
                    Logger.Instance.LogInfo($"ParameterizedDeviceMeasurement.RunAsync: Measurement {MeasurementName} completed with {data.Count} data points");
                    OnDataAvailable(data);
                }
                else
                {
                    Logger.Instance.LogInfo($"ParameterizedDeviceMeasurement.RunAsync: Measurement {MeasurementName} was cancelled during data collection");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"ParameterizedDeviceMeasurement.RunAsync: Error in measurement {MeasurementName}: {ex.Message}");
                throw;
            }
            finally
            {
                // Clean up device resources (processes, pipes, etc.)
                if (Device is IAsyncDisposable disposableDevice)
                {
                    try
                    {
                        Logger.Instance.LogInfo($"ParameterizedDeviceMeasurement.RunAsync: Disposing device {Device.DeviceName} after measurement");
                        await disposableDevice.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogError($"ParameterizedDeviceMeasurement.RunAsync: Error disposing device {Device.DeviceName}: {ex.Message}");
                    }
                }
            }
        }

        public async Task CancelAsync()
        {
            try
            {
                Logger.Instance.LogInfo($"DeviceMeasurement.Cancel: Cancelling measurement {MeasurementName}");
                _isCancelled = true;
                _cancellationTokenSource.Cancel();
                await Device.CancelAsync();

                // Clean up device resources after cancellation
                if (Device is IAsyncDisposable disposableDevice)
                {
                    try
                    {
                        Logger.Instance.LogInfo($"DeviceMeasurement.Cancel: Disposing device {Device.DeviceName} after cancellation");
                        await disposableDevice.DisposeAsync();
                    }
                    catch (Exception disposeEx)
                    {
                        Logger.Instance.LogError($"DeviceMeasurement.Cancel: Error disposing device {Device.DeviceName}: {disposeEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"DeviceMeasurement.Cancel: Error cancelling measurement {MeasurementName}: {ex.Message}");
                throw;
            }
        }

        public void End()
        {
            Logger.Instance.LogInfo($"DeviceMeasurement.End: Ending measurement {MeasurementName}");
            _isCancelled = true;
            _cancellationTokenSource.Cancel();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

    }
}