



using SmartLab.Domains.Core.Services;
using SmartLab.Domains.Device.Models;
using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Services;
using SmartLab.Domains.Device.Controllers;
using SmartLab.Domains.Measurement.Interfaces;
using SmartLab.Domains.Measurement.Services;
using SmartLab.Domains.Measurement.Controllers;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Data.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configure database
var settingsService = SmartLab.Domains.Core.Services.SettingsService.Instance;
var dataDirectory = settingsService.GetSettingByKey(SmartLab.Domains.Core.Services.ESettings.DataSetDirectory);
var dbPath = Path.Combine(Path.GetDirectoryName(dataDirectory) ?? "", "smartlab.db");


// Register services for dependency injection
builder.Services.AddSingleton<SmartLab.Domains.Core.Services.SettingsService>(SmartLab.Domains.Core.Services.SettingsService.Instance);

// Register device-related services
builder.Services.AddScoped<IDeviceFactory, DeviceFactory>();
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
builder.Services.AddScoped<IDeviceController, DeviceController>();

// Register proxy device services as transient to avoid disposal issues during startup
builder.Services.AddTransient<IProxyDeviceCommunication, NamedPipeCommunication>();
builder.Services.AddTransient<IProxyDeviceProcessManager, ProxyDeviceProcessManager>();

// Register measurement services
builder.Services.AddScoped<IMeasurementFactory, MeasurementFactory>();
builder.Services.AddSingleton<IMeasurementRegistry, MeasurementRegistry>();
builder.Services.AddScoped<IMeasurementController, MeasurementController>();
builder.Services.AddSingleton<IConfiguredMeasurementService, ConfiguredMeasurementService>();

builder.Services.AddSingleton<IDataController, SmartLab.Domains.Data.Services.DataController>();

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

// Initialize database and migrate data
var scope = app.Services.CreateScope();
try
{


    // Initialize device controller and load existing devices
    var deviceController = scope.ServiceProvider.GetRequiredService<IDeviceController>();
    if (deviceController is DeviceController dc)
    {
        await dc.LoadDevicesAsync();
        app.Logger.LogInformation("Successfully loaded existing devices");
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to initialize database or load existing data during startup");
}
finally
{
    if (scope is IAsyncDisposable asyncDisposable)
    {
        await asyncDisposable.DisposeAsync();
    }
    else
    {
        scope.Dispose();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();


app.MapRazorPages();
app.Urls.Add("http://0.0.0.0:5000");
app.Run();

