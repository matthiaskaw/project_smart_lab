using SmartLab.Domains.Device.Interfaces;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;

namespace SmartLab.Domains.Device.Services
{
    public class NamedPipeCommunication : IProxyDeviceCommunication, IAsyncDisposable
    {
        private readonly ILogger<NamedPipeCommunication> _logger;
        private readonly IPlatformHelper _platformHelper;
        private readonly ISocketFileTracker _socketFileTracker;
        private NamedPipeServerStream? _serverToClient;
        private NamedPipeServerStream? _clientToServer;
        private StreamWriter? _writer;
        private StreamReader? _reader;
        private bool _disposed;
        private Guid _deviceID;
        private string? _serverToClientPipeName;
        private string? _clientToServerPipeName;
        private Task? _serverToClientWaitTask;
        private Task? _clientToServerWaitTask;

        public NamedPipeCommunication(
            ILogger<NamedPipeCommunication> logger,
            IPlatformHelper platformHelper,
            ISocketFileTracker socketFileTracker)
        {
            _logger = logger;
            _platformHelper = platformHelper;
            _socketFileTracker = socketFileTracker;
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
                _serverToClientPipeName = $"serverToClient_{deviceID}";
                _clientToServerPipeName = $"clientToServer_{deviceID}";

                _logger.LogInformation("Creating named pipes for device {DeviceId} on platform {Platform}",
                    deviceID, _platformHelper.PlatformName);

                // Note: We do NOT clean up here anymore. Cleanup only happens:
                // 1. On app startup (stale files from crashes)
                // 2. On disposal (normal cleanup)

                // Create bidirectional pipes with multiple instances to handle reconnections
                // .NET automatically creates platform-specific pipes:
                // - Windows: \\.\pipe\{pipeName}
                // - Linux: /tmp/CoreFxPipe_{pipeName}
                _serverToClient = new NamedPipeServerStream(
                    _serverToClientPipeName,
                    PipeDirection.Out,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                _clientToServer = new NamedPipeServerStream(
                    _clientToServerPipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                _logger.LogInformation("Named pipes created successfully: {ServerToClient} and {ClientToServer}",
                    _serverToClientPipeName, _clientToServerPipeName);

                // Register socket file paths for cleanup tracking (Linux only, but safe to call on all platforms)
                var serverToClientSocketPath = _platformHelper.GetSocketFilePath(_serverToClientPipeName);
                var clientToServerSocketPath = _platformHelper.GetSocketFilePath(_clientToServerPipeName);

                if (serverToClientSocketPath != null)
                {
                    await _socketFileTracker.RegisterSocketFileAsync(serverToClientSocketPath);
                }
                if (clientToServerSocketPath != null)
                {
                    await _socketFileTracker.RegisterSocketFileAsync(clientToServerSocketPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create named pipes");
                InternalCleanup();
                throw;
            }
        }

        public async Task EnsurePipeReadyAsync()
        {
            if (_serverToClient == null || _clientToServer == null)
            {
                throw new InvalidOperationException("Pipes must be created before ensuring readiness");
            }

            // On Linux, we need to call WaitForConnectionAsync to create the socket file
            // On Windows, the pipe is ready immediately after creation
            if (_platformHelper.IsLinux || _platformHelper.IsMacOS)
            {
                _logger.LogInformation("Linux/macOS detected - starting WaitForConnectionAsync to create socket files");

                // Start waiting on both pipes in the background to create the socket files
                // On Linux, the socket files are created when WaitForConnectionAsync is called
                // Store the tasks so we can await them later in WaitForConnectionAsync
                _serverToClientWaitTask = _serverToClient.WaitForConnectionAsync();
                _clientToServerWaitTask = _clientToServer.WaitForConnectionAsync();

                // Give a small delay to ensure socket files are physically created on disk
                await Task.Delay(200);

                _logger.LogInformation("Socket files created and ready for client connection");
            }
            else
            {
                _logger.LogInformation("Windows detected - pipes are ready immediately after creation");
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

                // If we're on Linux and already started waiting (via EnsurePipeReadyAsync),
                // use those tasks. Otherwise, start waiting now.
                if (_serverToClientWaitTask != null && _clientToServerWaitTask != null)
                {
                    _logger.LogInformation("Using pre-started wait tasks (Linux socket file creation pattern)");

                    // Wait for both connections with cancellation support
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var serverTask = _serverToClientWaitTask;
                    var clientTask = _clientToServerWaitTask;

                    await Task.WhenAll(serverTask, clientTask).WaitAsync(cancellationToken);

                    _logger.LogInformation("Both pipes connected successfully");
                }
                else
                {
                    _logger.LogInformation("Starting fresh wait for connections (Windows pattern)");

                    // Wait for connections with timeout
                    await _serverToClient.WaitForConnectionAsync(cancellationToken);
                    _logger.LogInformation("Server-to-client pipe connected");

                    await _clientToServer.WaitForConnectionAsync(cancellationToken);
                    _logger.LogInformation("Client-to-server pipe connected");
                }

                // Setup stream readers/writers
                _writer = new StreamWriter(_serverToClient) { AutoFlush = true };
                _reader = new StreamReader(_clientToServer);

                // Clear the wait tasks
                _serverToClientWaitTask = null;
                _clientToServerWaitTask = null;

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

                // Unregister and clean up socket files on Linux (no-op on Windows)
                if (_serverToClientPipeName != null)
                {
                    var socketPath = _platformHelper.GetSocketFilePath(_serverToClientPipeName);
                    if (socketPath != null)
                    {
                        // Unregister from tracking file first
                        _socketFileTracker.UnregisterSocketFileAsync(socketPath).GetAwaiter().GetResult();
                    }
                    // Then delete the actual file
                    _platformHelper.CleanupSocketFile(_serverToClientPipeName);
                }
                if (_clientToServerPipeName != null)
                {
                    var socketPath = _platformHelper.GetSocketFilePath(_clientToServerPipeName);
                    if (socketPath != null)
                    {
                        // Unregister from tracking file first
                        _socketFileTracker.UnregisterSocketFileAsync(socketPath).GetAwaiter().GetResult();
                    }
                    // Then delete the actual file
                    _platformHelper.CleanupSocketFile(_clientToServerPipeName);
                }
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