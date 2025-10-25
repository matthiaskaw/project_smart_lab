namespace SmartLab.Domains.Device.Interfaces
{
    public interface IProxyDeviceProcessManager : IAsyncDisposable
    {
        Task StartProcessAsync(string executablePath, Guid deviceId, CancellationToken cancellationToken = default);
        Task StopProcessAsync(CancellationToken cancellationToken = default);
        bool IsRunning { get; }
        int? ProcessId { get; }
    }
}