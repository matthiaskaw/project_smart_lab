using SmartLab.Domains.Core.Services;
using SmartLab.Domains.Data.Interfaces;

namespace SmartLab.Domains.Data.Models{


    public class Dataset : IDataset{

        private string _datadirectory = SettingsService.Instance.GetSettingByKey(ESettings.DataSetDirectory);
        public Guid DatasetID { get; set; }
        public DateTime DatasetDate{ get; set; }
        public string DatasetName { get; set; } = "";
        public string DatasetDiscription { get; set; } = "";
        public string DatasetFilepath { get; set; } = "";

        public void SaveDataset(List<string> data)
        {

            string filename = $"{DatasetDate.ToString("yy-MM-dd-HH-mm-ss")}_{DatasetName}.txt";
            Logger.Instance.LogInfo($"Dataset.SaveDataset: {filename}");
            DatasetFilepath = Path.Combine(_datadirectory, filename);
            Logger.Instance.LogInfo($"Dataset.DatasetFilePath: {DatasetFilepath}");
            File.WriteAllLines(DatasetFilepath, data);
            Logger.Instance.LogInfo($"Dataset.DatasetFilePath: {DatasetFilepath}");
        }

        public async Task SaveDatasetFromUpload(IFormFile file)
        {

            Logger.Instance.LogInfo($"Dataset.SaveDatasetFromUpload: Saving manually added dataset");
            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            string _targetDir = SettingsService.Instance.GetSettingByKey(ESettings.DataSetDirectory);
            string safeFilename = $"{DatasetDate.ToString("yy-MM-dd-HH-mm-ss")}_{Path.GetRandomFileName()}_{ext}"; //Good practice with random filename
            var filePath = Path.Combine(_targetDir, safeFilename);
            //bad practice because filename on server matches uploaded filename
            using (var stream = System.IO.File.Create(filePath))
            {

                await file.CopyToAsync(stream);
            }
            DatasetFilepath = filePath;

        }

        public void DeleteDataset()
        {
            Logger.Instance.LogInfo($"Dataset.DeleteDataset: Deleting Data set {this.DatasetID}");
            // DatasetFilepath = $"{DatasetDate.ToString("yyyy-MM-dd-HH-mm-ss")}_{DatasetName}.txt";
            // string absfilename = Path.Combine(_datadirectory, DatasetFilepath);
            File.Delete(DatasetFilepath);
        }

        public void AppendToFile(List<string> data){

            DatasetFilepath = $"{DatasetDate.ToString("yyyy-MM-dd-HH-mm-ss")}_{DatasetName}.txt";
            string absfilename = Path.Combine(_datadirectory, DatasetFilepath);
            File.AppendAllLines(absfilename, data);
        }

        

    }
}