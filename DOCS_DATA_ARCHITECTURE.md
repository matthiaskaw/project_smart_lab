# SmartLab Data Architecture Documentation

## Table of Contents
1. [Overview](#overview)
2. [Architecture Principles](#architecture-principles)
3. [Database Schema](#database-schema)
4. [Entity Models](#entity-models)
5. [Data Services](#data-services)
6. [Data Flow](#data-flow)
7. [CRUD Operations](#crud-operations)
8. [Data Import & Export](#data-import--export)
9. [Validation System](#validation-system)
10. [Integration with Devices & Measurements](#integration-with-devices--measurements)
11. [Performance Optimizations](#performance-optimizations)
12. [Usage Examples](#usage-examples)

---

## Overview

SmartLab uses a **SQLite-based data management system** powered by **Entity Framework Core 9.0**. All data is persisted in a single SQLite database file (`smartlab.db`), eliminating the previous JSON file-based approach.

### Key Features
- ✅ Centralized SQLite database for all data
- ✅ Entity Framework Core ORM for type-safe queries
- ✅ Async/await throughout for better performance
- ✅ Automatic migrations for schema versioning
- ✅ Optimized SQLite PRAGMA settings
- ✅ Comprehensive validation system
- ✅ CSV import/export capabilities
- ✅ Integration with device measurements
- ✅ Soft delete for device configurations
- ✅ Cascade delete for related entities

---

## Architecture Principles

### 1. **Repository Pattern**
Each domain has a repository that abstracts database access:
- `DeviceRepository` - Manages device configurations
- `DataService` - Manages datasets, data points, and validation

### 2. **Service Layer**
Business logic is encapsulated in service classes:
- `IDataService` - Core data operations
- `IDataImportService` - CSV/JSON import logic
- `IDataValidationService` - Data quality checks

### 3. **Entity Framework Core**
- **DbContext**: `SmartLabDbContext` manages database connection and operations
- **Migrations**: Automatic schema versioning with `dotnet ef migrations`
- **LINQ**: Type-safe queries with compile-time checking

### 4. **Dependency Injection**
All services are registered in `Program.cs` and injected where needed:
```csharp
builder.Services.AddDbContext<SmartLabDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<IDataService, DataService>();
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
```

---

## Database Schema

### Database Location
```
{DataSetDirectory}/../smartlab.db
```

### Tables

#### 1. **Datasets** (Main dataset metadata)
| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| Name | VARCHAR(255) | Dataset name |
| Description | VARCHAR(1000) | Optional description |
| CreatedDate | DATETIME | Creation timestamp |
| DataSource | INT | Source: Manual(0), Import(1), Device(2) |
| EntryMethod | INT | Entry method: WebForm(0), CsvUpload(1), DirectDevice(2), DeviceMeasurement(3) |
| DeviceId | GUID | Foreign key to DeviceConfigurations (nullable) |
| OriginalFilename | VARCHAR(500) | Original filename if imported |
| FilePath | VARCHAR(1000) | File path if applicable |

**Indexes:**
- `IX_Datasets_CreatedDate`
- `IX_Datasets_DataSource`
- `IX_Datasets_DeviceId`

#### 2. **DataPoints** (Individual measurement points)
| Column | Type | Description |
|--------|------|-------------|
| Id | BIGINT | Primary key (auto-increment) |
| DatasetId | GUID | Foreign key to Datasets |
| Timestamp | DATETIME | Measurement timestamp |
| ParameterName | VARCHAR(100) | Parameter/variable name |
| Value | TEXT | Measurement value (stored as string) |
| Unit | VARCHAR(50) | Unit of measurement (optional) |
| Notes | VARCHAR(500) | Additional notes (optional) |
| RowIndex | INT | Original row number for ordering |

**Indexes:**
- `IX_DataPoints_DatasetId_Timestamp`
- `IX_DataPoints_ParameterName`
- `IX_DataPoints_RowIndex`

**Relationships:**
- Foreign key to `Datasets` with **CASCADE DELETE**

#### 3. **ValidationErrors** (Data quality issues)
| Column | Type | Description |
|--------|------|-------------|
| Id | BIGINT | Primary key (auto-increment) |
| DatasetId | GUID | Foreign key to Datasets |
| ErrorType | VARCHAR(100) | Type of error |
| Message | TEXT | Error description |
| RowIndex | INT | Row number where error occurred |
| ParameterName | VARCHAR(100) | Parameter name (if applicable) |
| CreatedDate | DATETIME | When error was detected |

**Indexes:**
- `IX_ValidationErrors_DatasetId`
- `IX_ValidationErrors_ErrorType`

**Relationships:**
- Foreign key to `Datasets` with **CASCADE DELETE**

#### 4. **DeviceConfigurations** (Device settings)
| Column | Type | Description |
|--------|------|-------------|
| Id | GUID | Primary key |
| Name | VARCHAR(255) | Device name |
| DeviceType | VARCHAR(100) | Device type/identifier |
| Description | VARCHAR(1000) | Device description |
| IsActive | BOOLEAN | Soft delete flag |
| CreatedDate | DATETIME | Creation timestamp |
| ModifiedDate | DATETIME | Last modification timestamp |
| ConfigurationJson | TEXT | Full configuration as JSON |

**Indexes:**
- `IX_DeviceConfigurations_Name`
- `IX_DeviceConfigurations_DeviceType`
- `IX_DeviceConfigurations_IsActive`

---

## Entity Models

### Core Entity Classes

#### DatasetEntity
```csharp
public class DatasetEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreatedDate { get; set; }
    public DataSource DataSource { get; set; }
    public EntryMethod EntryMethod { get; set; }
    public Guid? DeviceId { get; set; }
    public string? OriginalFilename { get; set; }
    public string? FilePath { get; set; }

    // Navigation properties
    public virtual ICollection<DataPointEntity> DataPoints { get; set; }
    public virtual ICollection<ValidationErrorEntity> ValidationErrors { get; set; }
}
```

#### DataPointEntity
```csharp
public class DataPointEntity
{
    public long Id { get; set; }
    public Guid DatasetId { get; set; }
    public DateTime Timestamp { get; set; }
    public string ParameterName { get; set; }
    public string Value { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
    public int RowIndex { get; set; }

    // Navigation property
    public virtual DatasetEntity? Dataset { get; set; }
}
```

#### ValidationErrorEntity
```csharp
public class ValidationErrorEntity
{
    public long Id { get; set; }
    public Guid DatasetId { get; set; }
    public string ErrorType { get; set; }
    public string Message { get; set; }
    public int? RowIndex { get; set; }
    public string? ParameterName { get; set; }
    public DateTime CreatedDate { get; set; }

    // Navigation property
    public virtual DatasetEntity? Dataset { get; set; }
}
```

#### DeviceConfigurationEntity
```csharp
public class DeviceConfigurationEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string DeviceType { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string ConfigurationJson { get; set; }
}
```

### Enums

#### DataSource
```csharp
public enum DataSource
{
    Manual = 0,   // Manually entered data
    Import = 1,   // Imported from CSV/file
    Device = 2    // Captured from device
}
```

#### EntryMethod
```csharp
public enum EntryMethod
{
    WebForm = 0,           // Manual web form entry
    CsvUpload = 1,         // CSV file upload
    DirectDevice = 2,      // Direct device communication
    DeviceMeasurement = 3  // Automated measurement
}
```

---

## Data Services

### IDataService Interface

The main service for all dataset operations.

#### Dataset Operations
```csharp
Task<Guid> CreateDatasetAsync(DatasetEntity dataset);
Task<DatasetEntity?> GetDatasetAsync(Guid id);
Task<List<DatasetSummary>> GetDatasetSummariesAsync();
Task<bool> DeleteDatasetAsync(Guid id);
Task<bool> UpdateDatasetAsync(DatasetEntity dataset);
```

#### Data Point Operations
```csharp
Task<bool> AddDataPointsAsync(Guid datasetId, List<DataPointEntity> dataPoints);
Task<List<DataPointEntity>> GetDataPointsAsync(Guid datasetId);
Task<List<DataPointEntity>> GetDataPointsByParameterAsync(Guid datasetId, string parameterName);
```

#### Manual Entry Operations
```csharp
Task<Guid> CreateManualDatasetAsync(ManualDatasetRequest request);
Task<DataValidationResult> ValidateManualDataAsync(List<ManualDataPoint> dataPoints);
```

#### Import Operations
```csharp
Task<ImportPreview> PreviewImportAsync(Stream fileStream, ImportOptions options);
Task<Guid> ImportDatasetAsync(ImportRequest request);
Task<DataValidationResult> ValidateImportDataAsync(Stream fileStream, ImportOptions options);
```

#### Validation Operations
```csharp
Task<bool> AddValidationErrorsAsync(Guid datasetId, List<ValidationError> errors);
Task<List<ValidationErrorEntity>> GetValidationErrorsAsync(Guid datasetId);
```

### IDataImportService Interface

Handles file import operations.

```csharp
Task<ImportPreview> PreviewCsvAsync(Stream fileStream, ImportOptions options);
Task<List<ManualDataPoint>> ImportCsvAsync(Stream fileStream, ImportOptions options);
Task<ImportPreview> PreviewJsonAsync(Stream fileStream);
Task<List<ManualDataPoint>> ImportJsonAsync(Stream fileStream);
Task<DataValidationResult> ValidateDataAsync(List<ManualDataPoint> dataPoints);
```

### IDataValidationService Interface

Validates data quality.

```csharp
DataValidationResult ValidateDataPoints(List<ManualDataPoint> dataPoints);
List<ValidationError> ValidateTimestamps(List<ManualDataPoint> dataPoints);
List<ValidationError> ValidateNumericValues(List<ManualDataPoint> dataPoints);
List<ValidationError> ValidateDuplicates(List<ManualDataPoint> dataPoints);
List<ValidationError> ValidateParameterConsistency(List<ManualDataPoint> dataPoints);
```

### IDeviceRepository Interface

Manages device configurations.

```csharp
Task<IEnumerable<DeviceConfiguration>> GetAllAsync();
Task<DeviceConfiguration?> GetByIdAsync(Guid id);
Task SaveAsync(DeviceConfiguration config);
Task DeleteAsync(Guid id);  // Soft delete
Task SaveAllAsync(IEnumerable<DeviceConfiguration> configurations);
```

---

## Data Flow

### 1. Manual Data Entry Flow

```
User fills form (ManualEntry.cshtml)
         ↓
POST to ManualEntryModel.OnPostAsync()
         ↓
Create ImportRequest with file
         ↓
IDataService.ImportDatasetAsync()
         ↓
IDataImportService.ImportCsvAsync()
         ↓
Parse CSV → List<ManualDataPoint>
         ↓
IDataValidationService.ValidateDataPoints()
         ↓
Create DatasetEntity + DataPointEntities
         ↓
SmartLabDbContext.SaveChangesAsync()
         ↓
SQLite Database (smartlab.db)
```

### 2. Device Measurement Flow

```
User starts measurement (MeasurementIndex.cshtml)
         ↓
MeasurementController.StartMeasurementAsync()
         ↓
Create DeviceMeasurement → measurement.RunAsync()
         ↓
Device captures data
         ↓
OnDataAvailable event fires
         ↓
Create DatasetEntity (DataSource=Device)
         ↓
Convert List<string> → List<DataPointEntity>
         ↓
IDataService.CreateDatasetAsync()
         ↓
IDataService.AddDataPointsAsync()
         ↓
SQLite Database (smartlab.db)
```

### 3. Dataset Retrieval Flow

```
User visits Data page (DataIndex.cshtml)
         ↓
GET request → IndexDatasetsModel.OnGetAsync()
         ↓
IDataService.GetDatasetSummariesAsync()
         ↓
EF Core LINQ query:
  - Include DataPoints (Count)
  - Include ValidationErrors (Count)
  - Group by ParameterName for ParameterCount
         ↓
Return List<DatasetSummary>
         ↓
Render table with summaries
```

### 4. Device Configuration Flow

```
User creates device (DeviceForm.cshtml)
         ↓
POST to DeviceController
         ↓
IDeviceRepository.SaveAsync(config)
         ↓
Find existing DeviceConfigurationEntity by ID
         ↓
If exists: Update entity
If not: Create new entity
         ↓
Serialize full DeviceConfiguration to ConfigurationJson
         ↓
SmartLabDbContext.SaveChangesAsync()
         ↓
SQLite Database (smartlab.db)
```

---

## CRUD Operations

### Create Dataset

```csharp
// Manual entry
var request = new ManualDatasetRequest
{
    Name = "Temperature Study",
    Description = "Lab temperature over 24 hours",
    MeasurementDate = DateTime.Now,
    DataPoints = new List<ManualDataPoint>
    {
        new() { Timestamp = DateTime.Now, ParameterName = "Temp", Value = "25.3", Unit = "°C" }
    }
};

var datasetId = await _dataService.CreateManualDatasetAsync(request);
```

### Read Dataset

```csharp
// Get full dataset with data points
var dataset = await _dataService.GetDatasetAsync(datasetId);

// Get summary list
var summaries = await _dataService.GetDatasetSummariesAsync();

// Get specific parameter data
var tempData = await _dataService.GetDataPointsByParameterAsync(datasetId, "Temp");
```

### Update Dataset

```csharp
var dataset = await _dataService.GetDatasetAsync(datasetId);
dataset.Description = "Updated description";

await _dataService.UpdateDatasetAsync(dataset);
```

### Delete Dataset

```csharp
// Cascade deletes all DataPoints and ValidationErrors
var success = await _dataService.DeleteDatasetAsync(datasetId);
```

---

## Data Import & Export

### CSV Import Process

#### 1. Preview Import
```csharp
var stream = file.OpenReadStream();
var options = new ImportOptions
{
    HasHeader = true,
    Delimiter = ',',
    TimestampColumn = "Timestamp",
    TimestampFormat = "yyyy-MM-dd HH:mm:ss"
};

var preview = await _dataService.PreviewImportAsync(stream, options);
// Returns: Headers, SampleRows, TotalRows, DetectedParameters
```

#### 2. Import Data
```csharp
var request = new ImportRequest
{
    File = file,
    DatasetName = "Imported Dataset",
    Description = "Data from CSV file",
    Options = options
};

var datasetId = await _dataService.ImportDatasetAsync(request);
```

### CSV Format Example

```csv
Timestamp,Temperature,Humidity,Pressure
2024-01-15 10:00:00,25.3,60.2,1013.2
2024-01-15 10:01:00,25.4,60.1,1013.3
2024-01-15 10:02:00,25.5,60.0,1013.4
```

### Import Options

| Option | Type | Description |
|--------|------|-------------|
| HasHeader | bool | First row contains column names |
| Delimiter | char | Column separator (`,` or `\t`) |
| TimestampColumn | string | Name of timestamp column |
| TimestampFormat | string | DateTime format string |
| ColumnMapping | Dictionary | Map CSV columns to parameters |
| SkipEmptyRows | bool | Ignore empty rows |
| SkipRows | int | Skip first N rows |

---

## Validation System

### Validation Rules

#### 1. Timestamp Validation
- Must be valid DateTime
- Cannot be in future
- Must be chronological within dataset
- No duplicate timestamps for same parameter

#### 2. Numeric Value Validation
- Numeric parameters must parse correctly
- Range checks for known parameters
- Outlier detection

#### 3. Parameter Consistency
- All parameters must have consistent units
- Parameter names must be consistent across dataset

#### 4. Duplicate Detection
- Identify duplicate entries
- Check for identical timestamp+parameter+value combinations

### Validation Result

```csharp
public class DataValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; }
    public List<ValidationError> Warnings { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public List<string> DetectedParameters { get; set; }
}
```

### Validation Error Structure

```csharp
public class ValidationError
{
    public string ErrorType { get; set; }      // "InvalidTimestamp", "NumericValue", etc.
    public string Message { get; set; }        // Human-readable message
    public int? RowIndex { get; set; }         // Row number where error occurred
    public string? ParameterName { get; set; } // Affected parameter
    public string? Value { get; set; }         // Invalid value
}
```

---

## Integration with Devices & Measurements

### Device Configuration Storage

Device configurations are stored in two ways:
1. **Structured fields**: `Name`, `DeviceType`, `IsActive`, etc.
2. **JSON blob**: Full configuration in `ConfigurationJson` column

This allows:
- ✅ Queryable fields for searching/filtering
- ✅ Full configuration preservation
- ✅ Backward compatibility
- ✅ Flexible configuration schemas per device type

### Measurement Data Capture

When a device measurement completes:

```csharp
private async void OnDataAvailable(object? invoker, (Guid measurementID, List<string> data) args)
{
    // Create dataset
    var dataset = new DatasetEntity
    {
        Id = args.measurementID,
        Name = measurement.MeasurementName,
        DataSource = DataSource.Device,
        EntryMethod = EntryMethod.DeviceMeasurement,
        DeviceId = measurement.Device.DeviceID
    };

    await _dataService.CreateDatasetAsync(dataset);

    // Convert strings to data points
    var dataPoints = args.data.Select((value, index) => new DataPointEntity
    {
        DatasetId = dataset.Id,
        Timestamp = dataset.CreatedDate.AddSeconds(index),
        ParameterName = "Value",
        Value = value,
        RowIndex = index
    }).ToList();

    await _dataService.AddDataPointsAsync(dataset.Id, dataPoints);
}
```

### Device-Dataset Relationship

```
DeviceConfiguration (1) ──────> (Many) Dataset
                                    ↓
                                    ↓ (Many)
                                    ↓
                                DataPoint
```

A device can produce many datasets over time. Each dataset links back to its source device via `DeviceId`.

---

## Performance Optimizations

### 1. SQLite PRAGMA Settings

Configured in `SmartLabDbContext` constructor:

```csharp
PRAGMA journal_mode=WAL;       // Write-Ahead Logging for better concurrency
PRAGMA synchronous=NORMAL;     // Balance speed and safety
PRAGMA cache_size=-64000;      // 64MB cache
PRAGMA foreign_keys=ON;        // Enable referential integrity
PRAGMA temp_store=MEMORY;      // Store temp tables in memory
PRAGMA page_size=4096;         // Optimal page size
PRAGMA busy_timeout=5000;      // Wait 5 seconds on locks
```

### 2. Database Indexes

Strategic indexes on frequently queried columns:
- `IX_Datasets_CreatedDate` - Sorting by date
- `IX_DataPoints_DatasetId_Timestamp` - Time-series queries
- `IX_DataPoints_ParameterName` - Parameter filtering
- `IX_DeviceConfigurations_IsActive` - Active device queries

### 3. Query Optimization

```csharp
// Use AsNoTracking() for read-only queries
var devices = await _context.DeviceConfigurations
    .Where(d => d.IsActive)
    .AsNoTracking()
    .ToListAsync();

// Batch operations
_context.DataPoints.AddRange(dataPoints);
await _context.SaveChangesAsync(); // Single transaction
```

### 4. Bulk Insert Strategy

For large datasets:
```csharp
// Disable change tracking for bulk inserts
_context.ChangeTracker.AutoDetectChangesEnabled = false;

foreach (var chunk in dataPoints.Chunk(1000))
{
    _context.DataPoints.AddRange(chunk);
    await _context.SaveChangesAsync();
}

_context.ChangeTracker.AutoDetectChangesEnabled = true;
```

### 5. Connection Pooling

Entity Framework Core automatically manages connection pooling for SQLite.

---

## Usage Examples

### Example 1: Create and Query Dataset

```csharp
// Inject service
public class MyController
{
    private readonly IDataService _dataService;

    public MyController(IDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> CreateDataset()
    {
        // Create dataset
        var dataset = new DatasetEntity
        {
            Name = "Test Dataset",
            Description = "Example dataset",
            CreatedDate = DateTime.UtcNow,
            DataSource = DataSource.Manual,
            EntryMethod = EntryMethod.WebForm
        };

        var id = await _dataService.CreateDatasetAsync(dataset);

        // Add data points
        var points = new List<DataPointEntity>
        {
            new() { Timestamp = DateTime.Now, ParameterName = "Temp", Value = "25.5" },
            new() { Timestamp = DateTime.Now.AddMinutes(1), ParameterName = "Temp", Value = "25.7" }
        };

        await _dataService.AddDataPointsAsync(id, points);

        // Query data
        var tempData = await _dataService.GetDataPointsByParameterAsync(id, "Temp");

        return Ok(tempData);
    }
}
```

### Example 2: Import CSV File

```csharp
public async Task<IActionResult> ImportCsv(IFormFile file)
{
    var options = new ImportOptions
    {
        HasHeader = true,
        Delimiter = ',',
        TimestampColumn = "Timestamp",
        TimestampFormat = "yyyy-MM-dd HH:mm:ss"
    };

    var request = new ImportRequest
    {
        File = file,
        DatasetName = Path.GetFileNameWithoutExtension(file.FileName),
        Description = "Imported data",
        Options = options
    };

    var datasetId = await _dataService.ImportDatasetAsync(request);

    return RedirectToPage("/Data/DataIndex");
}
```

### Example 3: Device Configuration

```csharp
public class DeviceSetup
{
    private readonly IDeviceRepository _repo;

    public async Task CreateDevice()
    {
        var config = new DeviceConfiguration
        {
            DeviceID = Guid.NewGuid(),
            DeviceName = "Temperature Sensor A",
            DeviceIdentifier = "TEMP_SENSOR",
            DeviceExecutablePath = @"C:\Devices\TempSensor.exe",
            Properties = new Dictionary<string, string>
            {
                { "SamplingRate", "1000" },
                { "Port", "COM3" }
            }
        };

        await _repo.SaveAsync(config);
    }

    public async Task<IEnumerable<DeviceConfiguration>> GetActiveDevices()
    {
        return await _repo.GetAllAsync(); // Returns only IsActive = true
    }
}
```

### Example 4: Query Time-Series Data

```csharp
public async Task<List<DataPointEntity>> GetTemperatureHistory(
    Guid datasetId,
    DateTime start,
    DateTime end)
{
    var allPoints = await _dataService.GetDataPointsByParameterAsync(datasetId, "Temperature");

    return allPoints
        .Where(p => p.Timestamp >= start && p.Timestamp <= end)
        .OrderBy(p => p.Timestamp)
        .ToList();
}
```

### Example 5: Validation

```csharp
public async Task<DataValidationResult> ValidateImportedData(Stream fileStream)
{
    var options = new ImportOptions { HasHeader = true, Delimiter = ',' };

    var result = await _dataService.ValidateImportDataAsync(fileStream, options);

    if (!result.IsValid)
    {
        foreach (var error in result.Errors)
        {
            Console.WriteLine($"Error at row {error.RowIndex}: {error.Message}");
        }
    }

    return result;
}
```

---

## Migration Guide

### From JSON to SQLite (Already Completed)

The application has been fully migrated from JSON files to SQLite. Key changes:

1. **Removed**: `DataController` (JSON-based)
2. **Removed**: `Dataset.cs` (legacy file-based model)
3. **Removed**: `IDataset` interface
4. **Added**: `DatasetEntity`, `DataPointEntity`, `ValidationErrorEntity`
5. **Added**: `SmartLabDbContext` with EF Core
6. **Updated**: All pages and controllers to use `IDataService`

### Database Initialization

On first run, the application will:
1. Create `smartlab.db` in the data directory
2. Apply all EF Core migrations
3. Configure SQLite PRAGMA settings
4. Initialize empty tables

### Backup & Recovery

To backup your data:
```bash
# Simple file copy
copy smartlab.db smartlab.backup.db

# Or use SQLite dump
sqlite3 smartlab.db .dump > backup.sql
```

To restore:
```bash
copy smartlab.backup.db smartlab.db
```

---

## Best Practices

### 1. Always Use Services
❌ **Don't**:
```csharp
var context = new SmartLabDbContext(options);
var dataset = context.Datasets.Find(id);
```

✅ **Do**:
```csharp
var dataset = await _dataService.GetDatasetAsync(id);
```

### 2. Use Async/Await
❌ **Don't**:
```csharp
var datasets = _dataService.GetDatasetSummariesAsync().Result;
```

✅ **Do**:
```csharp
var datasets = await _dataService.GetDatasetSummariesAsync();
```

### 3. Validate Before Saving
```csharp
var validationResult = await _dataService.ValidateManualDataAsync(dataPoints);

if (validationResult.IsValid)
{
    await _dataService.CreateManualDatasetAsync(request);
}
else
{
    // Handle errors
}
```

### 4. Use Transactions for Complex Operations
```csharp
using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    await _context.Datasets.AddAsync(dataset);
    await _context.DataPoints.AddRangeAsync(dataPoints);
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### 5. Dispose Resources Properly
```csharp
// Services are scoped and disposed automatically by DI container
// Streams should be disposed
using var stream = file.OpenReadStream();
await _dataService.ImportDatasetAsync(request);
```

---

## Troubleshooting

### Issue: Database Locked
**Symptom**: `SqliteException: database is locked`

**Solution**:
- Check `busy_timeout` PRAGMA is set
- Ensure no long-running transactions
- Enable WAL mode (already configured)

### Issue: Foreign Key Constraint Failed
**Symptom**: `SqliteException: FOREIGN KEY constraint failed`

**Solution**:
- Ensure `PRAGMA foreign_keys=ON` (already configured)
- Verify related entities exist before creating relationships
- Check DeviceId exists in DeviceConfigurations before creating Dataset

### Issue: Migration Errors
**Symptom**: `Unable to create migration`

**Solution**:
```bash
# Remove last migration
dotnet ef migrations remove

# Create new migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update
```

### Issue: Performance Degradation
**Symptom**: Slow queries

**Solution**:
1. Check database size: `SELECT page_count * page_size AS size FROM pragma_page_count(), pragma_page_size();`
2. Analyze queries: Enable EF Core logging
3. Add indexes: Check execution plans
4. Use `AsNoTracking()` for read-only queries
5. Consider archival strategy for old data

---

## Future Enhancements

### Potential Improvements
1. **Data Archival** - Move old datasets to separate archive database
2. **Advanced Queries** - Add statistical analysis queries (avg, min, max, std dev)
3. **Real-time Updates** - SignalR integration for live data updates
4. **Export Formats** - Add Excel, JSON export
5. **Data Compression** - Compress old data points
6. **Measurement Templates** - Store measurement configurations in database
7. **User Management** - Add user tracking for datasets (CreatedBy, ModifiedBy)
8. **Audit Log** - Track all CRUD operations
9. **Data Visualization** - Built-in charting from database queries
10. **Backup Automation** - Scheduled automatic backups

---

## References

- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [SQLite PRAGMA Commands](https://www.sqlite.org/pragma.html)
- [SQLite Performance Tuning](https://www.sqlite.org/performance.html)
- [EF Core Migrations](https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/)

---

**Document Version**: 1.0
**Last Updated**: 2025-01-15
**Author**: SmartLab Development Team
