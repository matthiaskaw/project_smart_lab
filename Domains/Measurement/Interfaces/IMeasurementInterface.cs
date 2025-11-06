using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Data.Interfaces;
namespace SmartLab.Domains.Measurement.Interfaces
{

    public interface IMeasurement
    {
        public Task CancelAsync();
        public void End();
        public event EventHandler<(Guid measurementID, List<string> data)> DataAvailable;
        public Guid MeasurementID { get; }
        public DateTime MeasurementDate { get; set; }
        string MeasurementName { get; set; }
        public Task RunAsync();
        public bool IsCancelled { get; }
        public IDevice Device { get; }
        public Dictionary<string, object> Parameters { get; set; }
    }
}