using CsvHelper;
using CsvHelper.Configuration;
using SmartLab.Domains.Data.Interfaces;
using SmartLab.Domains.Data.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace SmartLab.Domains.Data.Services
{
    public class DataImportService : IDataImportService
    {
        private readonly IDataValidationService _validationService;
        private readonly ILogger<DataImportService> _logger;

        public DataImportService(
            IDataValidationService validationService,
            ILogger<DataImportService> logger)
        {
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ImportPreview> PreviewCsvAsync(Stream fileStream, ImportOptions options)
        {
            try
            {
                fileStream.Position = 0;
                
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = options.Delimiter.ToString(),
                    HasHeaderRecord = options.HasHeader,
                    BadDataFound = null,
                    MissingFieldFound = null
                };

                using var reader = new StreamReader(fileStream);
                using var csv = new CsvReader(reader, config);

                var preview = new ImportPreview();
                var sampleRows = new List<Dictionary<string, string>>();
                var headers = new List<string>();
                
                if (options.HasHeader)
                {
                    await csv.ReadAsync();
                    csv.ReadHeader();
                    headers = csv.HeaderRecord?.ToList() ?? new List<string>();
                    preview.Headers = headers;
                }
                else
                {
                    // Generate column names for headerless files
                    var firstRow = await csv.ReadAsync();
                    if (firstRow)
                    {
                        for (int i = 0; i < csv.Parser.Count; i++)
                        {
                            headers.Add($"Column{i + 1}");
                        }
                        preview.Headers = headers;
                        
                        // Add first row as sample data
                        var firstRowData = new Dictionary<string, string>();
                        for (int i = 0; i < csv.Parser.Count; i++)
                        {
                            firstRowData[headers[i]] = csv.Parser[i] ?? string.Empty;
                        }
                        sampleRows.Add(firstRowData);
                    }
                }

                // Read sample rows (up to 10)
                int rowCount = options.HasHeader ? 0 : 1;
                while (await csv.ReadAsync() && sampleRows.Count < 10)
                {
                    var row = new Dictionary<string, string>();
                    for (int i = 0; i < headers.Count && i < csv.Parser.Count; i++)
                    {
                        row[headers[i]] = csv.Parser[i] ?? string.Empty;
                    }
                    sampleRows.Add(row);
                    rowCount++;
                }

                // Count total rows
                while (await csv.ReadAsync())
                {
                    rowCount++;
                }

                preview.SampleRows = sampleRows;
                preview.TotalRows = rowCount;
                
                // Auto-detect timestamp column
                preview.DetectedTimestampColumn = DetectTimestampColumn(headers, sampleRows);
                
                // Auto-detect parameters (non-timestamp columns)
                preview.DetectedParameters = headers
                    .Where(h => h != preview.DetectedTimestampColumn)
                    .ToList();

                // Auto-detect timestamp format
                if (!string.IsNullOrEmpty(preview.DetectedTimestampColumn) && sampleRows.Any())
                {
                    var timestampSamples = sampleRows
                        .Select(row => row.GetValueOrDefault(preview.DetectedTimestampColumn, ""))
                        .Where(ts => !string.IsNullOrWhiteSpace(ts))
                        .Take(5)
                        .ToList();
                    
                    preview.DetectedTimestampFormat = DetectTimestampFormat(timestampSamples);
                }

                _logger.LogInformation("Preview generated for CSV file: {RowCount} rows, {ColumnCount} columns", 
                    preview.TotalRows, preview.Headers.Count);

                return preview;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to preview CSV file");
                throw;
            }
        }

        public async Task<List<ManualDataPoint>> ImportCsvAsync(Stream fileStream, ImportOptions options)
        {
            try
            {
                fileStream.Position = 0;
                
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = options.Delimiter.ToString(),
                    HasHeaderRecord = options.HasHeader,
                    BadDataFound = null,
                    MissingFieldFound = null
                };

                using var reader = new StreamReader(fileStream);
                using var csv = new CsvReader(reader, config);

                var dataPoints = new List<ManualDataPoint>();
                var headers = new List<string>();

                if (options.HasHeader)
                {
                    await csv.ReadAsync();
                    csv.ReadHeader();
                    headers = csv.HeaderRecord?.ToList() ?? new List<string>();
                }
                else
                {
                    // Generate column names for headerless files
                    var firstRow = await csv.ReadAsync();
                    if (firstRow)
                    {
                        for (int i = 0; i < csv.Parser.Count; i++)
                        {
                            headers.Add($"Column{i + 1}");
                        }
                        
                        // Process first row
                        ProcessCsvRow(csv, headers, options, dataPoints, 0);
                    }
                }

                // Skip rows if requested
                for (int i = 0; i < options.SkipRows; i++)
                {
                    await csv.ReadAsync();
                }

                int rowIndex = options.HasHeader ? 0 : 1;
                while (await csv.ReadAsync())
                {
                    if (options.SkipEmptyRows && IsEmptyRow(csv, headers.Count))
                    {
                        continue;
                    }

                    ProcessCsvRow(csv, headers, options, dataPoints, rowIndex);
                    rowIndex++;
                }

                _logger.LogInformation("Imported {DataPointCount} data points from CSV file", dataPoints.Count);
                return dataPoints;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import CSV file");
                throw;
            }
        }

        public async Task<ImportPreview> PreviewJsonAsync(Stream fileStream)
        {
            try
            {
                fileStream.Position = 0;
                using var reader = new StreamReader(fileStream);
                var jsonContent = await reader.ReadToEndAsync();
                
                var jsonData = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                
                var preview = new ImportPreview();
                
                if (jsonData.ValueKind == JsonValueKind.Array && jsonData.GetArrayLength() > 0)
                {
                    var firstElement = jsonData.EnumerateArray().First();
                    if (firstElement.ValueKind == JsonValueKind.Object)
                    {
                        preview.Headers = firstElement.EnumerateObject()
                            .Select(prop => prop.Name)
                            .ToList();
                    }
                    
                    // Sample first few elements
                    preview.SampleRows = jsonData.EnumerateArray()
                        .Take(10)
                        .Select(element => 
                        {
                            var row = new Dictionary<string, string>();
                            if (element.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in element.EnumerateObject())
                                {
                                    row[prop.Name] = prop.Value.ToString();
                                }
                            }
                            return row;
                        })
                        .ToList();
                    
                    preview.TotalRows = jsonData.GetArrayLength();
                }

                // Auto-detect timestamp column and parameters
                preview.DetectedTimestampColumn = DetectTimestampColumn(preview.Headers, preview.SampleRows);
                preview.DetectedParameters = preview.Headers
                    .Where(h => h != preview.DetectedTimestampColumn)
                    .ToList();

                return preview;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to preview JSON file");
                throw;
            }
        }

        public async Task<List<ManualDataPoint>> ImportJsonAsync(Stream fileStream)
        {
            try
            {
                fileStream.Position = 0;
                using var reader = new StreamReader(fileStream);
                var jsonContent = await reader.ReadToEndAsync();
                
                var jsonData = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                var dataPoints = new List<ManualDataPoint>();

                if (jsonData.ValueKind == JsonValueKind.Array)
                {
                    int rowIndex = 0;
                    foreach (var element in jsonData.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.Object)
                        {
                            DateTime? timestamp = null;
                            
                            foreach (var prop in element.EnumerateObject())
                            {
                                var value = prop.Value.ToString();
                                
                                // Try to detect timestamp
                                if (IsTimestampColumn(prop.Name) && DateTime.TryParse(value, out var parsedTimestamp))
                                {
                                    timestamp = parsedTimestamp;
                                }
                                else if (!IsTimestampColumn(prop.Name))
                                {
                                    dataPoints.Add(new ManualDataPoint
                                    {
                                        Timestamp = timestamp ?? DateTime.UtcNow.AddSeconds(rowIndex),
                                        ParameterName = prop.Name,
                                        Value = value,
                                        RowIndex = rowIndex
                                    });
                                }
                            }
                        }
                        rowIndex++;
                    }
                }

                _logger.LogInformation("Imported {DataPointCount} data points from JSON file", dataPoints.Count);
                return dataPoints;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import JSON file");
                throw;
            }
        }

        public async Task<DataValidationResult> ValidateDataAsync(List<ManualDataPoint> dataPoints)
        {
            return await Task.FromResult(_validationService.ValidateDataPoints(dataPoints));
        }

        private void ProcessCsvRow(CsvReader csv, List<string> headers, ImportOptions options, 
            List<ManualDataPoint> dataPoints, int rowIndex)
        {
            DateTime? rowTimestamp = null;
            
            // First pass: find timestamp
            for (int i = 0; i < headers.Count && i < csv.Parser.Count; i++)
            {
                var columnName = headers[i];
                var value = csv.Parser[i] ?? string.Empty;
                
                if (columnName.Equals(options.TimestampColumn, StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParseExact(value, options.TimestampFormat, 
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTimestamp))
                    {
                        rowTimestamp = parsedTimestamp;
                    }
                    else if (DateTime.TryParse(value, out parsedTimestamp))
                    {
                        rowTimestamp = parsedTimestamp;
                    }
                }
            }

            // Second pass: create data points for non-timestamp columns
            for (int i = 0; i < headers.Count && i < csv.Parser.Count; i++)
            {
                var columnName = headers[i];
                var value = csv.Parser[i] ?? string.Empty;
                
                if (string.IsNullOrWhiteSpace(value)) continue;
                
                // Skip timestamp columns
                if (columnName.Equals(options.TimestampColumn, StringComparison.OrdinalIgnoreCase))
                    continue;
                
                // Apply column mapping if specified
                var parameterName = options.ColumnMapping.GetValueOrDefault(columnName, columnName);
                
                dataPoints.Add(new ManualDataPoint
                {
                    Timestamp = rowTimestamp ?? DateTime.UtcNow.AddSeconds(rowIndex),
                    ParameterName = parameterName,
                    Value = value,
                    RowIndex = rowIndex
                });
            }
        }

        private bool IsEmptyRow(CsvReader csv, int expectedColumnCount)
        {
            for (int i = 0; i < expectedColumnCount && i < csv.Parser.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(csv.Parser[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private string? DetectTimestampColumn(List<string> headers, List<Dictionary<string, string>> sampleRows)
        {
            var timestampKeywords = new[] { "timestamp", "time", "date", "datetime", "created", "when" };
            
            // First try exact matches
            foreach (var keyword in timestampKeywords)
            {
                var match = headers.FirstOrDefault(h => 
                    h.Equals(keyword, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // Then try partial matches
            foreach (var keyword in timestampKeywords)
            {
                var match = headers.FirstOrDefault(h => 
                    h.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // Finally, check if any column contains timestamp-like data
            foreach (var header in headers)
            {
                if (sampleRows.Any(row => 
                {
                    var value = row.GetValueOrDefault(header, "");
                    return DateTime.TryParse(value, out _);
                }))
                {
                    return header;
                }
            }

            return null;
        }

        private bool IsTimestampColumn(string columnName)
        {
            var timestampKeywords = new[] { "timestamp", "time", "date", "datetime", "created", "when" };
            return timestampKeywords.Any(keyword => 
                columnName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private string DetectTimestampFormat(List<string> timestampSamples)
        {
            var commonFormats = new[]
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss.fff",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ssZ",
                "MM/dd/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm:ss",
                "yyyy-MM-dd",
                "MM/dd/yyyy",
                "dd/MM/yyyy",
                "HH:mm:ss"
            };

            foreach (var format in commonFormats)
            {
                if (timestampSamples.All(sample => 
                    DateTime.TryParseExact(sample, format, CultureInfo.InvariantCulture, 
                        DateTimeStyles.None, out _)))
                {
                    return format;
                }
            }

            return "yyyy-MM-dd HH:mm:ss"; // Default format
        }
    }
}