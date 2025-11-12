using SmartLab.Domains.Analysis.Platform;
using SmartLab.Domains.Analysis.Services;
using SmartLab.Domains.Analysis.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace SmartLab.Testing
{
    /// <summary>
    /// Manual test harness for Analysis services.
    /// Run this to verify validation and script execution work correctly.
    /// </summary>
    public class TestAnalysisServices
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== SmartLab Analysis Services Test Harness ===\n");

            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Setup configuration
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Analysis:ScriptsDirectory"] = "analysis-scripts"
                }!)
                .Build();

            // Create services
            var platformHelper = new AnalysisPlatformHelper();
            var validationService = new ScriptValidationService(
                platformHelper,
                loggerFactory.CreateLogger<ScriptValidationService>(),
                config
            );
            var scriptExecutor = new PythonScriptExecutor(
                platformHelper,
                loggerFactory.CreateLogger<PythonScriptExecutor>()
            );

            // Display platform info
            Console.WriteLine($"Platform: {(platformHelper.IsWindows() ? "Windows" : platformHelper.IsLinux() ? "Linux" : "macOS")}");
            Console.WriteLine($"Python Command: {platformHelper.GetPythonCommand()}");
            Console.WriteLine($"Python Available: {scriptExecutor.IsInterpreterAvailable()}\n");

            if (!scriptExecutor.IsInterpreterAvailable())
            {
                Console.WriteLine("ERROR: Python interpreter not found!");
                Console.WriteLine("Please install Python 3.x and ensure it's in your PATH.");
                return;
            }

            // Run tests
            await TestValidatorScript(platformHelper, loggerFactory.CreateLogger("ValidatorTest"));
            await TestValidation(validationService);
            await TestScriptExecution(scriptExecutor, platformHelper);

            Console.WriteLine("\n=== All Tests Completed ===");
        }

        private static async Task TestValidatorScript(
            AnalysisPlatformHelper platformHelper,
            ILogger logger)
        {
            Console.WriteLine("\n--- Test 1: Validator Script Availability ---");

            var validatorPath = Path.Combine("analysis-scripts", "_validators", "validate_python.py");

            if (File.Exists(validatorPath))
            {
                Console.WriteLine($"✓ Validator script found: {validatorPath}");

                // Test running the validator
                var testCode = "import os\nos.system('echo test')";
                var tempFile = Path.GetTempFileName() + ".py";
                await File.WriteAllTextAsync(tempFile, testCode);

                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = platformHelper.GetPythonCommand(),
                        Arguments = $"\"{validatorPath}\" \"{tempFile}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = System.Diagnostics.Process.Start(psi);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        var output = await process.StandardOutput.ReadToEndAsync();
                        var error = await process.StandardError.ReadToEndAsync();

                        if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                        {
                            Console.WriteLine("✓ Validator script executes successfully");
                            Console.WriteLine($"  Sample output: {output.Substring(0, Math.Min(100, output.Length))}...");
                        }
                        else
                        {
                            Console.WriteLine($"✗ Validator failed: ExitCode={process.ExitCode}");
                            if (!string.IsNullOrWhiteSpace(error))
                                Console.WriteLine($"  Error: {error}");
                        }
                    }
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
            else
            {
                Console.WriteLine($"✗ Validator script NOT found at: {validatorPath}");
            }
        }

        private static async Task TestValidation(ScriptValidationService validationService)
        {
            Console.WriteLine("\n--- Test 2: Script Validation ---");

            var testCases = new[]
            {
                new {
                    Name = "Valid Script",
                    Path = "analysis-scripts/_test-scripts/test_valid_script.py",
                    ExpectedStatus = ValidationStatus.Passed
                },
                new {
                    Name = "Dangerous Script (blocked imports)",
                    Path = "analysis-scripts/_test-scripts/test_dangerous_script.py",
                    ExpectedStatus = ValidationStatus.Failed
                },
                new {
                    Name = "Syntax Error Script",
                    Path = "analysis-scripts/_test-scripts/test_syntax_error.py",
                    ExpectedStatus = ValidationStatus.Failed
                },
                new {
                    Name = "Incomplete Script (warnings)",
                    Path = "analysis-scripts/_test-scripts/test_incomplete_script.py",
                    ExpectedStatus = ValidationStatus.Warning
                }
            };

            foreach (var testCase in testCases)
            {
                Console.WriteLine($"\nTesting: {testCase.Name}");

                if (!File.Exists(testCase.Path))
                {
                    Console.WriteLine($"  ✗ Test script not found: {testCase.Path}");
                    continue;
                }

                var scriptContent = await File.ReadAllTextAsync(testCase.Path);
                var result = await validationService.ValidateScriptAsync(scriptContent, ScriptLanguage.Python);

                var statusSymbol = result.Status == testCase.ExpectedStatus ? "✓" : "✗";
                Console.WriteLine($"  {statusSymbol} Status: {result.Status} (expected: {testCase.ExpectedStatus})");

                if (result.Errors.Count > 0)
                {
                    Console.WriteLine($"  Errors ({result.Errors.Count}):");
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"    - [{error.Code}] {error.Message}");
                    }
                }

                if (result.Warnings.Count > 0)
                {
                    Console.WriteLine($"  Warnings ({result.Warnings.Count}):");
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"    - [{warning.Code}] {warning.Message}");
                    }
                }
            }
        }

        private static async Task TestScriptExecution(
            PythonScriptExecutor executor,
            AnalysisPlatformHelper platformHelper)
        {
            Console.WriteLine("\n--- Test 3: Script Execution ---");

            var scriptPath = "analysis-scripts/_test-scripts/test_valid_script.py";

            if (!File.Exists(scriptPath))
            {
                Console.WriteLine($"✗ Test script not found: {scriptPath}");
                return;
            }

            // Create test input data
            var inputData = new
            {
                datasetId = Guid.NewGuid().ToString(),
                datasetName = "Test Dataset",
                dataPoints = new[]
                {
                    new { timestamp = DateTime.Now.AddMinutes(-10).ToString("o"), value = 22.5 },
                    new { timestamp = DateTime.Now.AddMinutes(-8).ToString("o"), value = 23.1 },
                    new { timestamp = DateTime.Now.AddMinutes(-6).ToString("o"), value = 22.8 },
                    new { timestamp = DateTime.Now.AddMinutes(-4).ToString("o"), value = 23.5 },
                    new { timestamp = DateTime.Now.AddMinutes(-2).ToString("o"), value = 24.0 },
                    new { timestamp = DateTime.Now.ToString("o"), value = 23.7 }
                },
                parameters = new { }
            };

            var inputJson = JsonSerializer.Serialize(inputData);
            var outputDir = Path.Combine(Path.GetTempPath(), "smartlab-test-output");
            Directory.CreateDirectory(outputDir);

            Console.WriteLine($"Executing script: {scriptPath}");
            Console.WriteLine($"Output directory: {outputDir}");

            try
            {
                var result = await executor.ExecuteScriptAsync(
                    scriptPath,
                    inputJson,
                    outputDir,
                    new Dictionary<string, object>(),
                    timeoutSeconds: 30
                );

                Console.WriteLine($"\n{(result.Success ? "✓" : "✗")} Execution completed:");
                Console.WriteLine($"  Exit Code: {result.ExitCode}");
                Console.WriteLine($"  Duration: {result.ExecutionTimeMs}ms");

                if (!string.IsNullOrWhiteSpace(result.Output))
                {
                    Console.WriteLine($"  Output:\n{result.Output}");

                    // Try to parse output as JSON
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(result.Output);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("status", out var status))
                        {
                            Console.WriteLine($"\n  Script returned status: {status.GetString()}");
                        }

                        if (root.TryGetProperty("imagePath", out var imagePath))
                        {
                            var fullImagePath = Path.Combine(outputDir, imagePath.GetString()!);
                            if (File.Exists(fullImagePath))
                            {
                                var fileInfo = new FileInfo(fullImagePath);
                                Console.WriteLine($"  ✓ Generated image: {imagePath.GetString()} ({fileInfo.Length} bytes)");
                            }
                            else
                            {
                                Console.WriteLine($"  ✗ Image file not found: {fullImagePath}");
                            }
                        }

                        if (root.TryGetProperty("statistics", out var stats))
                        {
                            Console.WriteLine($"  Statistics: {stats.GetRawText()}");
                        }
                    }
                    catch (JsonException)
                    {
                        Console.WriteLine("  (Output is not valid JSON)");
                    }
                }

                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    Console.WriteLine($"  Stderr: {result.Error}");
                }
            }
            finally
            {
                Console.WriteLine($"\nTest output directory: {outputDir}");
                Console.WriteLine("(Check this directory for generated files)");
            }
        }
    }
}
