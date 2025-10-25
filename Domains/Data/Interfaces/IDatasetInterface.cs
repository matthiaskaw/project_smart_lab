namespace SmartLab.Domains.Data.Interfaces{

    public interface IDataset{

        public Guid DatasetID {get; set;}
        public DateTime DatasetDate {get; set;}
        public string DatasetName {get; set;}
        public string DatasetDiscription { get; set; }
        public string DatasetFilepath { get; set; }
        public void SaveDataset(List<string> data);
        public Task SaveDatasetFromUpload(IFormFile file);
        public void DeleteDataset();
        public void AppendToFile(List<string> data);
       
    }
}