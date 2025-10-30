using System.IO.Pipes;
using SmartLab.Domains.Core.Services;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Metrics;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Principal;
using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Measurement.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SmartLab.Domains.Device.Models
{
    public class ProxyDevice : IDevice, IParameterizedDevice, IAsyncDisposable
    {
        private readonly IProxyDeviceCommunication _communication;
        private readonly IProxyDeviceProcessManager _processManager;
        private readonly ILogger<ProxyDevice> _logger;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _disposed;
        private bool _isInitialized;
        private List<MeasurementParameter>? _cachedParameters;
        private DateTime _parameterCacheTime;
        private readonly TimeSpan _parameterCacheTimeout = TimeSpan.FromMinutes(5);

        public string DeviceIdentifier { get; set; } = string.Empty;
        public Guid DeviceID { get; set; }
        public string DeviceExecutablePath { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        
        // IParameterizedDevice implementation
        public bool SupportsParameterDiscovery => true;

        public ProxyDevice(
            IProxyDeviceCommunication communication,
            IProxyDeviceProcessManager processManager,
            ILogger<ProxyDevice> logger)
        {
            _communication = communication ?? throw new ArgumentNullException(nameof(communication));

            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cancellationTokenSource = new CancellationTokenSource();
            
        }

        public async Task CancelAsync()
        {
            try
            {
                _logger.LogInformation("Cancelling ProxyDevice {DeviceId}", DeviceID);
                
                if (_communication.IsConnected)
                {
                    await _communication.SendCommandAsync("CANCEL", _cancellationTokenSource.Token);
                    
                    // Send FINISH command after CANCEL to properly terminate the external device
                    try
                    {
                        _logger.LogInformation("Sending FINISH command after CANCEL");
                        await _communication.SendCommandAsync("FINISH", _cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send FINISH command after CANCEL");
                    }
                }
                
                _cancellationTokenSource.Cancel();
                await Task.Delay(500); // Give time for cancellation to propagate
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cancellation of ProxyDevice {DeviceId}", DeviceID);
                throw;
            }
        }

        public async Task<List<string>> GetDataAsync()
        {
            // Backward compatibility - use structured data with defaults
            var structuredData = await GetStructuredDataAsync(new Dictionary<string, object>());
            return structuredData.RawData;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized && !_disposed)
            {
                _logger.LogInformation($"ProxyDevice {DeviceID} already initialized, skipping", DeviceID);
                return;
            }

            try
            {
                _logger.LogInformation("Initializing ProxyDevice {DeviceId}", DeviceID);
                
                if (string.IsNullOrWhiteSpace(DeviceExecutablePath))
                {
                    throw new InvalidOperationException("DeviceExecutablePath must be set before initialization");
                }

                // Recreate CancellationTokenSource if disposed
                if (_disposed || _cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = new CancellationTokenSource();
                    _disposed = false;
                }

                // STEP 1: Create named pipes FIRST (non-blocking)
                _logger.LogInformation("Step 1: Creating named pipes for device {DeviceId}", DeviceID);
                await _communication.CreatePipesAsync(DeviceID);

                // STEP 2: Ensure pipes are ready for connection (creates socket files on Linux)
                _logger.LogInformation("Step 2: Ensuring pipes are ready for connection");
                await _communication.EnsurePipeReadyAsync();

                // STEP 3: Start external process (client can now connect to pipes)
                _logger.LogInformation("Step 3: Starting external process for device {DeviceId}", DeviceID);
                await _processManager.StartProcessAsync(DeviceExecutablePath, DeviceID, _cancellationTokenSource.Token);

                // STEP 4: Wait for client to connect with timeout
                _logger.LogInformation("Step 4: Waiting for client connection to pipes");
                using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
                connectionCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 second timeout for connection

                try
                {
                    await _communication.WaitForConnectionAsync(connectionCts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"Client process failed to connect to named pipes within 30 seconds for device {DeviceID}");
                }

                // STEP 5: Protocol handshake
                _logger.LogInformation("Step 5: Starting protocol handshake");
                const int maxRetries = 10;
                for (int i = 0; i < maxRetries; i++)
                {
                    await _communication.SendCommandAsync("INITIALIZE", _cancellationTokenSource.Token);

                    var response = await _communication.ReceiveResponseAsync(_cancellationTokenSource.Token);

                    if (!String.IsNullOrEmpty(response))
                    {
                        _logger.LogInformation("ProxyDevice {DeviceId} initialized successfully", DeviceID);
                        _isInitialized = true;
                        return;
                    }

                    if (i == maxRetries - 1)
                    {
                        throw new InvalidOperationException($"Failed to initialize ProxyDevice after {maxRetries} attempts. Last response: {response}");
                    }

                    _logger.LogWarning("Unexpected response '{Response}' on attempt {Attempt}, retrying...", response, i + 1);
                    await Task.Delay(500, _cancellationTokenSource.Token); // Wait before retry
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize ProxyDevice {DeviceId}", DeviceID);
                _isInitialized = false;
                throw;
            }
        }
        
        public async Task<List<MeasurementParameter>> GetRequiredParametersAsync()
        {
            // Check cache first
            if (_cachedParameters != null && 
                DateTime.Now - _parameterCacheTime < _parameterCacheTimeout)
            {
                return _cachedParameters;
            }
            
            try
            {
                // Ensure device is initialized for parameter discovery
                if (!_isInitialized || !_communication.IsConnected || _disposed)
                {
                    await InitializeAsync();
                }
                
                await _communication.SendCommandAsync("GETPARAMETERS", _cancellationTokenSource.Token);
                var response = await _communication.ReceiveResponseAsync(_cancellationTokenSource.Token);
                
                if (response.StartsWith("PARAMS:"))
                {
                    var jsonData = response.Substring(7); // Remove "PARAMS:" prefix
                    var options = new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    _cachedParameters = System.Text.Json.JsonSerializer.Deserialize<List<MeasurementParameter>>(jsonData, options) 
                        ?? new List<MeasurementParameter>();
                    _parameterCacheTime = DateTime.Now;
                    
                    _logger.LogInformation("Retrieved {Count} parameters from external device", 
                        _cachedParameters.Count);
                    
                    return _cachedParameters;
                }
                else if (response.StartsWith("PARAMETERS "))
                {
                    var jsonData = response.Substring(11); // Remove "PARAMETERS " prefix
                    var options = new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    _cachedParameters = System.Text.Json.JsonSerializer.Deserialize<List<MeasurementParameter>>(jsonData, options) 
                        ?? new List<MeasurementParameter>();
                    _parameterCacheTime = DateTime.Now;
                    
                    _logger.LogInformation("Retrieved {Count} parameters from external device", 
                        _cachedParameters.Count);
                    
                    return _cachedParameters;
                }
                else if (response == "ERROR:UNSUPPORTED")
                {
                    _logger.LogInformation("External device does not support parameter discovery");
                    return new List<MeasurementParameter>(); // Empty list for legacy devices
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected response format: {response}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve parameters from external device");
                return new List<MeasurementParameter>(); // Fallback to no parameters
            }
        }
        
        public async Task<StructuredMeasurementData> GetStructuredDataAsync(Dictionary<string, object> parameters)
        {
            try
            {
                // Send parameters to external device
                if (parameters.Any())
                {
                    var parametersJson = System.Text.Json.JsonSerializer.Serialize(parameters);
                    await _communication.SendCommandAsync($"SETPARAMETERS:{parametersJson}", 
                        _cancellationTokenSource.Token);
                    
                    var paramResponse = await _communication.ReceiveResponseAsync(_cancellationTokenSource.Token);
                    if (paramResponse != "PARAMS_SET")
                    {
                        throw new InvalidOperationException($"Failed to set parameters: {paramResponse}");
                    }
                }
                
                // Check if device supports breakpoints
                bool useBreakpoints = parameters.ContainsKey("useBreakpoints") && 
                                    bool.TryParse(parameters["useBreakpoints"]?.ToString(), out bool bp) && bp;
                
                if (useBreakpoints)
                {
                    return await GetStructuredDataWithBreakpointsAsync(parameters);
                }
                else
                {
                    return await GetStructuredDataTraditionalAsync(parameters);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get structured data from external device");
                throw;
            }
        }
        
        private async Task<StructuredMeasurementData> GetStructuredDataTraditionalAsync(Dictionary<string, object> parameters)
        {
            // Request structured data
            _logger.LogInformation("Sending GETDATA_STRUCTURED command");
            await _communication.SendCommandAsync("GETDATA_STRUCTURED", _cancellationTokenSource.Token);
            var dataResponse = await _communication.ReceiveResponseAsync(_cancellationTokenSource.Token);
            
            _logger.LogInformation("Received data response: {Response}", dataResponse?.Substring(0, Math.Min(dataResponse.Length, 200)) + (dataResponse?.Length > 200 ? "..." : ""));
            
            StructuredMeasurementData result;
            
            if (dataResponse.StartsWith("DATA:"))
            {
                var jsonData = dataResponse.Substring(5); // Remove "DATA:" prefix
                _logger.LogInformation("Attempting to deserialize JSON data (length: {Length})", jsonData.Length);
                
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var structuredData = System.Text.Json.JsonSerializer.Deserialize<StructuredMeasurementData>(jsonData, options);
                
                if (structuredData != null)
                {
                    _logger.LogInformation("Successfully deserialized structured data with {DataCount} raw data points", structuredData.RawData?.Count ?? 0);
                    result = structuredData;
                }
                else
                {
                    _logger.LogWarning("Deserialized structured data was null");
                    result = await CreateFallbackData(parameters);
                }
            }
            else
            {
                _logger.LogWarning("Data response did not start with 'DATA:': {Response}", dataResponse?.Substring(0, Math.Min(dataResponse?.Length ?? 0, 100)));
                result = await CreateFallbackData(parameters);
            }

            // Note: FINISH command is sent during device disposal, not here
            // This allows the device to potentially be reused for multiple measurements

            return result;
        }
        
        private async Task<StructuredMeasurementData> GetStructuredDataWithBreakpointsAsync(Dictionary<string, object> parameters)
        {
            _logger.LogInformation("Starting measurement with breakpoint support");
            
            // Start measurement with breakpoint mode
            await _communication.SendCommandAsync("GETDATA_STRUCTURED_BREAKPOINTS", _cancellationTokenSource.Token);
            
            var accumulatedData = new List<string>();
            var lastParameters = parameters;
            var sequenceNumber = 0;
            bool isComplete = false;
            
            while (!isComplete && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var response = await _communication.ReceiveResponseAsync(_cancellationTokenSource.Token);
                _logger.LogInformation("Received breakpoint response: {Response}", 
                    response?.Substring(0, Math.Min(response?.Length ?? 0, 200)) + (response?.Length > 200 ? "..." : ""));
                
                if (response.StartsWith("BREAKPOINT_DATA:"))
                {
                    var jsonData = response.Substring(16); // Remove "BREAKPOINT_DATA:" prefix
                    
                    try
                    {
                        var options = new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };
                        var partialData = System.Text.Json.JsonSerializer.Deserialize<StructuredMeasurementData>(jsonData, options);
                        
                        if (partialData != null)
                        {
                            sequenceNumber = partialData.BreakpointSequence;
                            isComplete = partialData.IsComplete;
                            
                            // Accumulate the data
                            if (partialData.RawData != null)
                            {
                                accumulatedData.AddRange(partialData.RawData);
                                _logger.LogInformation("Received breakpoint data sequence {Sequence}: {Count} points, Complete: {IsComplete}", 
                                    sequenceNumber, partialData.RawData.Count, isComplete);
                            }
                            
                            // Store partial data immediately for safety
                            await StorePartialDataAsync(partialData, sequenceNumber);
                            
                            // Acknowledge receipt and request continuation
                            await _communication.SendCommandAsync("BREAKPOINT_ACK", _cancellationTokenSource.Token);
                            
                            if (!isComplete)
                            {
                                await _communication.SendCommandAsync("CONTINUE", _cancellationTokenSource.Token);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing breakpoint data sequence {Sequence}", sequenceNumber);
                        // Send error acknowledgment
                        await _communication.SendCommandAsync("BREAKPOINT_ERROR", _cancellationTokenSource.Token);
                    }
                }
                else if (response == "MEASUREMENT_COMPLETE")
                {
                    isComplete = true;
                    _logger.LogInformation("Measurement marked as complete by external device");
                }
                else if (response.StartsWith("ERROR:"))
                {
                    _logger.LogError("External device reported error: {Error}", response);
                    throw new InvalidOperationException($"External device error: {response}");
                }
                else
                {
                    _logger.LogWarning("Unexpected response during breakpoint measurement: {Response}", response);
                }
            }

            // Note: FINISH command is sent during device disposal, not here
            // This allows the device to potentially be reused for multiple measurements

            // Return consolidated data
            var finalResult = new StructuredMeasurementData
            {
                Parameters = lastParameters,
                RawData = accumulatedData,
                Timestamp = DateTime.Now,
                IsPartialData = false,
                BreakpointSequence = sequenceNumber,
                IsComplete = true
            };
            
            _logger.LogInformation("Breakpoint measurement completed with {TotalCount} total data points", accumulatedData.Count);
            return finalResult;
        }
        
        private async Task StorePartialDataAsync(StructuredMeasurementData partialData, int sequenceNumber)
        {
            try
            {
                // Create a backup file for this partial data
                var backupDir = Path.Combine(Path.GetTempPath(), "SmartLabBreakpoints", DeviceID.ToString());
                Directory.CreateDirectory(backupDir);
                
                var backupFile = Path.Combine(backupDir, $"breakpoint_{sequenceNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = System.Text.Json.JsonSerializer.Serialize(partialData, options);
                await File.WriteAllTextAsync(backupFile, json);
                
                _logger.LogInformation("Stored partial data backup to {BackupFile}", backupFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store partial data backup for sequence {Sequence}", sequenceNumber);
            }
        }
        
        private async Task<StructuredMeasurementData> CreateFallbackData(Dictionary<string, object> parameters)
        {
            // Fallback to legacy format
            _logger.LogInformation("Falling back to legacy GETDATA format");
            await _communication.SendCommandAsync("GETDATA", _cancellationTokenSource.Token);
            var legacyResponse = await _communication.ReceiveResponseAsync(_cancellationTokenSource.Token);
            _logger.LogInformation("Legacy response: {Response}", legacyResponse?.Substring(0, Math.Min(legacyResponse?.Length ?? 0, 200)));
            
            var rawData = legacyResponse.Split(';').ToList();
            
            _logger.LogInformation("Created fallback structured data with {DataCount} raw data points", rawData.Count);
            
            return new StructuredMeasurementData
            {
                Parameters = parameters,
                RawData = rawData,
                Timestamp = DateTime.Now
            };
        }
        
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            
            _logger.LogInformation("Disposing ProxyDevice {DeviceId}", DeviceID);
            
            try
            {
                _disposed = true;
                _isInitialized = false;
                
                // Send FINISH command before shutting down communication
                if (_communication.IsConnected)
                {
                    try
                    {
                        _logger.LogInformation("Sending FINISH command during disposal");
                        await _communication.SendCommandAsync("FINISH", _cancellationTokenSource.Token);
                        await Task.Delay(500); // Give the external process time to handle the FINISH command
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send FINISH command during disposal");
                    }
                }
                
                try
                {
                    _cancellationTokenSource?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // CancellationTokenSource already disposed, ignore
                }
                
                await _processManager.DisposeAsync();
                await _communication.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during ProxyDevice disposal");
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
            }
        }
    }
}