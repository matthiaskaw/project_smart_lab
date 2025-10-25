using Microsoft.Extensions.Logging;

using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;



namespace SmartLab.Domains.Core.Services{

    public class SettingsService{

        private static SettingsService _instance;
        
        private static string _defaultDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SmartLabData"); 
        private static string _defaultDataSetDirectory = Path.Combine(_defaultDataDirectory, "Datasets");
        private static string _defaultDataSetCoreFilename = Path.Combine(_defaultDataDirectory, "Datacore.json");
        private static string _defaultLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        private static string  _defaultSettingsPath =  Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
        private static string _defaultDeviceFilename = Path.Combine(_defaultSettingsPath,"devicesettings.json");
        private static string _defaultSettingsFileName = Path.Combine(_defaultSettingsPath, "generalsettings.json");
        private Dictionary<ESettings, string> _settings = new Dictionary<ESettings, string>();
        private static readonly object _lock = new object();
        
        public string  DefaultSettingsFilename {get{ return _defaultSettingsFileName;}}
        public string DefaultDeviceFilename {get{ return _defaultDeviceFilename;}}
        
        private SettingsService(){ 
            
        if(!Directory.Exists(_defaultLogPath)){

            SmartLab.Domains.Core.Services.Logger.Instance.LogInfo($"Service.SettingsService.SettingsService(): No log directory (Path = {_defaultLogPath}! Creating a new log directory!");
            Directory.CreateDirectory(_defaultLogPath);
            SmartLab.Domains.Core.Services.Logger.Instance.LogInfo($"Service.SettingsService.SettingsService(): New log directory (Path = {_defaultLogPath} created!");
            _settings.Add(ESettings.LoggingPath, _defaultLogPath);
        }
        else{
            _settings.Add(ESettings.LoggingPath, _defaultLogPath);
        }

        if(!Directory.Exists(_defaultSettingsPath)){

            SmartLab.Domains.Core.Services.Logger.Instance.LogInfo($"Service.SettingsService.SettingsService(): No settings directory (Path = {_defaultSettingsPath}! Creating a new settings directory!");

            Directory.CreateDirectory(_defaultSettingsPath);
            
            SmartLab.Domains.Core.Services.Logger.Instance.LogInfo($"Service.SettingsService.SettingsService(): New settings directory (Path = {_defaultSettingsPath} created!");
        }
        

        if(!Directory.Exists(_defaultDataDirectory)){

            SmartLab.Domains.Core.Services.Logger.Instance.LogInfo($"Srvice.SettingsService.SettingsService(): New data directory (Path = ${_defaultDataDirectory})");
            Directory.CreateDirectory(_defaultDataDirectory);
        }

        if(!Directory.Exists(_defaultDataSetDirectory)){
            SmartLab.Domains.Core.Services.Logger.Instance.LogInfo($"Srvice.SettingsService.SettingsService(): New data directory (Path = ${_defaultDataSetDirectory})");
            Directory.CreateDirectory(_defaultDataSetDirectory);
            _settings.Add(ESettings.DataSetDirectory, _defaultDataSetDirectory);
        }
        else{
            _settings.Add(ESettings.DataSetDirectory, _defaultDataSetDirectory);
        }

        if(!File.Exists(_defaultDataSetCoreFilename)){
            SmartLab.Domains.Core.Services.Logger.Instance.LogInfo($"Srvice.SettingsService.SettingsService(): New data directory (Path = ${_defaultDataDirectory})");
            var stream =File.Create(_defaultDataSetCoreFilename);
            stream.Close();
            File.WriteAllLines(_defaultDataSetCoreFilename, new List<string>(){"[]"});
            _settings.Add(ESettings.DataCoreFile, _defaultDataSetCoreFilename);
        }
        else{
            _settings.Add(ESettings.DataCoreFile, _defaultDataSetCoreFilename);
        }

        if(!File.Exists(_defaultDeviceFilename)){

            SmartLab.Domains.Core.Services.Logger.Instance.LogInfo($"Service.SettingsService.SettingsService(): No settings file (Path = {_defaultDeviceFilename}! Creating a new general settings file!");
            File.Create(_defaultDeviceFilename).Close();
            File.WriteAllText(_defaultDeviceFilename, "[]");
            SmartLab.Domains.Core.Services.Logger.Instance.LogInfo($"Service.SettingsService.SettingsService(): Created settings file (Path = {_defaultDeviceFilename}! !");
            _settings.Add(ESettings.DeviceFilename, _defaultDeviceFilename);
        }
        else{
            _settings.Add(ESettings.DeviceFilename, _defaultDeviceFilename);
        }

        if(!File.Exists(_defaultSettingsFileName)){
            SmartLab.Domains.Core.Services.Logger.Instance.LogInfo($"Service.SettingsService.SettingsService(): No settings file (Path = {_defaultSettingsFileName}! Creating a new general settings file!");

            File.Create(_defaultSettingsFileName).Close();
            SmartLab.Domains.Core.Services.Logger.Instance.LogInfo($"Service.SettingsService.SettingsService(): Created settings file (Path = {_defaultSettingsFileName}! !");
            _settings.Add(ESettings.SettingsFilename,_defaultSettingsFileName);
            SaveSettings() ;
        }
        else{
            _settings.Add(ESettings.SettingsFilename,_defaultSettingsFileName);
        
        }

        LoadSettings();
    }
    

        public static SettingsService Instance { 
            
            get{
            // Double-checked locking
                if (_instance == null)
                {
                        if (_instance == null)
                        {
                            _instance = new SettingsService();
                        }
                    
                }
                return _instance;
            }
        }
        public void SaveSettings(){

            var options = new JsonSerializerOptions{WriteIndented = true};
            string json = JsonSerializer.Serialize(_settings,options);
            File.WriteAllText(_defaultSettingsFileName,json);

        }
        public Dictionary<ESettings, string> LoadSettings(){
            string json = File.ReadAllText(_defaultSettingsFileName);
            return JsonSerializer.Deserialize<Dictionary<ESettings, string>>(json);
        }
        public string GetSettingByKey(ESettings eSettings){

            string val = null;
            Settings.TryGetValue(eSettings, out val);
            // Removed excessive logging - this method is called frequently by scoped services
            return val;
        }
        public Dictionary<ESettings, string> Settings { 
            get{
                return _settings;
                }
            }
    }
}