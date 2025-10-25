using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Core.Services;
using SmartLab.Domains.Data.Interfaces;

namespace SmartLab.Domains.Measurement.Models
{
    public class ParameterizedDeviceMeasurement : DeviceMeasurement
    {
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        public ParameterizedDeviceMeasurement(IDevice device) : base(device)
        {
        }

        public override async Task RunAsync()
        {
            try
            {
                Logger.Instance.LogInfo($"ParameterizedDeviceMeasurement.RunAsync: Starting measurement {MeasurementName} with device {Device.DeviceName} and {Parameters.Count} parameters");

                await Device.InitializeAsync();

                if (IsCancelled)
                {
                    Logger.Instance.LogInfo($"ParameterizedDeviceMeasurement.RunAsync: Measurement {MeasurementName} was cancelled before data collection");
                    return;
                }

                List<string> data;

                // Check if device supports structured data with parameters
                if (Device is IParameterizedDevice paramDevice)
                {
                    Logger.Instance.LogInfo($"ParameterizedDeviceMeasurement.RunAsync: Getting structured data with parameters");
                    var structuredData = await paramDevice.GetStructuredDataAsync(Parameters);
                    data = structuredData.RawData;
                }
                else
                {
                    Logger.Instance.LogInfo($"ParameterizedDeviceMeasurement.RunAsync: Device doesn't support parameters, using standard data collection");
                    data = await Device.GetDataAsync();
                }

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
    }
}