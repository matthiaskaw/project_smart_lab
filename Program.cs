



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
using SmartLab.Domains.Data.Database;
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

builder.Services.AddDbContext<SmartLabDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Register services for dependency injection
builder.Services.AddSingleton<SmartLab.Domains.Core.Services.SettingsService>(SmartLab.Domains.Core.Services.SettingsService.Instance);

// Register device-related services
builder.Services.AddScoped<IDeviceFactory, DeviceFactory>();
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
builder.Services.AddScoped<IDeviceController, DeviceController>();

// Register platform helper for cross-platform named pipe support
builder.Services.AddSingleton<IPlatformHelper, PlatformHelper>();

// Register proxy device services as transient to avoid disposal issues during startup
builder.Services.AddTransient<IProxyDeviceCommunication, NamedPipeCommunication>();
builder.Services.AddTransient<IProxyDeviceProcessManager, ProxyDeviceProcessManager>();

// Register measurement services
builder.Services.AddSingleton<IMeasurementFactory, MeasurementFactory>();
builder.Services.AddSingleton<IMeasurementRegistry, MeasurementRegistry>();
builder.Services.AddSingleton<IMeasurementController, MeasurementController>();
builder.Services.AddSingleton<IConfiguredMeasurementService, ConfiguredMeasurementService>();

// Register data services
builder.Services.AddScoped<IDataService, DataService>();
builder.Services.AddScoped<IDataImportService, DataImportService>();
builder.Services.AddScoped<IDataValidationService, DataValidationService>();
builder.Services.AddScoped<IDataExportService, DataExportService>();

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

// Initialize database
await using (var scope = app.Services.CreateAsyncScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<SmartLabDbContext>();

        // Apply database migrations
        await dbContext.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations applied successfully");

        // Configure SQLite PRAGMA settings for optimal performance
        dbContext.ConfigureSqlitePragmas();
        app.Logger.LogInformation("SQLite PRAGMA settings configured");

        // Initialize device controller and load existing devices from database
        var deviceController = scope.ServiceProvider.GetRequiredService<IDeviceController>();
        if (deviceController is DeviceController dc)
        {
            await dc.LoadDevicesAsync();
            app.Logger.LogInformation("Successfully loaded existing devices from database");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to initialize database during startup");
        throw; // Fail fast if database initialization fails
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
//app.Urls.Add("http://localhost:5000"); //@ Claude: do not delete
app.Urls.Add("http://0.0.0.0:5000"); //@Claude: do not delete
app.Run();

