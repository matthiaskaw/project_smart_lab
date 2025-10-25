using SmartLab.Domains.Device.Interfaces;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;

namespace SmartLab.Domains.Device.Services
{
    public class NamedPipeCommunication : IProxyDeviceCommunication, IAsyncDisposable
    {
        private readonly ILogger<NamedPipeCommunication> _logger;
        private NamedPipeServerStream? _serverToClient;
        private NamedPipeServerStream? _clientToServer;
        private StreamWriter? _writer;
        private StreamReader? _reader;
        private bool _disposed;
        private Guid _deviceID;

        public NamedPipeCommunication(ILogger<NamedPipeCommunication> logger)
        {
            _logger = logger;
        }

        public bool IsConnected =>
            _serverToClient?.IsConnected == true &&
            _clientToServer?.IsConnected == true;

        public async Task InitializeAsync(Guid deviceID, CancellationToken cancellationToken = default)
        {
            // Backward compatibility: create pipes and wait for connection
            await CreatePipesAsync(deviceID);
            await WaitForConnectionAsync(cancellationToken);
        }

        public async Task CreatePipesAsync(Guid deviceID)
        {
            try
            {
                _deviceID = deviceID;
                _logger.LogInformation("Creating named pipes for device {DeviceId}", deviceID);

                // Clean up existing pipes if they exist
                InternalCleanup();

                // Create bidirectional pipes with multiple instances to handle reconnections
                _serverToClient = new NamedPipeServerStream(
                    $"serverToClient_{deviceID}",
                    PipeDirection.Out,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                _clientToServer = new NamedPipeServerStream(
                    $"clientToServer_{deviceID}",
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                _logger.LogInformation("Named pipes created successfully: serverToClient_{DeviceId} and clientToServer_{DeviceId}", deviceID, deviceID);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create named pipes");
                InternalCleanup();
                throw;
            }
        }

        public async Task WaitForConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_serverToClient == null || _clientToServer == null)
                {
                    throw new InvalidOperationException("Pipes must be created before waiting for connection");
                }

                _logger.LogInformation("Waiting for pipe connections from client...");

                // Wait for connections with timeout
                await _serverToClient.WaitForConnectionAsync(cancellationToken);
                _logger.LogInformation("Server-to-client pipe connected");

                await _clientToServer.WaitForConnectionAsync(cancellationToken);
                _logger.LogInformation("Client-to-server pipe connected");

                // Setup stream readers/writers
                _writer = new StreamWriter(_serverToClient) { AutoFlush = true };
                _reader = new StreamReader(_clientToServer);

                _logger.LogInformation("Named pipe communication established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish pipe connections");
                InternalCleanup();
                throw;
            }
        }

        public async Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            if (_writer == null)
                throw new InvalidOperationException("Communication not initialized");

            if (!IsConnected)
                throw new InvalidOperationException("Pipes are not connected");

            try
            {
                await _writer.WriteLineAsync(command);
                _logger.LogDebug("Sent command: {Command}", command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send command: {Command}", command);
                throw;
            }
        }

        public async Task<string> ReceiveResponseAsync(CancellationToken cancellationToken = default)
        {
            if (_reader == null)
                throw new InvalidOperationException("Communication not initialized");

            if (!IsConnected)
                throw new InvalidOperationException("Pipes are not connected");

            try
            {
                var response = await _reader.ReadLineAsync();
                if (response == null)
                {
                    throw new InvalidOperationException("Received null response from client");
                }

                _logger.LogDebug("Received response: {Response}", response);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to receive response");
                throw;
            }
        }

        private void InternalCleanup()
        {
            try
            {
                _writer?.Dispose();
                _reader?.Dispose();
                
                _serverToClient?.Close();
                _clientToServer?.Close();
                
                _serverToClient?.Dispose();
                _clientToServer?.Dispose();
                
                _writer = null;
                _reader = null;
                _serverToClient = null;
                _clientToServer = null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error during internal cleanup");
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _logger?.LogInformation("Disposing named pipe communication");
            
            InternalCleanup();

            await Task.CompletedTask;
        }
    }
}