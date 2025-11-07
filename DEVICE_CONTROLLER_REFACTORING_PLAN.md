# DeviceController Refactoring Strategy

## Current State Analysis

### DeviceController Issues (DeviceController.cs:17-207)

**Current Responsibilities:**
- Device Lifecycle Management (CreateDeviceAsync, LoadDevicesAsync)
- Query Operations (GetDeviceAsync, GetAllDevicesAsync, GetDevicesByNameAsync, RequestDeviceAsync)
- Mutation Operations (RemoveDeviceAsync, UpdateDeviceAsync)

**Critical Problems:**
1. **Name is misleading** - "Controller" sounds like HTTP controller but it's actually a service
2. **LoadDevicesAsync not in interface** - Program.cs:100-102 casts to concrete type (code smell!)
3. **Duplicate methods** - RequestDeviceAsync vs GetDevicesByNameAsync do similar things
4. **No separation** between command operations (Create, Update, Delete) and query operations (Get)
5. **Mixed abstractions** - some methods add value, others are pass-throughs
6. **207 lines** doing too many things

### DeviceRegistry vs DeviceRepository - Are Both Needed?

**YES** - They serve fundamentally different purposes:

**DeviceRegistry (Singleton)**
- **Storage**: In-memory ConcurrentDictionary<Guid, IDevice>
- **Purpose**: Runtime IDevice instances (active, running devices)
- **Lifecycle**: Application lifetime
- **Analogy**: People currently online in a chat app

**DeviceRepository (Scoped)**
- **Storage**: Database (Entity Framework + SQLite)
- **Purpose**: DeviceConfiguration objects (device settings/metadata)
- **Lifecycle**: Per-request/scope
- **Analogy**: Contacts list saved on your phone

**Current Usage Pattern:**
```
CreateDevice:    Config → [Factory] → Device → [Repository.Save] + [Registry.Register]
LoadDevices:     [Repository.GetAll] → Configs → [Factory] → Devices → [Registry.Register]
GetDevice:       [Registry.GetDevice] (runtime lookup)
RemoveDevice:    [Registry.Unregister] + [Repository.Delete]
```

---

## Refactoring Options

### Option A: Minimal Refactoring (Conservative)

**Changes:**
1. Rename: `IDeviceController` → `IDeviceManager`
2. Add missing method to interface: `Task LoadDevicesAsync()`
3. Remove duplicate: Delete `RequestDeviceAsync()` (use `GetDevicesByNameAsync()` instead)
4. Extract initialization logic: Create `IDeviceInitializer` for LoadDevicesAsync

**New Structure:**
```
IDeviceManager (renamed from IDeviceController)
├── CreateDeviceAsync()
├── GetDeviceAsync()
├── GetAllDevicesAsync()
├── GetDevicesByNameAsync()
├── RemoveDeviceAsync()
└── UpdateDeviceAsync()

IDeviceInitializer (new)
└── LoadDevicesAsync()
```

**Pros:** Simple, low risk, quick to implement
**Cons:** Still a fat service, doesn't solve fundamental design issues

---

### Option B: Service Layer Split (RECOMMENDED)

**Goal:** Separate commands, queries, and lifecycle management

#### New Architecture

```
┌─────────────────────────────────────────────┐
│         Application Layer (Pages)           │
└─────────────────────────────────────────────┘
                    │
        ┌───────────┴───────────┐
        │                       │
┌───────▼─────────┐    ┌───────▼──────────┐
│ IDeviceService  │    │ IDeviceQueries   │
│  (Commands)     │    │   (Read-only)    │
└─────────────────┘    └──────────────────┘
        │                       │
        └───────────┬───────────┘
                    │
        ┌───────────▼───────────┐
        │ IDeviceLifecycleManager│
        │  (Internal coordination)│
        └─────────────────────────┘
                    │
        ┌───────────┴──────────┐
        │                      │
┌───────▼────────┐    ┌───────▼────────┐
│ IDeviceRegistry│    │IDeviceRepository│
│  (In-memory)   │    │  (Database)     │
└────────────────┘    └─────────────────┘
```

#### Interface Definitions

**IDeviceService (Command operations - mutate state)**
```csharp
namespace SmartLab.Domains.Device.Interfaces
{
    public interface IDeviceService
    {
        Task<IDevice> CreateDeviceAsync(DeviceConfiguration config);
        Task RemoveDeviceAsync(Guid id);
        Task UpdateDeviceAsync(IDevice device);
    }
}
```

**IDeviceQueries (Query operations - read-only)**
```csharp
namespace SmartLab.Domains.Device.Interfaces
{
    public interface IDeviceQueries
    {
        Task<IDevice?> GetDeviceAsync(Guid id);
        Task<IEnumerable<IDevice>> GetAllDevicesAsync();
        Task<IEnumerable<IDevice>> GetDevicesByNameAsync(string name);
    }
}
```

**IDeviceLifecycleManager (Internal coordination - startup/shutdown)**
```csharp
namespace SmartLab.Domains.Device.Interfaces
{
    public interface IDeviceLifecycleManager
    {
        Task InitializeDevicesAsync();
        Task ShutdownDevicesAsync();
    }
}
```

#### Benefits

| Aspect | Before | After |
|--------|--------|-------|
| **Clarity** | One "controller" does everything | Clear separation: Service, Queries, Lifecycle |
| **Testability** | Must mock all dependencies | Can test queries without service dependencies |
| **Maintainability** | 197 lines in one file | ~50 lines per class, focused responsibility |
| **Dependency Injection** | Pages inject full controller | Pages inject only what they need |
| **Startup Logic** | Ugly cast to concrete type | Clean interface-based initialization |
| **SOLID Compliance** | Violates SRP, ISP | Follows all SOLID principles |

**Pros:** Clean architecture, maintainable, testable, right-sized
**Cons:** More files, requires consumer updates

---

### Option C: Full CQRS with MediatR (Advanced)

**Introduces:**
- Commands: CreateDeviceCommand, RemoveDeviceCommand, UpdateDeviceCommand
- Queries: GetDeviceQuery, GetAllDevicesQuery, GetDevicesByNameQuery
- Handlers: One handler per command/query
- MediatR library for message dispatching

**Example:**
```csharp
// In Razor Page
public async Task OnPostAsync()
{
    var command = new CreateDeviceCommand(config);
    var device = await _mediator.Send(command);
}
```

**Pros:** Maximum decoupling, enterprise-grade, audit trail built-in
**Cons:** High complexity, overkill for this project size, learning curve

---

## Detailed Implementation Plan (Option B)

### Phase 1: Create New Interfaces & Implementations

#### Step 1.1: Create IDeviceQueries

**File:** `Domains/Device/Interfaces/IDeviceQueries.cs`
```csharp
using SmartLab.Domains.Device.Interfaces;

namespace SmartLab.Domains.Device.Interfaces
{
    public interface IDeviceQueries
    {
        Task<IDevice?> GetDeviceAsync(Guid id);
        Task<IEnumerable<IDevice>> GetAllDevicesAsync();
        Task<IEnumerable<IDevice>> GetDevicesByNameAsync(string name);
    }
}
```

#### Step 1.2: Create DeviceQueries Implementation

**File:** `Domains/Device/Services/DeviceQueries.cs`
```csharp
using SmartLab.Domains.Device.Interfaces;
using Microsoft.Extensions.Logging;

namespace SmartLab.Domains.Device.Services
{
    public class DeviceQueries : IDeviceQueries
    {
        private readonly IDeviceRegistry _registry;
        private readonly ILogger<DeviceQueries> _logger;

        public DeviceQueries(IDeviceRegistry registry, ILogger<DeviceQueries> logger)
        {
            _registry = registry;
            _logger = logger;
        }

        public async Task<IDevice?> GetDeviceAsync(Guid id)
        {
            return await _registry.GetDeviceAsync(id);
        }

        public async Task<IEnumerable<IDevice>> GetAllDevicesAsync()
        {
            await Task.CompletedTask;
            return _registry.GetAllDevices();
        }

        public async Task<IEnumerable<IDevice>> GetDevicesByNameAsync(string name)
        {
            await Task.CompletedTask;
            return _registry.GetDevicesByName(name);
        }
    }
}
```

#### Step 1.3: Create IDeviceService

**File:** `Domains/Device/Interfaces/IDeviceService.cs`
```csharp
using SmartLab.Domains.Device.Models;

namespace SmartLab.Domains.Device.Interfaces
{
    public interface IDeviceService
    {
        Task<IDevice> CreateDeviceAsync(DeviceConfiguration config);
        Task RemoveDeviceAsync(Guid id);
        Task UpdateDeviceAsync(IDevice device);
    }
}
```

#### Step 1.4: Create DeviceService Implementation

**File:** `Domains/Device/Services/DeviceService.cs`
```csharp
using SmartLab.Domains.Device.Interfaces;
using SmartLab.Domains.Device.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SmartLab.Domains.Device.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly IDeviceFactory _factory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDeviceRegistry _registry;
        private readonly ILogger<DeviceService> _logger;

        public DeviceService(
            IDeviceFactory factory,
            IServiceScopeFactory scopeFactory,
            IDeviceRegistry registry,
            ILogger<DeviceService> logger)
        {
            _factory = factory;
            _scopeFactory = scopeFactory;
            _registry = registry;
            _logger = logger;
        }

        public async Task<IDevice> CreateDeviceAsync(DeviceConfiguration config)
        {
            try
            {
                if (!_factory.CanCreateDevice(config))
                {
                    throw new NotSupportedException($"Device configuration is invalid: {config.DeviceName}");
                }

                var device = _factory.CreateDevice(config);

                // Use scoped repository for database access
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                    await repository.SaveAsync(config);
                }

                await _registry.RegisterDeviceAsync(device);

                _logger.LogInformation("Created and registered device {DeviceName} with ID {DeviceId}",
                    config.DeviceName, config.DeviceID);

                return device;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create device {DeviceName}", config.DeviceName);
                throw;
            }
        }

        public async Task RemoveDeviceAsync(Guid id)
        {
            try
            {
                await _registry.UnregisterDeviceAsync(id);

                // Use scoped repository for database access
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                    await repository.DeleteAsync(id);
                }

                _logger.LogInformation("Removed device with ID {DeviceId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove device with ID {DeviceId}", id);
                throw;
            }
        }

        public async Task UpdateDeviceAsync(IDevice device)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(device);

                var config = new DeviceConfiguration
                {
                    DeviceID = device.DeviceID,
                    DeviceName = device.DeviceName,
                    DeviceExecutablePath = device.DeviceExecutablePath ?? "",
                    DeviceIdentifier = device.DeviceIdentifier ?? ""
                };

                // Use scoped repository for database access
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                    await repository.SaveAsync(config);
                }

                _logger.LogInformation("Updated device {DeviceName} with ID {DeviceId}",
                    device.DeviceName, device.DeviceID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update device with ID {DeviceId}", device.DeviceID);
                throw;
            }
        }
    }
}
```

#### Step 1.5: Create IDeviceLifecycleManager

**File:** `Domains/Device/Interfaces/IDeviceLifecycleManager.cs`
```csharp
namespace SmartLab.Domains.Device.Interfaces
{
    public interface IDeviceLifecycleManager
    {
        Task InitializeDevicesAsync();
        Task ShutdownDevicesAsync();
    }
}
```

#### Step 1.6: Create DeviceLifecycleManager Implementation

**File:** `Domains/Device/Services/DeviceLifecycleManager.cs`
```csharp
using SmartLab.Domains.Device.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SmartLab.Domains.Device.Services
{
    public class DeviceLifecycleManager : IDeviceLifecycleManager
    {
        private readonly IDeviceFactory _factory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDeviceRegistry _registry;
        private readonly ILogger<DeviceLifecycleManager> _logger;

        public DeviceLifecycleManager(
            IDeviceFactory factory,
            IServiceScopeFactory scopeFactory,
            IDeviceRegistry registry,
            ILogger<DeviceLifecycleManager> logger)
        {
            _factory = factory;
            _scopeFactory = scopeFactory;
            _registry = registry;
            _logger = logger;
        }

        public async Task InitializeDevicesAsync()
        {
            try
            {
                IEnumerable<DeviceConfiguration> configurations;

                // Use scoped repository for database access
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                    configurations = await repository.GetAllAsync();
                }

                foreach (var config in configurations)
                {
                    if (_factory.CanCreateDevice(config))
                    {
                        var device = _factory.CreateDevice(config);
                        await _registry.RegisterDeviceAsync(device);
                        _logger.LogInformation("Loaded device {DeviceName} with ID {DeviceId}",
                            config.DeviceName, config.DeviceID);
                    }
                    else
                    {
                        _logger.LogWarning("Cannot create device {DeviceName}, invalid configuration",
                            config.DeviceName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize devices");
                throw;
            }
        }

        public async Task ShutdownDevicesAsync()
        {
            try
            {
                var devices = _registry.GetAllDevices();
                foreach (var device in devices)
                {
                    await _registry.UnregisterDeviceAsync(device.DeviceID);
                }
                _logger.LogInformation("Shutdown all devices");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to shutdown devices");
                throw;
            }
        }
    }
}
```

---

### Phase 2: Update Dependency Injection

**File:** `Program.cs`

**Remove or mark obsolete:**
```csharp
// builder.Services.AddSingleton<IDeviceController, DeviceController>();
```

**Add new services:**
```csharp
// Register device-related services
builder.Services.AddSingleton<IDeviceFactory, DeviceFactory>();
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>(); // Scoped - needs DbContext
builder.Services.AddSingleton<IDeviceRegistry, DeviceRegistry>();

// New service layer
builder.Services.AddSingleton<IDeviceService, DeviceService>();
builder.Services.AddSingleton<IDeviceQueries, DeviceQueries>();
builder.Services.AddSingleton<IDeviceLifecycleManager, DeviceLifecycleManager>();
```

**Update startup code:**
```csharp
// Replace lines 98-105 with:
using (var scope = app.Services.CreateScope())
{
    var lifecycleManager = scope.ServiceProvider.GetRequiredService<IDeviceLifecycleManager>();
    await lifecycleManager.InitializeDevicesAsync();
    app.Logger.LogInformation("Successfully initialized devices from database");
}
```

---

### Phase 3: Update Consumers (Razor Pages)

#### Example: DeviceForm.cshtml.cs

**Before:**
```csharp
private readonly IDeviceController _deviceController;

public DeviceFormModel(IDeviceController deviceController)
{
    _deviceController = deviceController;
}
```

**After:**
```csharp
private readonly IDeviceService _deviceService;
private readonly IDeviceQueries _deviceQueries;

public DeviceFormModel(IDeviceService deviceService, IDeviceQueries deviceQueries)
{
    _deviceService = deviceService;
    _deviceQueries = deviceQueries;
}

// Update method calls:
// GetDeviceAsync → _deviceQueries.GetDeviceAsync
// CreateDeviceAsync → _deviceService.CreateDeviceAsync
// UpdateDeviceAsync → _deviceService.UpdateDeviceAsync
```

#### Example: MeasurementIndex.cshtml.cs

**Before:**
```csharp
private readonly IDeviceController _deviceController;
```

**After:**
```csharp
private readonly IDeviceQueries _deviceQueries; // Only needs queries

public MeasurementIndex(IMeasurementController measurementController, IDeviceQueries deviceQueries)
{
    _measurementController = measurementController;
    _deviceQueries = deviceQueries;
}
```

---

### Phase 4: Deprecate Old Controller

**Mark DeviceController as obsolete:**
```csharp
[Obsolete("Use IDeviceService for commands and IDeviceQueries for read operations")]
public class DeviceController : IDeviceController
{
    // Keep temporarily for backward compatibility
    // Remove after all consumers migrated
}
```

---

### Phase 5: Final Cleanup

Once all consumers are migrated:
1. Delete `DeviceController.cs`
2. Delete `IDeviceController.cs`
3. Remove obsolete DI registrations

---

## Migration Checklist

### Files to Create
- [ ] `Domains/Device/Interfaces/IDeviceQueries.cs`
- [ ] `Domains/Device/Services/DeviceQueries.cs`
- [ ] `Domains/Device/Interfaces/IDeviceService.cs`
- [ ] `Domains/Device/Services/DeviceService.cs`
- [ ] `Domains/Device/Interfaces/IDeviceLifecycleManager.cs`
- [ ] `Domains/Device/Services/DeviceLifecycleManager.cs`

### Files to Update
- [ ] `Program.cs` - Update DI registrations and startup code
- [ ] `Pages/Devices/DeviceForm.cshtml.cs`
- [ ] `Pages/Devices/DevicesIndex.cshtml.cs`
- [ ] `Pages/Devices/EditDevice.cshtml.cs`
- [ ] `Pages/Measurements/ConfigureParameters.cshtml.cs`
- [ ] `Pages/Measurements/MeasurementIndex.cshtml.cs`

### Files to Delete (After Migration)
- [ ] `Domains/Device/Controllers/DeviceController.cs`
- [ ] `Domains/Device/Interfaces/IDeviceController.cs`

---

## Testing Strategy

1. **Unit Tests**
   - Test DeviceQueries separately (mock Registry)
   - Test DeviceService separately (mock Factory, Registry, ScopeFactory)
   - Test DeviceLifecycleManager separately

2. **Integration Tests**
   - Test full workflow: Create → Query → Update → Remove
   - Test initialization workflow

3. **Manual Testing**
   - Verify all Razor Pages still work
   - Verify device creation, updates, deletion
   - Verify application startup loads devices correctly

---

## Rollback Plan

If issues arise during migration:
1. Keep old `IDeviceController` and `DeviceController` active
2. Register both old and new services in DI
3. Migrate consumers one at a time
4. Test each page after migration
5. Roll back individual pages if needed

---

## Timeline Estimate

- **Phase 1** (Create new services): 2-3 hours
- **Phase 2** (Update DI): 30 minutes
- **Phase 3** (Update consumers): 2-3 hours
- **Phase 4** (Deprecate old): 15 minutes
- **Phase 5** (Cleanup): 30 minutes
- **Testing**: 2-3 hours

**Total: ~8-10 hours**

---

## Conclusion

**Recommended Approach: Option B (Service Layer Split)**

This refactoring will:
- Improve code maintainability
- Follow SOLID principles
- Enable better testing
- Provide clearer separation of concerns
- Set a good architectural foundation for future development

The registry and repository should both remain as they serve complementary purposes:
- **Registry**: Runtime device instances (in-memory)
- **Repository**: Persistent device configurations (database)
