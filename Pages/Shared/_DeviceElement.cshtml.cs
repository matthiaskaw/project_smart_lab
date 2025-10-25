using Microsoft.AspNetCore.Mvc;

[BindProperties]
public class DeviceElement{
   
    public Guid DeviceID { get; set; } = new Guid();
    public string DeviceName {get; set;}="";
    public string DeviceExecutablePath {get; set;}="";
    public string ButtonText {get; set;} ="";
    
    public DeviceElement(){
        // Simplified for ProxyDevice-only architecture
    } 
   
}