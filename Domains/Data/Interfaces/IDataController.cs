using SmartLab.Domains.Data.Models;
using System.Collections.Concurrent;
namespace SmartLab.Domains.Data.Interfaces
{
    public interface IDataController
    {
        Task AddDatasetAsync(IDataset dataset);
        Task DeleteDataAsync(Guid datasetID);
        Task WriteDatasetsAsync();
        Task<ConcurrentDictionary<Guid,IDataset>> GetAllDatasetsAsync();
    }
}