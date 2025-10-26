# SQLite Implementation - Concepts & Literature Guide

This guide covers the architectural patterns, design principles, and key concepts used in the SmartLab SQLite implementation.

## Core Architectural Patterns

### 1. **ORM (Object-Relational Mapping)**
- **What**: Maps C# objects to database tables automatically
- **Implementation**: Entity Framework Core
- **Literature**:
  - "Entity Framework Core in Action" by Jon P. Smith
  - Official docs: https://learn.microsoft.com/en-us/ef/core/

### 2. **Repository Pattern**
- **What**: Abstraction layer between business logic and data access
- **Where**: `DeviceRepository.cs`
- **Benefits**: Decouples data access, testable, swappable
- **Literature**: "Patterns of Enterprise Application Architecture" by Martin Fowler

### 3. **Unit of Work Pattern**
- **What**: Groups database operations into transactions
- **Implementation**: `DbContext.SaveChangesAsync()` handles this
- **Where**: Implicit in EF Core's DbContext

### 4. **Dependency Injection (DI)**
- **What**: Objects receive dependencies rather than creating them
- **Where**: `Program.cs:36-58`
- **Pattern**: Constructor injection
- **Literature**: "Dependency Injection Principles, Practices, and Patterns" by Steven van Deursen

### 5. **Domain-Driven Design (DDD) - Light**
- **Entities**: `DatasetEntity`, `DataPointEntity`, etc.
- **Services**: `DataService`, `DeviceRepository`
- **Bounded Contexts**: Domains folder structure (Device, Data, Measurement)
- **Literature**: "Domain-Driven Design" by Eric Evans (full), "Domain-Driven Design Distilled" by Vaughn Vernon (shorter)

## Database Patterns

### 6. **Code-First Migrations**
- **What**: Define database schema in code, generate SQL automatically
- **How**: C# entity classes → migrations → database tables
- **Alternative**: Database-First (design DB first, generate code)

### 7. **Fluent API Configuration**
- **Where**: `SmartLabDbContext.OnModelCreating()` (Lines 32-94)
- **What**: Configures entity relationships, indexes, constraints via method chaining
- **Alternative**: Data Annotations (attributes on entity classes)

### 8. **Lazy Loading vs Eager Loading**
- **Eager Loading**: `.Include(d => d.DataPoints)` - loads related data upfront
- **Lazy Loading**: Loads related data on-demand (not used here)
- **Where**: `DataService.cs:54-55`

### 9. **Soft Delete Pattern**
- **Where**: `DeviceRepository.DeleteAsync()` Line 114
- **What**: Mark records as inactive instead of deleting
- **Benefits**: Data recovery, audit trails

### 10. **Connection Resiliency**
- **Where**: SQLite PRAGMA settings (SmartLabDbContext.cs:21-30)
- **Settings**: WAL mode, busy timeout, cache size

## Design Patterns Used

### 11. **Factory Pattern**
- **Where**: `IDeviceFactory`, `IMeasurementFactory`
- **What**: Encapsulates object creation logic
- **Literature**: "Design Patterns: Elements of Reusable Object-Oriented Software" (Gang of Four)

### 12. **Registry Pattern**
- **Where**: `IDeviceRegistry`, `IMeasurementRegistry`
- **What**: Central repository for tracking active instances

### 13. **Service Layer Pattern**
- **Where**: All `*Service.cs` files
- **What**: Business logic layer between UI and data access
- **Benefits**: Reusable, testable business operations

### 14. **DTO (Data Transfer Object) Pattern**
- **Where**: `DatasetSummary`, `ManualDataPoint`
- **What**: Objects designed for transferring data between layers
- **Benefits**: Decouples internal models from API/UI

## Entity Framework Core Concepts

### 15. **DbContext**
- **What**: Session with the database, manages entity instances
- **Lifetime**: Scoped per request in web apps
- **Where**: `SmartLabDbContext.cs`

### 16. **DbSet<T>**
- **What**: Collection representing a table
- **Where**: `DbSet<DatasetEntity> Datasets`
- **Usage**: Query and save entities

### 17. **Change Tracking**
- **What**: EF Core tracks entity state (Added/Modified/Deleted)
- **Disable**: `.AsNoTracking()` for read-only queries (performance)
- **Where**: `DeviceRepository.cs:29`

### 18. **Navigation Properties**
- **What**: Properties representing relationships
- **Where**: `Dataset.DataPoints`, `DataPoint.Dataset`
- **Types**: One-to-Many, Many-to-One, Many-to-Many

### 19. **Shadow Properties**
- **What**: Properties that exist in model but not in .NET class
- **Example**: Foreign keys can be shadow properties

### 20. **Value Conversions**
- **Where**: `.HasConversion<int>()` for enums (SmartLabDbContext.cs:42-43)
- **What**: Converts .NET types to database types

## Database Optimization Concepts

### 21. **Indexing Strategy**
- **Composite Index**: `(DatasetId, Timestamp)` for time-series queries
- **Single Column**: `ParameterName`, `CreatedDate`
- **Trade-off**: Faster reads, slower writes

### 22. **Cascade Delete**
- **Where**: Foreign key constraints
- **What**: Delete parent → automatically deletes children
- **Alternative**: Restrict, SetNull

### 23. **WAL (Write-Ahead Logging)**
- **Where**: `PRAGMA journal_mode=WAL` (SmartLabDbContext.cs:24)
- **What**: SQLite optimization for concurrent reads/writes
- **Literature**: SQLite documentation on WAL mode

### 24. **Database Normalization**
- **Level**: 3rd Normal Form (3NF)
- **What**: Eliminate data redundancy
- **Pattern**: Datasets → DataPoints (one-to-many)

## ASP.NET Core Patterns

### 25. **Razor Pages (Page-Based MVC)**
- **Where**: `Pages/*.cshtml.cs`
- **Pattern**: PageModel (similar to MVVM)
- **Alternative**: Traditional MVC with Controllers

### 26. **Scoped vs Singleton vs Transient**
- **Scoped**: Created once per request (`DbContext`, repositories)
- **Singleton**: Created once for app lifetime (`SettingsService`)
- **Transient**: Created each time requested (`ProxyDeviceCommunication`)
- **Where**: `Program.cs:36-58`

### 27. **Async/Await Pattern**
- **Where**: All database operations
- **What**: Non-blocking I/O operations
- **Benefits**: Scalability, better thread usage

## Validation & Data Integrity

### 28. **Data Annotations**
- **Where**: `[Required]`, `[MaxLength(255)]` in entity classes
- **What**: Declarative validation and schema constraints

### 29. **Foreign Key Constraints**
- **Where**: DataPoints → Datasets relationship
- **What**: Ensures referential integrity

### 30. **Validation Service Pattern**
- **Where**: `IDataValidationService`
- **What**: Centralized data validation logic

## Testing Concepts (not implemented but applicable)

### 31. **In-Memory Database**
- **For**: Unit testing repositories
- **What**: EF Core can use in-memory provider for tests

### 32. **Mock/Stub Pattern**
- **For**: Testing services without real database
- **Tools**: Moq, NSubstitute

## Key Literature & Resources

### Books
1. **"Entity Framework Core in Action"** - Jon P. Smith (comprehensive EF Core)
2. **"Patterns of Enterprise Application Architecture"** - Martin Fowler (repository, unit of work, etc.)
3. **"Clean Architecture"** - Robert C. Martin (layered architecture)
4. **"Domain-Driven Design Distilled"** - Vaughn Vernon (DDD concepts)
5. **"Dependency Injection Principles, Practices, and Patterns"** - Steven van Deursen

### Online Resources
1. **Microsoft Docs**: https://learn.microsoft.com/en-us/ef/core/
2. **SQLite Documentation**: https://www.sqlite.org/docs.html
3. **Martin Fowler's Blog**: https://martinfowler.com/eaaCatalog/
4. **ASP.NET Core Fundamentals**: https://learn.microsoft.com/en-us/aspnet/core/

### Video Courses
1. **Pluralsight**: "Entity Framework Core 6 Fundamentals"
2. **YouTube**: Tim Corey's C# tutorials on EF Core
3. **Microsoft Learn**: Free ASP.NET Core learning paths

## Keywords to Research Further

- **CQRS** (Command Query Responsibility Segregation) - advanced pattern
- **Event Sourcing** - storing state changes as events
- **Specification Pattern** - composable query logic
- **Repository vs DbContext** - when to use each
- **N+1 Query Problem** - performance anti-pattern
- **Database Seeding** - initial data loading
- **Migration Strategies** - blue/green, rolling updates
- **Connection Pooling** - database connection management

## Implementation Overview

### Database Schema
The implementation uses 4 main tables:

1. **Datasets** - Metadata for data collections
2. **DataPoints** - Individual measurement records
3. **ValidationErrors** - Data quality tracking
4. **DeviceConfigurations** - Device settings and state

### Key Files
- `SmartLabDbContext.cs` - Database context and configuration
- `Migrations/20251025123216_InitialCreate.cs` - Database schema definition
- `DeviceRepository.cs` - Device data access layer
- `DataService.cs` - Business logic for data operations
- `Program.cs` - Dependency injection and app configuration

### Architecture Layers
1. **Presentation Layer**: Razor Pages (`Pages/`)
2. **Service Layer**: Business logic (`*Service.cs`)
3. **Data Access Layer**: Repositories and DbContext
4. **Domain Layer**: Entities and interfaces (`Domains/`)

This implementation follows industry-standard patterns that scale from small applications to enterprise systems. The combination of Repository Pattern + Unit of Work + Dependency Injection is especially common in .NET applications.

## Migration Filename Convention

Migration files use timestamps for versioning:
```
20251025123216_InitialCreate.cs
└─────┬──────┘ └─────┬──────┘
  Timestamp      Description
      │
      └─ Format: YYYYMMDDHHmmss
         2025-10-25 12:32:16
```

This ensures migrations are applied in chronological order and prevents naming conflicts in team environments.
