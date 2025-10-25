using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Core.Services;
using SmartLab.Domains.Data.Interfaces;

namespace SmartLab.Domains.Measurement.Models
{
    public class DeviceMeasurement : IMeasurement
    {
        private bool _isCancelled = false;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public Guid MeasurementID { get; }
        public DateTime MeasurementDate { get; set; }
        public string MeasurementName { get; set; } = string.Empty;
        public bool IsCancelled => _isCancelled;
        public IDevice Device { get; }
        private IDataController _dataController;
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

        public virtual async Task RunAsync()
        {
            try
            {
                Logger.Instance.LogInfo($"DeviceMeasurement.RunAsync: Starting measurement {MeasurementName} with device {Device.DeviceName}");

                await Device.InitializeAsync();

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Logger.Instance.LogInfo($"DeviceMeasurement.RunAsync: Measurement {MeasurementName} was cancelled before data collection");
                    return;
                }

                var data = await Device.GetDataAsync();

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Logger.Instance.LogInfo($"DeviceMeasurement.RunAsync: Measurement {MeasurementName} completed with {data.Count} data points");
                    OnDataAvailable(data);
                }
                else
                {
                    Logger.Instance.LogInfo($"DeviceMeasurement.RunAsync: Measurement {MeasurementName} was cancelled during data collection");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"DeviceMeasurement.RunAsync: Error in measurement {MeasurementName}: {ex.Message}");
                throw;
            }
            finally
            {
                // Clean up device resources (processes, pipes, etc.)
                if (Device is IAsyncDisposable disposableDevice)
                {
                    try
                    {
                        Logger.Instance.LogInfo($"DeviceMeasurement.RunAsync: Disposing device {Device.DeviceName} after measurement");
                        await disposableDevice.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogError($"DeviceMeasurement.RunAsync: Error disposing device {Device.DeviceName}: {ex.Message}");
                    }
                }
            }
        }

        public async Task Cancel()
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