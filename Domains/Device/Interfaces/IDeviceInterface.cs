using System.Net;
using System.ComponentModel;
using SmartLab.Domains.Measurement.Models;



//Make IDevice.GetData a async method
namespace SmartLab.Domains.Device.Interfaces
{


    public enum EDeviceJSON
    {

        DeviceID = 0,
        DeviceName = 1,
        DeviceIdentifier = 2,
        DeviceExecutablePath = 3

    }
    public interface IDevice
    {

        public Guid DeviceID { get; set; }
        public string DeviceExecutablePath { get; set; }
        public string DeviceName { get; set; }
        public string DeviceIdentifier { get; set; }

        public Task CancelAsync();
        public Task<StructuredMeasurementData> GetDataAsync();
        public Task<List<MeasurementParameter>> GetRequiredParametersAsync();
        // Task<StructuredMeasurementData> GetStructuredDataAsync(Dictionary<string, object> parameters);
        public Task SetRequiredParametersAsync(List<MeasurementParameter> parameters);
        public Task InitializeAsync();
    }
}

