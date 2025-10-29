!!! WORK IN PROGRESS !!! README WAS GENERATED WITH CLAUDE AI AND WAS NOT THOROUGHLY CHECKED!!!


# SmartLab - Laboratory Data Management System

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-blueviolet.svg)](https://docs.microsoft.com/en-us/aspnet/core/)
[![SQLite](https://img.shields.io/badge/SQLite-3.0-green.svg)](https://www.sqlite.org/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Overview

SmartLab is a comprehensive laboratory data management system designed to automate measurement processes, manage device communications, and provide robust data storage. The system supports both automated device measurements and manual data entry with a modern web-based interface. With this web-based interface, data and measurement handling can be done with any device like a lab computer, office computer, and even your smartphone.

## Features

### 🔬 **Device Management**
- **Proxy Device Support**: Communicate with external measurement devices via named pipes
- **Parameter Discovery**: Automatic detection of device parameters and capabilities
- **Multi-Device Support**: Manage multiple laboratory devices simultaneously
- **Device Configuration**: Store and manage device configurations in SQLite database

### 📊 **Measurement System**
- **Automated Measurements**: Run measurements with configurable parameters
- **Real-time Monitoring**: Track measurement progress and status
- **Background Processing**: Non-blocking measurement execution with proper disposal handling
- **Parameterized Measurements**: Support for device-specific measurement parameters

### 💾 **Data Management**
- **SQLite Database**: Robust data storage with Entity Framework Core
- **Manual Data Entry**: Upload and import CSV/TXT files
- **Data Validation**: Comprehensive validation with error reporting
- **Dataset Management**: Organize data into structured datasets with metadata

### 🌐 **Web Interface**
- **Responsive Design**: Modern Bootstrap-based UI
- **Real-time Updates**: Live measurement status and progress tracking
- **File Upload**: Drag-and-drop file upload with validation
- **Data Visualization**: Browse and analyze stored datasets

## Architecture

### Domain-Driven Design
The project follows Domain-Driven Design principles with clear separation of concerns:

```
├── Domains/
│   ├── Core/           # Core services and settings
│   ├── Device/         # Device management and communication
│   ├── Measurement/    # Measurement orchestration and control
│   └── Data/           # Data storage and validation
├── Pages/              # Razor Pages for web UI
└── wwwroot/           # Static web assets
```

### Key Components

#### **Device Layer**
- **ProxyDevice**: Handles communication with external processes via named pipes
- **DeviceFactory**: Creates device instances based on configuration
- **DeviceRegistry**: Manages active device instances
- **DeviceController**: Orchestrates device operations

#### **Measurement Layer**
- **MeasurementController**: Coordinates measurement lifecycle
- **MeasurementFactory**: Creates measurement instances
- **MeasurementRegistry**: Tracks active measurements
- **ParameterizedMeasurement**: Supports configurable measurements

#### **Data Layer**
- **DataService**: Handles database operations with Entity Framework
- **DataImportService**: Processes file uploads and imports
- **DataValidationService**: Validates imported data
- **SmartLabDbContext**: Entity Framework database context

## Installation

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQLite](https://www.sqlite.org/) (included with .NET)
- Windows OS (for named pipe communication)

### Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/your-username/project_smart_lab.git
   cd project_smart_lab
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Configure database**
   ```bash
   dotnet ef database update
   ```

4. **Run the application**
   ```bash
   dotnet run --launch-profile http
   ```

5. **Access the application**
   - Open your browser and navigate to `http://localhost:5000`

## Configuration

### Application Settings
The application uses a settings service to manage configuration:

- **Data Directory**: Location for storing datasets and database
- **Device Configurations**: Stored in SQLite database
- **Logging**: Configurable via appsettings.json

### Database Configuration
SQLite database is automatically created and configured with:
- **Write-Ahead Logging (WAL)** for better performance
- **Foreign key constraints** enabled
- **Optimized PRAGMA settings** for laboratory data workloads

## Usage

### Adding Devices
1. Navigate to the Device Management section
2. Click "Add New Device"
3. Configure device parameters:
   - **Device Name**: Descriptive name for the device
   - **Executable Path**: Path to the external device program
   - **Device Identifier**: Unique identifier for the device

### Running Measurements
1. Go to the Measurements section
2. Select a configured device
3. Click "Configure & Start"
4. Set measurement parameters (if supported)
5. Monitor progress in real-time

### Manual Data Entry
1. Navigate to Data Management
2. Click "Manual Entry"
3. Fill in dataset information:
   - **Dataset Name**: Descriptive name
   - **Dataset Date**: When the data was collected
   - **Description**: Optional description
4. Upload your CSV/TXT file
5. Review and confirm the import

### Data Analysis
- Browse stored datasets in the Data Index
- View dataset details and validation results
- Export data in various formats

## API Documentation

### Device Controller
```csharp
// Get all devices
GET /api/devices

// Create new device
POST /api/devices
{
    "deviceName": "string",
    "deviceExecutablePath": "string",
    "deviceIdentifier": "string"
}

// Get device parameters
GET /api/devices/{id}/parameters
```

### Measurement Controller
```csharp
// Start measurement
POST /api/measurements
{
    "deviceId": "guid",
    "name": "string",
    "parameters": {}
}

// Get measurement status
GET /api/measurements/{id}

// Cancel measurement
DELETE /api/measurements/{id}
```

## Development

### Project Structure
```
SmartLab/
├── Domains/
│   ├── Core/Services/          # Core application services
│   ├── Device/
│   │   ├── Controllers/        # Device orchestration
│   │   ├── Interfaces/         # Device abstractions
│   │   ├── Models/            # Device domain models
│   │   └── Services/          # Device implementation services
│   ├── Measurement/
│   │   ├── Controllers/        # Measurement orchestration
│   │   ├── Interfaces/         # Measurement abstractions
│   │   ├── Models/            # Measurement domain models
│   │   └── Services/          # Measurement services
│   └── Data/
│       ├── Database/          # Entity Framework context
│       ├── Interfaces/        # Data service interfaces
│       ├── Models/           # Data models and entities
│       └── Services/         # Data service implementations
├── Pages/                    # Razor Pages
├── wwwroot/                 # Static web assets
└── Program.cs              # Application entry point
```

### Key Design Patterns

#### **Dependency Injection**
The application uses .NET's built-in DI container with carefully managed service lifetimes:
- **Singleton**: Controllers, registries, factories (stateless or long-lived)
- **Scoped**: Data services, repositories (per-request database context)
- **Transient**: Communication services (per-device instances)

#### **Background Task Processing**
Measurements run as background tasks with proper resource management:
```csharp
// Fire-and-forget with proper scoping
_ = Task.Run(async () =>
{
    using var scope = _scopeFactory.CreateAsyncScope();
    var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
    // Process measurement data
});
```

#### **Proxy Pattern**
External devices are accessed through proxy objects that manage:
- Named pipe communication
- Process lifecycle
- Error handling and recovery
- Resource disposal

### Database Schema

#### **Core Entities**
- **DatasetEntity**: Represents a collection of measurement data
- **DataPointEntity**: Individual data measurements
- **DeviceConfigurationEntity**: Device setup and parameters
- **ValidationErrorEntity**: Data validation results

#### **Entity Relationships**
```sql
Dataset (1) ──────── (*) DataPoint
Dataset (1) ──────── (*) ValidationError
Device (1)  ──────── (*) Dataset
```

## Troubleshooting

### Common Issues

#### **ObjectDisposedException during measurements**
- **Cause**: Service disposal while background tasks are running
- **Solution**: Properly implemented with IServiceScopeFactory pattern

#### **Named pipe communication failures**
- **Cause**: External process not responding or terminated
- **Solution**: Comprehensive error handling and process monitoring

#### **Database lock issues**
- **Cause**: Concurrent access to SQLite database
- **Solution**: WAL mode enabled with proper connection management

#### **File upload validation errors**
- **Cause**: Incorrect file format or missing required data
- **Solution**: Detailed validation messages and format guidelines

### Performance Optimization

#### **Database Performance**
- SQLite with WAL mode for concurrent reads
- Proper indexing on frequently queried columns
- Bulk insert operations for large datasets

#### **Memory Management**
- Proper disposal of resources with `using` statements
- Background task scoping to prevent memory leaks
- Efficient file processing with streaming

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines
- Follow Domain-Driven Design principles
- Use dependency injection for all services
- Implement proper error handling and logging
- Write unit tests for business logic
- Use async/await for I/O operations

## Security Considerations

- **File Upload Validation**: Strict file type and size validation
- **Input Sanitization**: All user inputs are validated and sanitized
- **Process Isolation**: External devices run in separate processes
- **Database Security**: Parameterized queries prevent SQL injection

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/)
- Database powered by [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- UI components from [Bootstrap](https://getbootstrap.com/)
- Icons from [Font Awesome](https://fontawesome.com/)

## Support

For support and questions:
- Create an issue in the GitHub repository
- Check the [documentation](docs/) for detailed guides
- Review the [troubleshooting](#troubleshooting) section

---

**SmartLab** - Streamlining laboratory data management with modern web technology.
