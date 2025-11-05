using System.Text.Json;
using SmartLab.Domains.Core.Services;
using SmartLab.Domains.Measurement.Models;

namespace SmartLab.Domains.Measurement.Services
{
    public interface IConfiguredMeasurementService
    {
        Task<IEnumerable<ConfiguredMeasurement>> GetAllAsync();
        Task<ConfiguredMeasurement?> GetByIdAsync(Guid id);
        Task AddAsync(ConfiguredMeasurement measurement);
        Task RemoveAsync(Guid id);
        Task SaveAsync();
    }

    public class ConfiguredMeasurementService : IConfiguredMeasurementService
    {
        private readonly List<ConfiguredMeasurement> _measurements = new();
        private readonly string _filePath;

        public ConfiguredMeasurementService()
        {
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
            _filePath = Path.Combine(settingsPath, "configured_measurements.json");
            LoadMeasurements();
        }

        public async Task<IEnumerable<ConfiguredMeasurement>> GetAllAsync()
        {
            await Task.CompletedTask;
            return _measurements.ToList();
        }

        public async Task<ConfiguredMeasurement?> GetByIdAsync(Guid id)
        {
            await Task.CompletedTask;
            return _measurements.FirstOrDefault(m => m.MeasurementConfigId == id);
        }

        public async Task AddAsync(ConfiguredMeasurement measurement)
        {
            _measurements.Add(measurement);
            await SaveAsync();
        }

        public async Task RemoveAsync(Guid id)
        {
            var measurement = _measurements.FirstOrDefault(m => m.MeasurementConfigId == id);
            if (measurement != null)
            {
                _measurements.Remove(measurement);
                await SaveAsync();
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_measurements, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"ConfiguredMeasurementService.SaveAsync: Error saving measurements: {ex.Message}");
            }
        }

        private void LoadMeasurements()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var measurements = JsonSerializer.Deserialize<List<ConfiguredMeasurement>>(json);
                    if (measurements != null)
                    {
                        _measurements.Clear();
                        _measurements.AddRange(measurements);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"ConfiguredMeasurementService.LoadMeasurements: Error loading measurements: {ex.Message}");
            }
        }
    }
}