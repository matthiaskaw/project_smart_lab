using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Models;
using Microsoft.Extensions.Logging;
using SmartLab.Domains.Core.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartLab.Domains.Device.Services
{
    public class DeviceRepository : IDeviceRepository
    {
        private readonly ILogger<DeviceRepository> _logger;
        private readonly string _devicesFilename;
        private readonly SemaphoreSlim _fileLock;

        public DeviceRepository(ILogger<DeviceRepository> logger)
        {
            _logger = logger;
            _devicesFilename = SettingsService.Instance.GetSettingByKey(ESettings.DeviceFilename);
            _fileLock = new SemaphoreSlim(1, 1);
        }

        public async Task<IEnumerable<DeviceConfiguration>> GetAllAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                if (!File.Exists(_devicesFilename))
                {
                    _logger.LogWarning("Device configuration file not found: {FileName}", _devicesFilename);
                    return Enumerable.Empty<DeviceConfiguration>();
                }

                var jsonString = await File.ReadAllTextAsync(_devicesFilename);
                if (string.IsNullOrWhiteSpace(jsonString) || jsonString.Trim() == "[]")
                {
                    return Enumerable.Empty<DeviceConfiguration>();
                }

                var options = new JsonSerializerOptions 
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = null, // Don't convert property names
                    Converters = { new JsonStringEnumConverter() }
                };
                
                var configurations = JsonSerializer.Deserialize<DeviceConfiguration[]>(jsonString, options);
                return configurations ?? Enumerable.Empty<DeviceConfiguration>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load device configurations from {FileName}", _devicesFilename);
                throw;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<DeviceConfiguration?> GetByIdAsync(Guid id)
        {
            var configurations = await GetAllAsync();
            return configurations.FirstOrDefault(c => c.DeviceID == id);
        }

        public async Task SaveAsync(DeviceConfiguration config)
        {
            await _fileLock.WaitAsync();
            try
            {
                var configurations = await GetAllInternalAsync();
                
                var existingIndex = configurations.FindIndex(c => c.DeviceID == config.DeviceID);
                if (existingIndex >= 0)
                {
                    configurations[existingIndex] = config;
                    _logger.LogInformation("Updated device configuration for {DeviceId}", config.DeviceID);
                }
                else
                {
                    configurations.Add(config);
                    _logger.LogInformation("Added new device configuration for {DeviceId}", config.DeviceID);
                }

                await SaveAllInternalAsync(configurations);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            await _fileLock.WaitAsync();
            try
            {
                var configurations = await GetAllInternalAsync();
                var removedCount = configurations.RemoveAll(c => c.DeviceID == id);
                
                if (removedCount > 0)
                {
                    await SaveAllInternalAsync(configurations);
                    _logger.LogInformation("Deleted device configuration for {DeviceId}", id);
                }
                else
                {
                    _logger.LogWarning("Device configuration not found for deletion: {DeviceId}", id);
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task SaveAllAsync(IEnumerable<DeviceConfiguration> configurations)
        {
            await _fileLock.WaitAsync();
            try
            {
                await SaveAllInternalAsync(configurations.ToList());
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task<List<DeviceConfiguration>> GetAllInternalAsync()
        {
            try
            {
                if (!File.Exists(_devicesFilename))
                {
                    _logger.LogInformation("Device configuration file does not exist, returning empty list");
                    return new List<DeviceConfiguration>();
                }

                var jsonString = await File.ReadAllTextAsync(_devicesFilename);
                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    return new List<DeviceConfiguration>();
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = null,
                    Converters = { new JsonStringEnumConverter() }
                };

                var configurations = JsonSerializer.Deserialize<DeviceConfiguration[]>(jsonString, options);
                return configurations?.ToList() ?? new List<DeviceConfiguration>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load device configurations from {FileName}", _devicesFilename);
                throw;
            }
        }

        private async Task SaveAllInternalAsync(List<DeviceConfiguration> configurations)
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = null, // Keep original property names for compatibility
                    Converters = { new JsonStringEnumConverter() }
                };
                
                var jsonString = JsonSerializer.Serialize(configurations.ToArray(), options);
                await File.WriteAllTextAsync(_devicesFilename, jsonString);
                
                _logger.LogInformation("Saved {Count} device configurations to {FileName}", 
                    configurations.Count, _devicesFilename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save device configurations to {FileName}", _devicesFilename);
                throw;
            }
        }
    }
}