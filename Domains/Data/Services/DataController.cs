using System.Collections.Concurrent;
using System.Text.Json;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Data.Models;
using Microsoft.Extensions.Logging;
using SmartLab.Domains.Core.Services;

namespace SmartLab.Domains.Data.Services
{
    public class DataController : IDataController, IDisposable, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<Guid, IDataset> _datasets = new();
        private readonly string _dataCoreFilename;
        private readonly ILogger<DataController> _logger;
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private bool _disposed;
        public ConcurrentDictionary<Guid, IDataset> Datasets { get { return _datasets; } }
        public DataController(ILogger<DataController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataCoreFilename = SettingsService.Instance.GetSettingByKey(ESettings.DataCoreFile);
            
            // Load existing datasets on initialization
            _ = Task.Run(async () => await LoadDatasetsAsync());
        }

        public async Task AddDatasetAsync(IDataset dataset)
        {
            ArgumentNullException.ThrowIfNull(dataset);
            
            if (_datasets.TryAdd(dataset.DatasetID, dataset))
            {
                _logger.LogInformation("Added dataset {DatasetId} with name {DatasetName}", 
                    dataset.DatasetID, dataset.DatasetName);
            }
            else
            {
                _logger.LogWarning("Dataset {DatasetId} already exists", dataset.DatasetID);
            }
            
            await Task.CompletedTask;
        }


        public async Task DeleteDataAsync(Guid datasetID)
        {
            IDataset datasetToDelete;
            if (!_datasets.TryGetValue(datasetID, out datasetToDelete))
            {
                Logger.Instance.LogInfo($"DataController.DeleteDataAsync: dataset {datasetID} not found");
                return;

            }


            datasetToDelete.DeleteDataset();
            IDataset removedDataset;
            _datasets.TryRemove(datasetToDelete.DatasetID, out removedDataset);
            await WriteDatasetsAsync();


        }
        public async Task WriteDatasetsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                var datasetsArray = _datasets.Values.ToArray();
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var jsonString = JsonSerializer.Serialize(datasetsArray, options);
                await File.WriteAllTextAsync(_dataCoreFilename, jsonString);

                _logger.LogInformation("Saved {Count} datasets to {FileName}",
                    datasetsArray.Length, _dataCoreFilename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write datasets to {FileName}", _dataCoreFilename);
                throw;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<ConcurrentDictionary<Guid, IDataset>> GetAllDatasetsAsync()
        {
            return await Task.FromResult(_datasets);
        }

        private async Task LoadDatasetsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                if (!File.Exists(_dataCoreFilename))
                {
                    _logger.LogInformation("DataCore file does not exist, starting with empty dataset collection");
                    return;
                }

                var jsonString = await File.ReadAllTextAsync(_dataCoreFilename);
                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    _logger.LogInformation("DataCore file is empty, starting with empty dataset collection");
                    return;
                }

                var datasets = JsonSerializer.Deserialize<Dataset[]>(jsonString);
                if (datasets != null)
                {
                    _datasets.Clear();
                    foreach (var dataset in datasets)
                    {
                        _datasets.TryAdd(dataset.DatasetID, dataset);
                    }
                    
                    _logger.LogInformation("Loaded {Count} datasets from {FileName}", 
                        datasets.Length, _dataCoreFilename);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load datasets from {FileName}", _dataCoreFilename);
            }
            finally
            {
                _fileLock.Release();
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

            _logger?.LogInformation("Disposing DataController with {Count} datasets", _datasets.Count);

            try
            {
                // Save any pending datasets
                await WriteDatasetsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error saving datasets during disposal");
            }

            _fileLock?.Dispose();
            await Task.CompletedTask;
        }
    }
}