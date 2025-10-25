using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Models;
using SmartLab.Domains.Data.Database;
using SmartLab.Domains.Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace SmartLab.Domains.Device.Services
{
    public class DeviceRepository : IDeviceRepository
    {
        private readonly SmartLabDbContext _context;
        private readonly ILogger<DeviceRepository> _logger;

        public DeviceRepository(SmartLabDbContext context, ILogger<DeviceRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<DeviceConfiguration>> GetAllAsync()
        {
            try
            {
                var entities = await _context.DeviceConfigurations
                    .Where(d => d.IsActive)
                    .OrderBy(d => d.Name)
                    .AsNoTracking()
                    .ToListAsync();

                return entities.Select(MapToDeviceConfiguration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load device configurations from database");
                throw;
            }
        }

        public async Task<DeviceConfiguration?> GetByIdAsync(Guid id)
        {
            try
            {
                var entity = await _context.DeviceConfigurations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == id);

                return entity != null ? MapToDeviceConfiguration(entity) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load device configuration {DeviceId} from database", id);
                throw;
            }
        }

        public async Task SaveAsync(DeviceConfiguration config)
        {
            try
            {
                var entity = await _context.DeviceConfigurations.FindAsync(config.DeviceID);

                if (entity != null)
                {
                    // Update existing
                    entity.Name = config.DeviceName ?? string.Empty;
                    entity.DeviceType = config.DeviceIdentifier ?? "Unknown";
                    entity.Description = $"Device: {config.DeviceName}";
                    entity.IsActive = true;
                    entity.ModifiedDate = DateTime.UtcNow;
                    entity.ConfigurationJson = JsonSerializer.Serialize(config);

                    _context.DeviceConfigurations.Update(entity);
                    _logger.LogInformation("Updated device configuration for {DeviceId}", config.DeviceID);
                }
                else
                {
                    // Create new
                    entity = new DeviceConfigurationEntity
                    {
                        Id = config.DeviceID,
                        Name = config.DeviceName ?? string.Empty,
                        DeviceType = config.DeviceIdentifier ?? "Unknown",
                        Description = $"Device: {config.DeviceName}",
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow,
                        ModifiedDate = DateTime.UtcNow,
                        ConfigurationJson = JsonSerializer.Serialize(config)
                    };

                    await _context.DeviceConfigurations.AddAsync(entity);
                    _logger.LogInformation("Added new device configuration for {DeviceId}", config.DeviceID);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save device configuration {DeviceId}", config.DeviceID);
                throw;
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            try
            {
                var entity = await _context.DeviceConfigurations.FindAsync(id);

                if (entity != null)
                {
                    // Soft delete by marking as inactive
                    entity.IsActive = false;
                    entity.ModifiedDate = DateTime.UtcNow;

                    _context.DeviceConfigurations.Update(entity);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Deleted (soft) device configuration for {DeviceId}", id);
                }
                else
                {
                    _logger.LogWarning("Device configuration not found for deletion: {DeviceId}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete device configuration {DeviceId}", id);
                throw;
            }
        }

        public async Task SaveAllAsync(IEnumerable<DeviceConfiguration> configurations)
        {
            try
            {
                foreach (var config in configurations)
                {
                    await SaveAsync(config);
                }

                _logger.LogInformation("Saved {Count} device configurations to database", configurations.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save multiple device configurations");
                throw;
            }
        }

        private DeviceConfiguration MapToDeviceConfiguration(DeviceConfigurationEntity entity)
        {
            try
            {
                // Try to deserialize from ConfigurationJson first
                if (!string.IsNullOrWhiteSpace(entity.ConfigurationJson) && entity.ConfigurationJson != "{}")
                {
                    var config = JsonSerializer.Deserialize<DeviceConfiguration>(entity.ConfigurationJson);
                    if (config != null)
                    {
                        return config;
                    }
                }

                // Fallback: create from basic fields
                return new DeviceConfiguration
                {
                    DeviceID = entity.Id,
                    DeviceName = entity.Name,
                    DeviceIdentifier = entity.DeviceType
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize device configuration {DeviceId}, using fallback", entity.Id);

                // Fallback
                return new DeviceConfiguration
                {
                    DeviceID = entity.Id,
                    DeviceName = entity.Name,
                    DeviceIdentifier = entity.DeviceType
                };
            }
        }
    }
}
