namespace SmartLab.Domains.Device.Interfaces
{
    public interface IProxyDeviceCommunication : IAsyncDisposable
    {
        Task InitializeAsync(Guid deviceID, CancellationToken cancellationToken = default);
        Task CreatePipesAsync(Guid deviceID);
        Task WaitForConnectionAsync(CancellationToken cancellationToken = default);
        Task SendCommandAsync(string command, CancellationToken cancellationToken = default);
        Task<string> ReceiveResponseAsync(CancellationToken cancellationToken = default);
        bool IsConnected { get; }
    }
}