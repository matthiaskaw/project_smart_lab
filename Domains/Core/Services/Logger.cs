using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace LabManager.Domains.Core.Services{


    public class Logger
{
    // Static instance of the logger using Lazy<T> for thread-safety and lazy initialization
    private static readonly Lazy<Logger> _instance = new Lazy<Logger>(() => new Logger());

    // Logger instance
    private readonly ILogger<Logger> _logger;

    // Private constructor to prevent external instantiation
    private Logger()
    {

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")
            .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs/log.txt"), rollingInterval: RollingInterval.Day, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")
            .CreateLogger();

            
            Serilog.Debugging.SelfLog.Enable(Console.Out);
            


        // Set up the logger here, could be done via DI or manually as shown below
        var serviceProvider = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information))
            .BuildServiceProvider();

        _logger = serviceProvider.GetRequiredService<ILogger<Logger>>();
    }

    // Public property to access the singleton instance
    public static Logger Instance => _instance.Value;

    // Example logging methods
    public void LogInfo(string message)
    {
        Log.Information($"INFO: {message}");
    }

    public void LogWarning(string message)
    {
        Log.Warning($"WARNING: {message}");
    }

    public void LogError(string message)
    {
        Log.Error($"ERROR: {message}");
    }
}
}