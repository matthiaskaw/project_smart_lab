# Implementation Strategy: Custom Data Analysis Scripts Feature

## Table of Contents
1. [Overview & Business Value](#overview)
2. [Architecture Design](#architecture-design)
3. [Implementation Phases](#implementation-plan)
4. [Technical Specifications](#technical-specifications)
5. [Security & Performance](#security-considerations)
6. [Testing Strategy](#testing-strategy)
7. [Deployment & Operations](#deployment)

---

## Overview

### Business Problem
Currently, SmartLab stores measurement data from various devices but provides limited built-in analysis capabilities. Users need to:
- Download datasets manually
- Analyze data using external tools (Excel, Python notebooks, etc.)
- Re-upload visualizations or results separately

This creates friction in the workflow and limits the platform's value proposition.

### Proposed Solution
Add a **custom data analysis script** feature that allows users to:
1. **Upload Python scripts** via a web interface
2. **Execute scripts** directly on stored datasets within the platform
3. **Generate visualizations** (charts, graphs, statistical plots) automatically
4. **View results** inline without leaving the application
5. **Share scripts** with other users (optional future feature)

### Key Benefits
- **Reduced Workflow Friction**: Analyze data without leaving the platform
- **Reproducibility**: Scripts can be re-run on new datasets automatically
- **Collaboration**: Share analysis methods between team members
- **Extensibility**: Users can create custom analysis without modifying core application
- **Audit Trail**: All analysis results are stored with execution metadata

### Design Principles

**1. Python-First, Multi-Language Ready**
- **Initial Focus**: Python (most popular for data analysis)
- **Future-Proof**: Architecture supports R, Julia, MATLAB in future phases
- **Why Python First?**
  - Largest ecosystem (matplotlib, pandas, numpy, scipy)
  - Most accessible to researchers and engineers
  - Excellent plotting libraries
  - Fast to implement and test

**2. Security by Design**
- Scripts run in **isolated processes** with timeout limits
- **Static analysis** checks for dangerous code patterns
- **Sandboxed execution** prevents file system damage
- **Per-user isolation** prevents data leakage between users

**3. Cross-Platform Compatibility**
- Must work on **Windows** (developer machines, Windows Server)
- Must work on **Linux** (production servers, Docker containers)
- Automatic **platform detection** selects correct interpreters
- **Unified API** abstracts platform differences

**4. User-Friendly Upload Experience**
- **Web-based** upload (no server access required)
- **Live validation** feedback during upload
- **Preview** script before saving
- **Metadata management** (tags, descriptions, versioning)

---

## Architecture Design

### 1. **High-Level Data Flow**

```
┌─────────────────────────────────────────────────────────────────┐
│                         USER JOURNEY                            │
└─────────────────────────────────────────────────────────────────┘

Step 1: Script Management (One-time Setup)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
User navigates to: /Analysis/ManageScripts
        ↓
Sees three tabs:
  • Built-in Scripts (system-provided)
  • My Scripts (user-uploaded)
  • Shared Scripts (community)
        ↓
Clicks "Upload New Script" button
        ↓
Upload Modal opens:
  - Drag-drop .py file
  - Preview appears
  - Enter metadata (name, description, tags)
  - Click "Upload & Validate"
        ↓
Server validates script:
  - Syntax check (python -m py_compile)
  - Security scan (dangerous patterns)
  - Structure check (JSON I/O, imports)
        ↓
Script saved to: analysis-scripts/user-uploads/[user_id]/
        ↓
Metadata cached in: .metadata.json
        ↓
User sees validation result:
  ✓ Passed → Green badge
  ⚠ Warning → Orange badge (allowed but flagged)
  ✗ Failed → Red badge (cannot execute)


Step 2: Dataset Analysis (Regular Usage)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
User clicks "View" button on dataset in /Data/DataIndex
        ↓
ViewDataset page loads (/Data/ViewDataset?id={guid})
        ↓
Page displays:
  • Dataset metadata (name, date, source, points count)
  • "Run Analysis" section with dropdown
  • Previous analysis results (if any)
        ↓
User selects script from dropdown:
  [Built-in Scripts]
    • Basic Line Plot
    • Histogram
    • Statistical Summary
  [My Scripts]
    • FFT Analysis (user-uploaded)
    • Custom Temperature Plot
        ↓
Script metadata loads via AJAX:
  - Description appears
  - Dynamic parameter fields generate:
      [Window Size: ___]  (number input)
      [Smoothing: ☑]      (checkbox)
      [Color: 🎨]          (color picker)
        ↓
User configures parameters and clicks "Run Analysis"
        ↓
Server processes request:

  ┌─────────────────────────────────────────────┐
  │   Backend Processing Flow                   │
  └─────────────────────────────────────────────┘

  1. AnalysisService.ExecuteAnalysisAsync() called
          ↓
  2. Load dataset from database:
     - DataPoints (timestamp, parameter, value, unit)
     - RawDataJson (original device output)
     - Metadata
          ↓
  3. Transform to standard JSON format:
     {
       "datasetId": "guid",
       "datasetName": "Temperature Measurement",
       "dataPoints": [
         {"timestamp": "...", "value": 22.5, "unit": "°C"},
         ...
       ],
       "parameters": {
         "window_size": 128,
         "smoothing": true
       }
     }
          ↓
  4. ScriptExecutorFactory.GetExecutorForFile(".py")
     → Returns: PythonScriptExecutor
          ↓
  5. PythonScriptExecutor.ExecuteScriptAsync():

     a) Create ProcessStartInfo:
        FileName: "python" or "python3" (platform-specific)
        Arguments: "script.py output_dir"
        RedirectStandardInput: true
        RedirectStandardOutput: true
        RedirectStandardError: true
        Environment["PYTHONIOENCODING"]: "utf-8"
        Environment["MPLBACKEND"]: "Agg" (Linux only)

     b) Start process

     c) Write JSON to stdin:
        process.StandardInput.Write(jsonData)
        process.StandardInput.Close()

     d) Wait for exit (max 5 minutes):
        - Timeout → Kill process → Return error
        - Success → Read stdout and stderr

     e) Parse output JSON from stdout:
        {
          "status": "success",
          "imagePath": "result_abc123.png",
          "statistics": {
            "mean": 22.5,
            "std": 0.5
          }
        }
          ↓
  6. Verify image file exists:
     wwwroot/analysis-results/result_abc123.png
          ↓
  7. Save to database (AnalysisResults table):
     - DatasetId, ScriptName, ScriptLanguage
     - ExecutionDate, Status
     - ResultImagePath, ResultDataJson
     - ExecutionTimeMs, Parameters (JSON)
          ↓
  8. Return AnalysisResult to UI
          ↓
Page reloads (or partial update via AJAX)
        ↓
New section appears showing:
  📊 Generated Chart
  📈 Statistical Summary (mean, std, etc.)
  🕒 Execution Time: 1.2s
  [Delete Result] button
```

### 2. **Component Interaction Diagram**

```
┌─────────────────────────────────────────────────────────────────┐
│                    SYSTEM ARCHITECTURE                          │
└─────────────────────────────────────────────────────────────────┘

┌───────────────────┐
│   Browser (User)  │
└─────────┬─────────┘
          │ HTTP POST /Analysis/ManageScripts?handler=Upload
          ↓
┌─────────────────────────────────────────────────────┐
│               ASP.NET Core (Razor Pages)            │
│  ┌───────────────────────────────────────────────┐  │
│  │  ManageScriptsModel (Page Model)              │  │
│  │  • OnPostUploadAsync()                        │  │
│  │  • Validate file size, extension              │  │
│  │  • Read file content                          │  │
│  └───────────────────┬───────────────────────────┘  │
│                      │                              │
│                      ↓                              │
│  ┌───────────────────────────────────────────────┐  │
│  │  IScriptManagementService                     │  │
│  │  ┌─────────────────────────────────────────┐  │  │
│  │  │ ScriptManagementService                 │  │  │
│  │  │ • UploadScriptAsync()                   │  │  │
│  │  │ • Generate unique script ID             │  │  │
│  │  │ • Save to filesystem                    │  │  │
│  │  └───────────┬─────────────────────────────┘  │  │
│  │              │ Calls                           │  │
│  │              ↓                                 │  │
│  │  ┌─────────────────────────────────────────┐  │  │
│  │  │ IScriptValidationService                │  │  │
│  │  │ ┌─────────────────────────────────────┐ │  │  │
│  │  │ │ ScriptValidationService             │ │  │  │
│  │  │ │ • ValidatePythonScript()            │ │  │  │
│  │  │ │ • Check dangerous patterns          │ │  │  │
│  │  │ │ • Run syntax check (py_compile)     │ │  │  │
│  │  │ │ • Validate structure                │ │  │  │
│  │  │ └───────────┬─────────────────────────┘ │  │  │
│  │  │             │ Uses                      │  │  │
│  │  │             ↓                           │  │  │
│  │  │ ┌─────────────────────────────────────┐ │  │  │
│  │  │ │ IPlatformHelper                     │ │  │  │
│  │  │ │ • GetPythonCommand() → "python3"    │ │  │  │
│  │  │ │ • IsLinux() → true                  │ │  │  │
│  │  │ └─────────────────────────────────────┘ │  │  │
│  │  └─────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
          │
          ↓ Writes to
┌─────────────────────────────────────────┐
│       File System                       │
│  analysis-scripts/                      │
│    └── user-uploads/                    │
│        └── [user_id]/                   │
│            ├── fft_analysis.py          │
│            └── .metadata.json           │
└─────────────────────────────────────────┘


Later: Analysis Execution Flow
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

┌───────────────────┐
│   Browser (User)  │
└─────────┬─────────┘
          │ HTTP POST /Data/ViewDataset?handler=RunAnalysis
          ↓
┌─────────────────────────────────────────────────────────┐
│               ViewDatasetModel                          │
│  • OnPostRunAnalysisAsync(scriptId, parameters)        │
└───────────────────┬─────────────────────────────────────┘
                    │ Calls
                    ↓
┌─────────────────────────────────────────────────────────┐
│               IAnalysisService                          │
│  ┌───────────────────────────────────────────────────┐  │
│  │ AnalysisService                                   │  │
│  │ • ExecuteAnalysisAsync(datasetId, scriptId, ...) │  │
│  │                                                   │  │
│  │ Step 1: Load dataset from DB                     │  │
│  │   └─→ IDataService.GetDatasetAsync()             │  │
│  │       IDataService.GetDataPointsAsync()          │  │
│  │                                                   │  │
│  │ Step 2: Transform to JSON                        │  │
│  │   └─→ Build standardized JSON structure          │  │
│  │                                                   │  │
│  │ Step 3: Get script path                          │  │
│  │   └─→ IScriptManagementService.GetScriptPathAsync│  │
│  │                                                   │  │
│  │ Step 4: Select executor                          │  │
│  │   └─→ ScriptExecutorFactory.GetExecutorForFile() │  │
│  │       Returns: IScriptExecutor (Python/R/etc.)   │  │
│  │                                                   │  │
│  │ Step 5: Execute script                           │  │
│  │   └─→ executor.ExecuteScriptAsync()              │  │
│  │         │                                         │  │
│  │         ↓                                         │  │
│  │    ┌─────────────────────────────────────┐       │  │
│  │    │  PythonScriptExecutor               │       │  │
│  │    │  • Start Python process             │       │  │
│  │    │  • Write JSON to stdin              │       │  │
│  │    │  • Wait for completion (timeout)    │       │  │
│  │    │  • Read JSON from stdout            │       │  │
│  │    │  • Parse result                     │       │  │
│  │    └────────────┬────────────────────────┘       │  │
│  │                 │ Spawns                         │  │
│  │                 ↓                                │  │
│  │    ┌─────────────────────────────────────┐       │  │
│  │    │  External Python Process            │       │  │
│  │    │  python3 script.py output_dir       │       │  │
│  │    │  • Read JSON from stdin             │       │  │
│  │    │  • Process data                     │       │  │
│  │    │  • Generate plot (matplotlib)       │       │  │
│  │    │  • Save PNG to output_dir           │       │  │
│  │    │  • Write result JSON to stdout      │       │  │
│  │    │  • Exit with code 0                 │       │  │
│  │    └────────────┬────────────────────────┘       │  │
│  │                 │ Returns                        │  │
│  │                 ↓                                │  │
│  │    ProcessResult { Success, Output, ... }        │  │
│  │                                                   │  │
│  │ Step 6: Save result to database                  │  │
│  │   └─→ SmartLabDbContext.AnalysisResults.Add()    │  │
│  │       SaveChangesAsync()                         │  │
│  │                                                   │  │
│  │ Step 7: Return AnalysisResult                    │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
                    │
                    ↓ Returns to
            ViewDatasetModel
                    │
                    ↓ RedirectToPage()
            Browser (refreshes page)
                    │
                    ↓ Displays
      📊 Chart + 📈 Statistics + 🕒 Execution time
```

---

## Implementation Plan

### **Phase 1: Core Infrastructure** (Foundation)

**Goal**: Establish the foundational domain layer, database schema, and core interfaces following Domain-Driven Design principles.

**Duration Estimate**: 2-3 days

**Prerequisites**:
- Understanding of existing SmartLab DDD structure
- Familiarity with ProxyDevice pattern (for reference)
- SQLite/EF Core knowledge

#### 1.1 Create Analysis Domain Layer

**Rationale**: Following your existing DDD pattern (Device/, Measurement/, Data/ domains), we create a new Analysis/ domain that encapsulates all script management and execution logic. This maintains clean separation of concerns and makes the codebase maintainable.

**Directory Structure Explained**:
```
Domains/Analysis/
├── Services/
│   ├── IAnalysisService.cs              (Interface)
│   ├── AnalysisService.cs                (Main service)
│   ├── IScriptExecutor.cs                (Executor interface)
│   ├── ScriptExecutorFactory.cs          (Factory for selecting executor)
│   ├── PythonScriptExecutor.cs           (Python process management)
│   ├── RScriptExecutor.cs                (R process management)
│   └── GenericScriptExecutor.cs          (Fallback for other languages)
├── Models/
│   ├── AnalysisScript.cs                 (Script metadata)
│   ├── AnalysisRequest.cs                (Input parameters)
│   ├── AnalysisResult.cs                 (Output with image path)
│   ├── ScriptParameter.cs                (User-configurable parameters)
│   ├── ScriptLanguage.cs                 (Enum: Python, R, Shell, etc.)
│   └── ProcessResult.cs                  (Execution result)
├── Platform/
│   ├── IPlatformHelper.cs                (Platform detection interface)
│   └── PlatformHelper.cs                 (OS-specific logic)
└── Database/
    └── AnalysisResultEntity.cs           (Persistent storage)
```

**Key Components:**

**IAnalysisService** - Main interface
```csharp
Task<List<AnalysisScript>> GetAvailableScriptsAsync();
Task<AnalysisResult> ExecuteAnalysisAsync(
    Guid datasetId,
    string scriptName,
    Dictionary<string, object> parameters
);
Task<List<AnalysisResult>> GetAnalysisHistoryAsync(Guid datasetId);
Task<bool> DeleteAnalysisResultAsync(Guid resultId);
```

**IScriptExecutor** - Base executor interface
```csharp
public interface IScriptExecutor
{
    ScriptLanguage SupportedLanguage { get; }
    Task<ProcessResult> ExecuteScriptAsync(
        string scriptPath,
        string inputJson,
        string outputDirectory,
        Dictionary<string, object> parameters,
        int timeoutSeconds = 300
    );
    bool IsInterpreterAvailable();
}
```

**ScriptExecutorFactory** - Executor selection
```csharp
public class ScriptExecutorFactory
{
    public IScriptExecutor GetExecutor(ScriptLanguage language);
    public IScriptExecutor GetExecutorForFile(string scriptPath);
}
```

**IPlatformHelper** - Platform detection
```csharp
public interface IPlatformHelper
{
    bool IsWindows();
    bool IsLinux();
    bool IsMacOS();
    string GetPythonCommand();
    string GetRCommand();
}
```

#### 1.2 Database Schema Updates

Add new table: **AnalysisResults**
```sql
CREATE TABLE AnalysisResults (
    Id                UNIQUEIDENTIFIER PRIMARY KEY,
    DatasetId         UNIQUEIDENTIFIER NOT NULL,  -- FK to Datasets
    ScriptName        NVARCHAR(255) NOT NULL,
    ScriptLanguage    NVARCHAR(50) NOT NULL,      -- Python/R/Shell/etc.
    ScriptVersion     NVARCHAR(50),
    ExecutionDate     DATETIME NOT NULL,
    Status            NVARCHAR(50) NOT NULL,      -- Success/Failed/Timeout
    Parameters        NVARCHAR(MAX),              -- JSON
    ResultImagePath   NVARCHAR(500),              -- Relative path
    ResultDataJson    NVARCHAR(MAX),              -- Optional numerical results
    ErrorMessage      NVARCHAR(MAX),
    ExecutionTimeMs   INT,
    FOREIGN KEY (DatasetId) REFERENCES Datasets(Id) ON DELETE CASCADE
);
```

Create migration:
```bash
dotnet ef migrations add AddAnalysisResults
dotnet ef database update
```

---

### **Phase 2: Multi-Language Script Infrastructure**

#### 2.1 Script Directory Structure

**Initial Implementation (Python Focus):**
```
analysis-scripts/
├── _templates/
│   └── python/
│       ├── template.py                    (Comprehensive example)
│       ├── template_minimal.py            (Minimal example)
│       └── README.md                      (Development guide)
│
├── built-in/                              (System-provided scripts)
│   └── python/
│       ├── basic_line_plot.py             (Time series line plot)
│       ├── histogram.py                   (Distribution analysis)
│       ├── scatter_plot.py                (Correlation analysis)
│       ├── statistical_summary.py         (Stats + table visualization)
│       └── multi_parameter_plot.py        (Multiple parameters overlay)
│
└── user-uploads/                          (User-uploaded scripts)
    ├── [user_id_1]/                       (Per-user organization)
    │   ├── my_custom_analysis.py
    │   ├── advanced_fft.py
    │   └── .metadata.json                 (Script metadata cache)
    ├── [user_id_2]/
    │   └── temperature_analysis.py
    └── shared/                            (Admin-approved shared scripts)
        └── community_contributed.py
```

**Directory Details:**

1. **_templates/**: Read-only templates for users to copy
   - Not executable directly
   - Serve as documentation and starting points
   - Versioned alongside the application

2. **built-in/**: System-provided, pre-validated scripts
   - Read-only (cannot be modified via UI)
   - Automatically discovered on startup
   - Include comprehensive error handling
   - Maintained by developers

3. **user-uploads/**: User-managed scripts
   - Organized by user ID (or username)
   - Each user has isolated directory
   - `.metadata.json` stores script info (name, description, parameters, upload date)
   - `shared/` subfolder for admin-promoted scripts

**Future Extension (Other Languages):**
```
analysis-scripts/
├── _templates/
│   ├── python/
│   └── r/                                 (Added in Phase 6+)
├── built-in/
│   ├── python/
│   └── r/                                 (Added in Phase 6+)
└── user-uploads/
    ├── [user_id]/
    │   ├── *.py
    │   └── *.R                            (Added in Phase 6+)
    └── shared/
```

#### 2.2 Universal Script Contract (Language-Agnostic)

**Input:** Scripts receive JSON via stdin
```json
{
  "datasetId": "guid",
  "datasetName": "Temperature Measurement",
  "createdDate": "2025-11-07T10:00:00Z",
  "dataSource": "Device",
  "parameters": {
    "window_size": 10,
    "smoothing": true,
    "color": "#FF5733"
  },
  "dataPoints": [
    {"timestamp": "2025-11-07T10:00:00", "parameter": "Temp", "value": 22.5, "unit": "°C"},
    {"timestamp": "2025-11-07T10:01:00", "parameter": "Temp", "value": 22.6, "unit": "°C"}
  ],
  "rawData": ["optional raw format data"]
}
```

**Output:** Scripts write JSON to stdout
```json
{
  "status": "success",
  "imagePath": "result_20251107_100530.png",
  "statistics": {
    "mean": 22.55,
    "std": 0.071,
    "min": 22.5,
    "max": 22.6
  },
  "metadata": {
    "scriptVersion": "1.0.0",
    "processingTimeMs": 1234
  }
}
```

**Error Output:**
```json
{
  "status": "error",
  "errorMessage": "ValueError: Invalid data format",
  "errorType": "ValueError"
}
```

#### 2.3 Template Script Examples

**analysis-scripts/_templates/python/template.py:**
```python
#!/usr/bin/env python3
"""
SmartLab Analysis Script Template
----------------------------------
This template shows how to create custom analysis scripts.

Requirements:
- Read JSON input from stdin
- Generate visualization (PNG/SVG)
- Write result JSON to stdout
- Save image to output directory passed as argument
"""

import sys
import json
import matplotlib.pyplot as plt
from pathlib import Path

def main():
    # 1. Read input data
    input_data = json.load(sys.stdin)

    dataset_name = input_data['datasetName']
    data_points = input_data['dataPoints']
    params = input_data.get('parameters', {})
    output_dir = sys.argv[1] if len(sys.argv) > 1 else '.'

    # 2. Process data
    timestamps = [dp['timestamp'] for dp in data_points]
    values = [float(dp['value']) for dp in data_points]

    # 3. Create visualization
    plt.figure(figsize=(10, 6))
    plt.plot(timestamps, values, marker='o')
    plt.title(f'Analysis: {dataset_name}')
    plt.xlabel('Time')
    plt.ylabel('Value')
    plt.xticks(rotation=45)
    plt.tight_layout()

    # 4. Save image
    output_filename = f"result_{input_data['datasetId'][:8]}.png"
    output_path = Path(output_dir) / output_filename
    plt.savefig(output_path, dpi=150)
    plt.close()

    # 5. Calculate statistics
    mean_val = sum(values) / len(values)

    # 6. Write result to stdout
    result = {
        "status": "success",
        "imagePath": output_filename,
        "statistics": {
            "mean": mean_val,
            "count": len(values)
        },
        "metadata": {
            "scriptVersion": "1.0.0"
        }
    }
    print(json.dumps(result))

if __name__ == '__main__':
    try:
        main()
    except Exception as e:
        error_result = {
            "status": "error",
            "errorMessage": str(e),
            "errorType": type(e).__name__
        }
        print(json.dumps(error_result))
        sys.exit(1)
```

**analysis-scripts/_templates/r/template.R:**
```r
#!/usr/bin/env Rscript
# SmartLab Analysis Script Template (R)
# --------------------------------------
# This template shows how to create R analysis scripts.
#
# Requirements:
# - Read JSON input from stdin
# - Generate visualization (PNG/PDF)
# - Write result JSON to stdout
# - Save image to output directory passed as argument

library(jsonlite)
library(ggplot2)

main <- function() {
  # 1. Read command line arguments
  args <- commandArgs(trailingOnly = TRUE)
  output_dir <- if (length(args) > 0) args[1] else "."

  # 2. Read input data from stdin
  input_data <- fromJSON(file("stdin"), simplifyDataFrame = TRUE)

  dataset_name <- input_data$datasetName
  data_points <- input_data$dataPoints
  params <- if (is.null(input_data$parameters)) list() else input_data$parameters

  # 3. Process data
  timestamps <- as.POSIXct(data_points$timestamp, format="%Y-%m-%dT%H:%M:%S")
  values <- as.numeric(data_points$value)

  df <- data.frame(
    timestamp = timestamps,
    value = values
  )

  # 4. Create visualization
  p <- ggplot(df, aes(x = timestamp, y = value)) +
    geom_line(color = "blue") +
    geom_point() +
    labs(
      title = paste("Analysis:", dataset_name),
      x = "Time",
      y = "Value"
    ) +
    theme_minimal() +
    theme(axis.text.x = element_text(angle = 45, hjust = 1))

  # 5. Save image
  dataset_id_short <- substr(input_data$datasetId, 1, 8)
  output_filename <- paste0("result_", dataset_id_short, ".png")
  output_path <- file.path(output_dir, output_filename)

  ggsave(output_path, plot = p, width = 10, height = 6, dpi = 150)

  # 6. Calculate statistics
  mean_val <- mean(values)
  std_val <- sd(values)

  # 7. Write result to stdout
  result <- list(
    status = "success",
    imagePath = output_filename,
    statistics = list(
      mean = mean_val,
      sd = std_val,
      count = length(values)
    ),
    metadata = list(
      scriptVersion = "1.0.0",
      rVersion = paste(R.version$major, R.version$minor, sep = ".")
    )
  )

  cat(toJSON(result, auto_unbox = TRUE))
}

# Error handling
tryCatch({
  main()
}, error = function(e) {
  error_result <- list(
    status = "error",
    errorMessage = as.character(e$message),
    errorType = "Error"
  )
  cat(toJSON(error_result, auto_unbox = TRUE))
  quit(status = 1)
})
```

#### 2.4 Script Metadata Model

**Script Metadata Structure (.metadata.json):**
```json
{
  "scripts": [
    {
      "id": "guid",
      "fileName": "my_analysis.py",
      "displayName": "My Custom Analysis",
      "description": "Performs FFT analysis on time-series data",
      "author": "user@example.com",
      "uploadDate": "2025-11-07T10:00:00Z",
      "lastModified": "2025-11-07T10:00:00Z",
      "version": "1.0.0",
      "language": "python",
      "tags": ["fft", "frequency", "signal-processing"],
      "parameters": [
        {
          "name": "window_size",
          "displayName": "Window Size",
          "type": "number",
          "defaultValue": 128,
          "required": true,
          "description": "FFT window size (power of 2)"
        },
        {
          "name": "overlap",
          "displayName": "Overlap Percentage",
          "type": "number",
          "defaultValue": 50,
          "min": 0,
          "max": 100,
          "required": false
        }
      ],
      "validationStatus": "passed",
      "validationErrors": [],
      "isShared": false,
      "executionCount": 42,
      "lastExecuted": "2025-11-07T15:30:00Z"
    }
  ]
}
```

---

### **Phase 3: Script Upload & Management UI**

#### 3.1 Script Management Page

**Pages/Analysis/ManageScripts.cshtml:**
```html
@page
@model ManageScriptsModel

<div class="container mt-4">
    <div class="d-flex justify-content-between align-items-center mb-3">
        <h1>Analysis Scripts</h1>
        <button type="button" class="btn btn-primary" data-bs-toggle="modal" data-bs-target="#uploadModal">
            <i class="fas fa-upload"></i> Upload New Script
        </button>
    </div>

    <!-- Tabs for Built-in vs User Scripts -->
    <ul class="nav nav-tabs mb-3" role="tablist">
        <li class="nav-item">
            <a class="nav-link active" data-bs-toggle="tab" href="#built-in-scripts">
                Built-in Scripts
            </a>
        </li>
        <li class="nav-item">
            <a class="nav-link" data-bs-toggle="tab" href="#user-scripts">
                My Scripts <span class="badge bg-primary">@Model.UserScripts.Count</span>
            </a>
        </li>
        <li class="nav-item">
            <a class="nav-link" data-bs-toggle="tab" href="#shared-scripts">
                Shared Scripts
            </a>
        </li>
    </ul>

    <div class="tab-content">
        <!-- Built-in Scripts Tab -->
        <div class="tab-pane fade show active" id="built-in-scripts">
            <div class="row">
                @foreach (var script in Model.BuiltInScripts)
                {
                    <div class="col-md-6 col-lg-4 mb-3">
                        <div class="card h-100">
                            <div class="card-header bg-success text-white">
                                <h5 class="mb-0">
                                    <i class="fas fa-check-circle"></i> @script.DisplayName
                                </h5>
                            </div>
                            <div class="card-body">
                                <p class="card-text">@script.Description</p>
                                <p class="text-muted small">
                                    <i class="fas fa-code"></i> @script.Language
                                    @if (script.Parameters?.Count > 0)
                                    {
                                        <span class="ms-2">
                                            <i class="fas fa-sliders-h"></i> @script.Parameters.Count parameters
                                        </span>
                                    }
                                </p>
                            </div>
                            <div class="card-footer">
                                <small class="text-muted">System-provided</small>
                            </div>
                        </div>
                    </div>
                }
            </div>
        </div>

        <!-- User Scripts Tab -->
        <div class="tab-pane fade" id="user-scripts">
            @if (Model.UserScripts.Count == 0)
            {
                <div class="alert alert-info">
                    <i class="fas fa-info-circle"></i> You haven't uploaded any scripts yet.
                    Click "Upload New Script" to get started.
                </div>
            }
            else
            {
                <div class="row">
                    @foreach (var script in Model.UserScripts)
                    {
                        <div class="col-md-6 col-lg-4 mb-3">
                            <div class="card h-100 @(script.ValidationStatus == "failed" ? "border-danger" : "")">
                                <div class="card-header">
                                    <div class="d-flex justify-content-between align-items-start">
                                        <h5 class="mb-0">@script.DisplayName</h5>
                                        <div class="dropdown">
                                            <button class="btn btn-sm btn-link" data-bs-toggle="dropdown">
                                                <i class="fas fa-ellipsis-v"></i>
                                            </button>
                                            <ul class="dropdown-menu">
                                                <li>
                                                    <a class="dropdown-item" href="#"
                                                       onclick="editScript('@script.Id')">
                                                        <i class="fas fa-edit"></i> Edit Details
                                                    </a>
                                                </li>
                                                <li>
                                                    <a class="dropdown-item"
                                                       href="/Analysis/DownloadScript?id=@script.Id">
                                                        <i class="fas fa-download"></i> Download
                                                    </a>
                                                </li>
                                                <li><hr class="dropdown-divider"></li>
                                                <li>
                                                    <form method="post" asp-page-handler="Delete"
                                                          asp-route-id="@script.Id"
                                                          style="display:inline;">
                                                        <button type="submit" class="dropdown-item text-danger"
                                                                onclick="return confirm('Delete this script?')">
                                                            <i class="fas fa-trash"></i> Delete
                                                        </button>
                                                    </form>
                                                </li>
                                            </ul>
                                        </div>
                                    </div>
                                </div>
                                <div class="card-body">
                                    <p class="card-text">@script.Description</p>

                                    <!-- Validation Status -->
                                    @if (script.ValidationStatus == "passed")
                                    {
                                        <span class="badge bg-success">
                                            <i class="fas fa-check"></i> Validated
                                        </span>
                                    }
                                    else if (script.ValidationStatus == "failed")
                                    {
                                        <span class="badge bg-danger">
                                            <i class="fas fa-exclamation-triangle"></i> Validation Failed
                                        </span>
                                        <div class="alert alert-danger mt-2 small">
                                            @foreach (var error in script.ValidationErrors)
                                            {
                                                <div>@error</div>
                                            }
                                        </div>
                                    }

                                    <!-- Tags -->
                                    @if (script.Tags?.Count > 0)
                                    {
                                        <div class="mt-2">
                                            @foreach (var tag in script.Tags)
                                            {
                                                <span class="badge bg-secondary me-1">@tag</span>
                                            }
                                        </div>
                                    }
                                </div>
                                <div class="card-footer">
                                    <small class="text-muted">
                                        Uploaded: @script.UploadDate.ToString("yyyy-MM-dd")
                                        @if (script.ExecutionCount > 0)
                                        {
                                            <span class="ms-2">
                                                <i class="fas fa-play-circle"></i> Run @script.ExecutionCount times
                                            </span>
                                        }
                                    </small>
                                </div>
                            </div>
                        </div>
                    }
                </div>
            }
        </div>

        <!-- Shared Scripts Tab -->
        <div class="tab-pane fade" id="shared-scripts">
            <p class="text-muted">Community-contributed scripts approved by administrators.</p>
            <!-- Similar card layout for shared scripts -->
        </div>
    </div>
</div>

<!-- Upload Modal -->
<div class="modal fade" id="uploadModal" tabindex="-1">
    <div class="modal-dialog modal-lg">
        <div class="modal-content">
            <form method="post" asp-page-handler="Upload" enctype="multipart/form-data" id="uploadForm">
                <div class="modal-header">
                    <h5 class="modal-title">Upload Analysis Script</h5>
                    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                </div>
                <div class="modal-body">
                    <!-- File Upload -->
                    <div class="mb-3">
                        <label for="scriptFile" class="form-label">
                            Python Script File <span class="text-danger">*</span>
                        </label>
                        <input type="file"
                               class="form-control"
                               id="scriptFile"
                               name="scriptFile"
                               accept=".py"
                               required
                               onchange="previewScript(this)">
                        <div class="form-text">
                            Only .py files are accepted. Max size: 1 MB
                        </div>
                    </div>

                    <!-- Script Preview -->
                    <div class="mb-3" id="scriptPreviewContainer" style="display:none;">
                        <label class="form-label">Script Preview</label>
                        <pre class="border p-2 bg-light"
                             id="scriptPreview"
                             style="max-height: 200px; overflow-y: auto; font-size: 0.85rem;"></pre>
                    </div>

                    <!-- Display Name -->
                    <div class="mb-3">
                        <label for="displayName" class="form-label">
                            Display Name <span class="text-danger">*</span>
                        </label>
                        <input type="text"
                               class="form-control"
                               id="displayName"
                               name="displayName"
                               placeholder="e.g., FFT Analysis"
                               required>
                    </div>

                    <!-- Description -->
                    <div class="mb-3">
                        <label for="description" class="form-label">Description</label>
                        <textarea class="form-control"
                                  id="description"
                                  name="description"
                                  rows="3"
                                  placeholder="Describe what this script does..."></textarea>
                    </div>

                    <!-- Tags -->
                    <div class="mb-3">
                        <label for="tags" class="form-label">Tags</label>
                        <input type="text"
                               class="form-control"
                               id="tags"
                               name="tags"
                               placeholder="e.g., fft, frequency, signal-processing (comma-separated)">
                    </div>

                    <!-- Version -->
                    <div class="mb-3">
                        <label for="version" class="form-label">Version</label>
                        <input type="text"
                               class="form-control"
                               id="version"
                               name="version"
                               value="1.0.0"
                               placeholder="1.0.0">
                    </div>

                    <!-- Validation Results (shown after upload) -->
                    <div id="validationResults" class="alert" style="display:none;"></div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                    <button type="submit" class="btn btn-primary">
                        <i class="fas fa-upload"></i> Upload & Validate
                    </button>
                </div>
            </form>
        </div>
    </div>
</div>

<script src="~/js/script-management.js"></script>
```

**Pages/Analysis/ManageScripts.cshtml.cs:**
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLab.Domains.Analysis.Interfaces;
using SmartLab.Domains.Analysis.Models;

public class ManageScriptsModel : PageModel
{
    private readonly IScriptManagementService _scriptService;
    private readonly ILogger<ManageScriptsModel> _logger;

    public List<AnalysisScriptMetadata> BuiltInScripts { get; set; } = new();
    public List<AnalysisScriptMetadata> UserScripts { get; set; } = new();
    public List<AnalysisScriptMetadata> SharedScripts { get; set; } = new();

    public ManageScriptsModel(
        IScriptManagementService scriptService,
        ILogger<ManageScriptsModel> logger)
    {
        _scriptService = scriptService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        var currentUserId = GetCurrentUserId();

        BuiltInScripts = await _scriptService.GetBuiltInScriptsAsync();
        UserScripts = await _scriptService.GetUserScriptsAsync(currentUserId);
        SharedScripts = await _scriptService.GetSharedScriptsAsync();
    }

    public async Task<IActionResult> OnPostUploadAsync(
        IFormFile scriptFile,
        string displayName,
        string description,
        string tags,
        string version)
    {
        try
        {
            // Validation
            if (scriptFile == null || scriptFile.Length == 0)
            {
                ModelState.AddModelError("", "Please select a file");
                return Page();
            }

            if (scriptFile.Length > 1048576) // 1 MB
            {
                ModelState.AddModelError("", "File size exceeds 1 MB limit");
                return Page();
            }

            if (!scriptFile.FileName.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Only Python (.py) files are allowed");
                return Page();
            }

            var currentUserId = GetCurrentUserId();

            // Read file content
            using var stream = scriptFile.OpenReadStream();
            using var reader = new StreamReader(stream);
            var scriptContent = await reader.ReadToEndAsync();

            // Create metadata
            var metadata = new AnalysisScriptMetadata
            {
                FileName = scriptFile.FileName,
                DisplayName = displayName,
                Description = description,
                Tags = tags?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(t => t.Trim())
                           .ToList() ?? new List<string>(),
                Version = version ?? "1.0.0",
                Language = ScriptLanguage.Python
            };

            // Upload and validate
            var result = await _scriptService.UploadScriptAsync(
                currentUserId,
                scriptContent,
                metadata
            );

            if (!result.Success)
            {
                ModelState.AddModelError("", $"Upload failed: {result.ErrorMessage}");
                return Page();
            }

            _logger.LogInformation(
                "User {UserId} uploaded script {FileName} (validation: {Status})",
                currentUserId, scriptFile.FileName, result.ValidationStatus
            );

            TempData["SuccessMessage"] = result.ValidationStatus == "passed"
                ? "Script uploaded and validated successfully!"
                : $"Script uploaded but validation failed: {string.Join(", ", result.ValidationErrors)}";

            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload script");
            ModelState.AddModelError("", "An error occurred during upload");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var success = await _scriptService.DeleteUserScriptAsync(currentUserId, id);

            if (success)
            {
                TempData["SuccessMessage"] = "Script deleted successfully";
            }
            else
            {
                TempData["ErrorMessage"] = "Script not found or access denied";
            }

            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete script {ScriptId}", id);
            TempData["ErrorMessage"] = "An error occurred while deleting the script";
            return RedirectToPage();
        }
    }

    private string GetCurrentUserId()
    {
        // TODO: Integrate with authentication system
        // For now, use a default user ID or session-based identifier
        return User.Identity?.Name ?? "default_user";
    }
}
```

#### 3.2 JavaScript for Script Management

**wwwroot/js/script-management.js:**
```javascript
// Preview script content when file is selected
function previewScript(input) {
    const file = input.files[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = function(e) {
        const content = e.target.result;
        document.getElementById('scriptPreview').textContent = content;
        document.getElementById('scriptPreviewContainer').style.display = 'block';

        // Auto-fill display name from filename if empty
        const displayNameInput = document.getElementById('displayName');
        if (!displayNameInput.value) {
            const fileName = file.name.replace('.py', '').replace(/_/g, ' ');
            displayNameInput.value = fileName.charAt(0).toUpperCase() + fileName.slice(1);
        }
    };
    reader.readAsText(file);
}

// Edit script metadata
function editScript(scriptId) {
    // Open edit modal with existing metadata
    fetch(`/Analysis/GetScriptMetadata?id=${scriptId}`)
        .then(response => response.json())
        .then(data => {
            // Populate edit form
            document.getElementById('editDisplayName').value = data.displayName;
            document.getElementById('editDescription').value = data.description;
            document.getElementById('editTags').value = data.tags.join(', ');
            document.getElementById('editVersion').value = data.version;
            document.getElementById('editScriptId').value = scriptId;

            // Show modal
            new bootstrap.Modal(document.getElementById('editModal')).show();
        });
}

// Handle form submission with validation feedback
document.getElementById('uploadForm')?.addEventListener('submit', async function(e) {
    e.preventDefault();

    const formData = new FormData(this);
    const submitButton = this.querySelector('button[type="submit"]');
    const validationResults = document.getElementById('validationResults');

    // Disable submit button
    submitButton.disabled = true;
    submitButton.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Uploading...';

    try {
        const response = await fetch(this.action, {
            method: 'POST',
            body: formData
        });

        const result = await response.json();

        if (result.success) {
            if (result.validationStatus === 'passed') {
                validationResults.className = 'alert alert-success';
                validationResults.innerHTML = '<i class="fas fa-check-circle"></i> Script uploaded and validated successfully!';
                validationResults.style.display = 'block';

                // Reload page after 1 second
                setTimeout(() => location.reload(), 1000);
            } else {
                validationResults.className = 'alert alert-warning';
                validationResults.innerHTML = `
                    <i class="fas fa-exclamation-triangle"></i>
                    Script uploaded but has validation warnings:
                    <ul class="mb-0 mt-2">
                        ${result.validationErrors.map(err => `<li>${err}</li>`).join('')}
                    </ul>
                `;
                validationResults.style.display = 'block';

                // Reload page after 3 seconds
                setTimeout(() => location.reload(), 3000);
            }
        } else {
            validationResults.className = 'alert alert-danger';
            validationResults.innerHTML = `<i class="fas fa-times-circle"></i> ${result.errorMessage}`;
            validationResults.style.display = 'block';
        }
    } catch (error) {
        validationResults.className = 'alert alert-danger';
        validationResults.innerHTML = `<i class="fas fa-times-circle"></i> An error occurred: ${error.message}`;
        validationResults.style.display = 'block';
    } finally {
        submitButton.disabled = false;
        submitButton.innerHTML = '<i class="fas fa-upload"></i> Upload & Validate';
    }
});
```

---

### **Phase 4: Dataset Viewing & Analysis Execution UI**

#### 4.1 Create ViewDataset Page (Updated with Script Selection)

**Pages/Data/ViewDataset.cshtml:**
```html
@page
@model ViewDatasetModel

<div class="container mt-4">
    <!-- Dataset Header -->
    <div class="card mb-4">
        <div class="card-header">
            <h2>@Model.Dataset.Name</h2>
        </div>
        <div class="card-body">
            <p><strong>Description:</strong> @Model.Dataset.Description</p>
            <p><strong>Created:</strong> @Model.Dataset.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")</p>
            <p><strong>Source:</strong> @Model.Dataset.DataSource</p>
            <p><strong>Data Points:</strong> @Model.DataPoints.Count</p>
        </div>
    </div>

    <!-- Analysis Section -->
    <div class="card mb-4">
        <div class="card-header">
            <h4>Run Analysis</h4>
        </div>
        <div class="card-body">
            <form method="post" asp-page-handler="RunAnalysis">
                <input type="hidden" asp-for="DatasetId" />

                <!-- Script Selection with Categories -->
                <div class="mb-3">
                    <label for="scriptSelect" class="form-label">Select Analysis Script</label>
                    <select id="scriptSelect" name="scriptId" class="form-select" required onchange="loadScriptDetails(this.value)">
                        <option value="">-- Choose Script --</option>
                        <optgroup label="Built-in Scripts">
                            @foreach (var script in Model.BuiltInScripts)
                            {
                                <option value="@script.Id" data-type="built-in">@script.DisplayName</option>
                            }
                        </optgroup>
                        <optgroup label="My Scripts">
                            @foreach (var script in Model.UserScripts)
                            {
                                <option value="@script.Id" data-type="user">@script.DisplayName</option>
                            }
                        </optgroup>
                    </select>
                </div>

                <!-- Script Description -->
                <div id="scriptDescription" class="alert alert-info" style="display:none;">
                    <h6 id="scriptDescTitle"></h6>
                    <p id="scriptDescText" class="mb-0"></p>
                </div>

                <!-- Manage Scripts Link -->
                <div class="mb-3">
                    <a href="/Analysis/ManageScripts" class="btn btn-sm btn-outline-secondary">
                        <i class="fas fa-cog"></i> Manage My Scripts
                    </a>
                </div>

                <!-- Dynamic Parameters (populated via JavaScript) -->
                <div id="parametersContainer"></div>

                <button type="submit" class="btn btn-primary">Run Analysis</button>
            </form>
        </div>
    </div>

    <!-- Analysis Results History -->
    @if (Model.AnalysisHistory.Count > 0)
    {
        <div class="card">
            <div class="card-header">
                <h4>Analysis Results</h4>
            </div>
            <div class="card-body">
                @foreach (var result in Model.AnalysisHistory)
                {
                    <div class="analysis-result mb-4">
                        <h5>@result.ScriptName - @result.ExecutionDate.ToString("yyyy-MM-dd HH:mm")</h5>

                        @if (result.Status == "Success")
                        {
                            <!-- Display Image -->
                            <img src="/@result.ResultImagePath"
                                 alt="Analysis Result"
                                 class="img-fluid mb-3"
                                 style="max-width: 100%; height: auto;" />

                            <!-- Display Statistics -->
                            @if (!string.IsNullOrEmpty(result.ResultDataJson))
                            {
                                <div class="stats-container">
                                    <h6>Statistics:</h6>
                                    <pre>@result.ResultDataJson</pre>
                                </div>
                            }
                        }
                        else
                        {
                            <div class="alert alert-danger">
                                <strong>Error:</strong> @result.ErrorMessage
                            </div>
                        }

                        <!-- Delete Button -->
                        <form method="post" asp-page-handler="DeleteResult" asp-route-resultId="@result.Id" style="display:inline;">
                            <button type="submit" class="btn btn-sm btn-danger">Delete</button>
                        </form>
                        <hr />
                    </div>
                }
            </div>
        </div>
    }

    <!-- Back Button -->
    <a href="/Data/DataIndex" class="btn btn-secondary mt-3">Back to Datasets</a>
</div>
```

**Pages/Data/ViewDataset.cshtml.cs:**
```csharp
public class ViewDatasetModel : PageModel
{
    private readonly IDataService _dataService;
    private readonly IAnalysisService _analysisService;
    private readonly ILogger<ViewDatasetModel> _logger;

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public DatasetDetail Dataset { get; set; }
    public List<DataPoint> DataPoints { get; set; }
    public List<AnalysisScript> AvailableScripts { get; set; }
    public List<AnalysisResult> AnalysisHistory { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Dataset = await _dataService.GetDatasetAsync(Id);
        if (Dataset == null) return NotFound();

        DataPoints = await _dataService.GetDataPointsAsync(Id);
        AvailableScripts = await _analysisService.GetAvailableScriptsAsync();
        AnalysisHistory = await _analysisService.GetAnalysisHistoryAsync(Id);

        return Page();
    }

    public async Task<IActionResult> OnPostRunAnalysisAsync(
        string scriptName,
        Dictionary<string, object> parameters)
    {
        var result = await _analysisService.ExecuteAnalysisAsync(
            Id,
            scriptName,
            parameters
        );

        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostDeleteResultAsync(Guid resultId)
    {
        await _analysisService.DeleteAnalysisResultAsync(resultId);
        return RedirectToPage(new { id = Id });
    }
}
```

#### 3.2 JavaScript for Dynamic Parameters

**wwwroot/js/analysis.js:**
```javascript
// Dynamically load script parameters when user selects a script
document.getElementById('scriptSelect').addEventListener('change', async (e) => {
    const scriptName = e.target.value;

    const response = await fetch(`/api/analysis/scripts/${scriptName}/parameters`);
    const parameters = await response.json();

    const container = document.getElementById('parametersContainer');
    container.innerHTML = '';

    parameters.forEach(param => {
        const input = createParameterInput(param);
        container.appendChild(input);
    });
});

function createParameterInput(param) {
    const div = document.createElement('div');
    div.className = 'mb-3';

    const label = document.createElement('label');
    label.textContent = param.displayName;
    label.className = 'form-label';

    let input;
    if (param.type === 'number') {
        input = document.createElement('input');
        input.type = 'number';
        input.value = param.defaultValue;
    } else if (param.type === 'boolean') {
        input = document.createElement('input');
        input.type = 'checkbox';
        input.checked = param.defaultValue;
    } else {
        input = document.createElement('input');
        input.type = 'text';
        input.value = param.defaultValue;
    }

    input.name = `parameters[${param.name}]`;
    input.className = 'form-control';

    div.appendChild(label);
    div.appendChild(input);

    return div;
}
```

---

### **Phase 5: Script Management & Validation Service**

#### 5.1 IScriptManagementService Interface

**Domains/Analysis/Services/IScriptManagementService.cs:**
```csharp
public interface IScriptManagementService
{
    // Discovery
    Task<List<AnalysisScriptMetadata>> GetBuiltInScriptsAsync();
    Task<List<AnalysisScriptMetadata>> GetUserScriptsAsync(string userId);
    Task<List<AnalysisScriptMetadata>> GetSharedScriptsAsync();
    Task<AnalysisScriptMetadata> GetScriptMetadataAsync(Guid scriptId);

    // Upload & Management
    Task<ScriptUploadResult> UploadScriptAsync(
        string userId,
        string scriptContent,
        AnalysisScriptMetadata metadata
    );
    Task<bool> UpdateScriptMetadataAsync(Guid scriptId, AnalysisScriptMetadata metadata);
    Task<bool> DeleteUserScriptAsync(string userId, Guid scriptId);

    // Execution
    Task<string> GetScriptContentAsync(Guid scriptId);
    Task<string> GetScriptPathAsync(Guid scriptId);
}

public class ScriptUploadResult
{
    public bool Success { get; set; }
    public Guid? ScriptId { get; set; }
    public string ValidationStatus { get; set; }  // "passed", "warning", "failed"
    public List<string> ValidationErrors { get; set; } = new();
    public string ErrorMessage { get; set; }
}
```

#### 5.2 Script Validation Service

**Domains/Analysis/Services/IScriptValidationService.cs:**
```csharp
public interface IScriptValidationService
{
    Task<ValidationResult> ValidateScriptAsync(string scriptContent, ScriptLanguage language);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string Status { get; set; }  // "passed", "warning", "failed"
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
}

public class ValidationError
{
    public string Code { get; set; }
    public string Message { get; set; }
    public int? LineNumber { get; set; }
}

public class ValidationWarning
{
    public string Code { get; set; }
    public string Message { get; set; }
    public int? LineNumber { get; set; }
}
```

**Domains/Analysis/Services/ScriptValidationService.cs:**
```csharp
public class ScriptValidationService : IScriptValidationService
{
    private readonly IPlatformHelper _platformHelper;
    private readonly ILogger<ScriptValidationService> _logger;

    // Dangerous patterns to detect
    private readonly Dictionary<ScriptLanguage, List<string>> _dangerousPatterns = new()
    {
        {
            ScriptLanguage.Python, new List<string>
            {
                "os.system",
                "subprocess.call",
                "subprocess.run",
                "subprocess.Popen",
                "eval(",
                "exec(",
                "__import__",
                "compile(",
                "open(",  // Warning only - might be needed
                "file(",
                "input(",  // Scripts should use stdin
                "raw_input("
            }
        }
    };

    public async Task<ValidationResult> ValidateScriptAsync(
        string scriptContent,
        ScriptLanguage language)
    {
        var result = new ValidationResult { IsValid = true, Status = "passed" };

        // 1. Basic checks
        if (string.IsNullOrWhiteSpace(scriptContent))
        {
            result.IsValid = false;
            result.Status = "failed";
            result.Errors.Add(new ValidationError
            {
                Code = "EMPTY_SCRIPT",
                Message = "Script content is empty"
            });
            return result;
        }

        // 2. Check for dangerous patterns
        if (language == ScriptLanguage.Python)
        {
            await ValidatePythonScript(scriptContent, result);
        }

        // 3. Syntax check (if interpreter available)
        if (_platformHelper.IsWindows() || _platformHelper.IsLinux())
        {
            await CheckSyntax(scriptContent, language, result);
        }

        // 4. Check for required imports/libraries
        ValidateRequiredStructure(scriptContent, language, result);

        // Update final status
        if (result.Errors.Count > 0)
        {
            result.IsValid = false;
            result.Status = "failed";
        }
        else if (result.Warnings.Count > 0)
        {
            result.Status = "warning";
        }

        return result;
    }

    private async Task ValidatePythonScript(string scriptContent, ValidationResult result)
    {
        var lines = scriptContent.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Skip comments
            if (line.StartsWith("#")) continue;

            // Check dangerous patterns
            if (_dangerousPatterns[ScriptLanguage.Python].Any(pattern => line.Contains(pattern)))
            {
                var dangerousPattern = _dangerousPatterns[ScriptLanguage.Python]
                    .First(pattern => line.Contains(pattern));

                if (dangerousPattern == "open(")
                {
                    result.Warnings.Add(new ValidationWarning
                    {
                        Code = "FILE_ACCESS",
                        Message = "Script uses file I/O. Ensure proper error handling.",
                        LineNumber = i + 1
                    });
                }
                else
                {
                    result.Errors.Add(new ValidationError
                    {
                        Code = "DANGEROUS_OPERATION",
                        Message = $"Dangerous operation detected: {dangerousPattern}",
                        LineNumber = i + 1
                    });
                }
            }
        }
    }

    private async Task CheckSyntax(
        string scriptContent,
        ScriptLanguage language,
        ValidationResult result)
    {
        if (language != ScriptLanguage.Python) return;

        try
        {
            // Create temporary file
            var tempFile = Path.GetTempFileName() + ".py";
            await File.WriteAllTextAsync(tempFile, scriptContent);

            var pythonCmd = _platformHelper.GetPythonCommand();

            var psi = new ProcessStartInfo
            {
                FileName = pythonCmd,
                Arguments = $"-m py_compile \"{tempFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                result.Errors.Add(new ValidationError
                {
                    Code = "SYNTAX_ERROR",
                    Message = $"Python syntax error: {error}"
                });
            }

            // Clean up
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not perform syntax check");
            // Don't fail validation if syntax check fails
        }
    }

    private void ValidateRequiredStructure(
        string scriptContent,
        ScriptLanguage language,
        ValidationResult result)
    {
        if (language == ScriptLanguage.Python)
        {
            // Check for required imports
            if (!scriptContent.Contains("import json"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "MISSING_IMPORT",
                    Message = "Script should import 'json' module for I/O"
                });
            }

            if (!scriptContent.Contains("import sys"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "MISSING_IMPORT",
                    Message = "Script should import 'sys' module for stdin/stdout"
                });
            }

            // Check for main execution pattern
            if (!scriptContent.Contains("if __name__"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "MISSING_MAIN_GUARD",
                    Message = "Script should use 'if __name__ == \"__main__\":' guard"
                });
            }

            // Check for JSON output
            if (!scriptContent.Contains("json.dumps") && !scriptContent.Contains("toJSON"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "MISSING_JSON_OUTPUT",
                    Message = "Script should output results as JSON"
                });
            }
        }
    }
}
```

#### 5.3 Script Management Service Implementation

**Domains/Analysis/Services/ScriptManagementService.cs:**
```csharp
public class ScriptManagementService : IScriptManagementService
{
    private readonly IOptions<AnalysisOptions> _options;
    private readonly IScriptValidationService _validationService;
    private readonly ILogger<ScriptManagementService> _logger;

    public async Task<List<AnalysisScriptMetadata>> GetBuiltInScriptsAsync()
    {
        var scriptsDir = Path.Combine(_options.Value.ScriptDirectory, "built-in", "python");

        if (!Directory.Exists(scriptsDir))
            return new List<AnalysisScriptMetadata>();

        var scripts = new List<AnalysisScriptMetadata>();

        foreach (var file in Directory.GetFiles(scriptsDir, "*.py"))
        {
            var metadata = await ParseScriptMetadata(file, isBuiltIn: true);
            scripts.Add(metadata);
        }

        return scripts;
    }

    public async Task<List<AnalysisScriptMetadata>> GetUserScriptsAsync(string userId)
    {
        var userDir = Path.Combine(_options.Value.ScriptDirectory, "user-uploads", userId);

        if (!Directory.Exists(userDir))
            return new List<AnalysisScriptMetadata>();

        // Load from metadata cache
        var metadataFile = Path.Combine(userDir, ".metadata.json");

        if (File.Exists(metadataFile))
        {
            var json = await File.ReadAllTextAsync(metadataFile);
            var cache = JsonSerializer.Deserialize<MetadataCache>(json);
            return cache?.Scripts ?? new List<AnalysisScriptMetadata>();
        }

        return new List<AnalysisScriptMetadata>();
    }

    public async Task<ScriptUploadResult> UploadScriptAsync(
        string userId,
        string scriptContent,
        AnalysisScriptMetadata metadata)
    {
        try
        {
            // 1. Validate script
            var validation = await _validationService.ValidateScriptAsync(
                scriptContent,
                metadata.Language
            );

            // 2. Create user directory if needed
            var userDir = Path.Combine(_options.Value.ScriptDirectory, "user-uploads", userId);
            Directory.CreateDirectory(userDir);

            // 3. Generate unique filename
            metadata.Id = Guid.NewGuid();
            var safeFileName = GenerateSafeFileName(metadata.FileName);
            var filePath = Path.Combine(userDir, safeFileName);

            // 4. Save script file
            await File.WriteAllTextAsync(filePath, scriptContent);

            // 5. Update metadata
            metadata.UploadDate = DateTime.UtcNow;
            metadata.LastModified = DateTime.UtcNow;
            metadata.ValidationStatus = validation.Status;
            metadata.ValidationErrors = validation.Errors.Select(e => e.Message).ToList();

            // 6. Update metadata cache
            await UpdateMetadataCache(userDir, metadata);

            _logger.LogInformation(
                "Script {FileName} uploaded for user {UserId} (status: {Status})",
                metadata.FileName, userId, validation.Status
            );

            return new ScriptUploadResult
            {
                Success = true,
                ScriptId = metadata.Id,
                ValidationStatus = validation.Status,
                ValidationErrors = validation.Errors.Select(e => e.Message).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload script for user {UserId}", userId);
            return new ScriptUploadResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task UpdateMetadataCache(string userDir, AnalysisScriptMetadata metadata)
    {
        var metadataFile = Path.Combine(userDir, ".metadata.json");

        MetadataCache cache;

        if (File.Exists(metadataFile))
        {
            var json = await File.ReadAllTextAsync(metadataFile);
            cache = JsonSerializer.Deserialize<MetadataCache>(json) ?? new MetadataCache();
        }
        else
        {
            cache = new MetadataCache();
        }

        // Update or add script metadata
        var existing = cache.Scripts.FindIndex(s => s.Id == metadata.Id);
        if (existing >= 0)
        {
            cache.Scripts[existing] = metadata;
        }
        else
        {
            cache.Scripts.Add(metadata);
        }

        // Save cache
        var updatedJson = JsonSerializer.Serialize(cache, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(metadataFile, updatedJson);
    }

    private string GenerateSafeFileName(string fileName)
    {
        // Remove invalid characters
        var invalid = Path.GetInvalidFileNameChars();
        var safeName = string.Join("_", fileName.Split(invalid));

        // Ensure .py extension
        if (!safeName.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
        {
            safeName += ".py";
        }

        return safeName;
    }

    private async Task<AnalysisScriptMetadata> ParseScriptMetadata(
        string filePath,
        bool isBuiltIn)
    {
        // Parse metadata from script comments/docstrings
        // For built-in scripts, this would extract documentation
        var content = await File.ReadAllTextAsync(filePath);

        return new AnalysisScriptMetadata
        {
            Id = Guid.NewGuid(),
            FileName = Path.GetFileName(filePath),
            DisplayName = Path.GetFileNameWithoutExtension(filePath),
            Language = ScriptLanguage.Python,
            ValidationStatus = "passed"
        };
    }
}

public class MetadataCache
{
    public List<AnalysisScriptMetadata> Scripts { get; set; } = new();
}
```

---

### **Phase 6: Cross-Platform Service Implementation**

#### 4.1 PlatformHelper.cs (Platform Detection)

```csharp
using System.Runtime.InteropServices;

public interface IPlatformHelper
{
    bool IsWindows();
    bool IsLinux();
    bool IsMacOS();
    string GetPythonCommand();
    string GetRCommand();
    string GetShellCommand();
}

public class PlatformHelper : IPlatformHelper
{
    public bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public bool IsMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public string GetPythonCommand()
    {
        if (IsWindows())
        {
            // Try python first, then py launcher
            return CheckCommandExists("python") ? "python" :
                   CheckCommandExists("py") ? "py" : "python";
        }
        else
        {
            // Linux/macOS: prefer python3
            return CheckCommandExists("python3") ? "python3" : "python";
        }
    }

    public string GetRCommand()
    {
        return "Rscript";  // Same on all platforms
    }

    public string GetShellCommand()
    {
        return IsWindows() ? "cmd" : "/bin/bash";
    }

    private bool CheckCommandExists(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = IsWindows() ? "where" : "which",
                Arguments = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
```

#### 4.2 IScriptExecutor Interface

```csharp
public enum ScriptLanguage
{
    Python,
    R,
    Shell,
    Unknown
}

public interface IScriptExecutor
{
    ScriptLanguage SupportedLanguage { get; }

    Task<ProcessResult> ExecuteScriptAsync(
        string scriptPath,
        string inputJson,
        string outputDirectory,
        Dictionary<string, object> parameters,
        int timeoutSeconds = 300
    );

    bool IsInterpreterAvailable();
}

public class ProcessResult
{
    public bool Success { get; set; }
    public string Output { get; set; }
    public string ErrorMessage { get; set; }
    public int ExitCode { get; set; }
    public int ExecutionTimeMs { get; set; }
}
```

#### 4.3 PythonScriptExecutor.cs (Cross-Platform)

```csharp
public class PythonScriptExecutor : IScriptExecutor
{
    private readonly IPlatformHelper _platformHelper;
    private readonly ILogger<PythonScriptExecutor> _logger;

    public ScriptLanguage SupportedLanguage => ScriptLanguage.Python;

    public PythonScriptExecutor(
        IPlatformHelper platformHelper,
        ILogger<PythonScriptExecutor> logger)
    {
        _platformHelper = platformHelper;
        _logger = logger;
    }

    public bool IsInterpreterAvailable()
    {
        try
        {
            var pythonCmd = _platformHelper.GetPythonCommand();
            var psi = new ProcessStartInfo
            {
                FileName = pythonCmd,
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Python interpreter not found");
            return false;
        }
    }

    public async Task<ProcessResult> ExecuteScriptAsync(
        string scriptPath,
        string inputJson,
        string outputDirectory,
        Dictionary<string, object> parameters,
        int timeoutSeconds = 300)
    {
        var pythonCmd = _platformHelper.GetPythonCommand();

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonCmd,
            Arguments = $"\"{scriptPath}\" \"{outputDirectory}\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // Set UTF-8 encoding for cross-platform compatibility
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // Add environment variables for better cross-platform support
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        // Linux-specific: Ensure matplotlib uses non-GUI backend
        if (_platformHelper.IsLinux())
        {
            startInfo.Environment["MPLBACKEND"] = "Agg";
        }

        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (s, e) => {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (s, e) => {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        var stopwatch = Stopwatch.StartNew();
        process.Start();

        // Write input to stdin
        await process.StandardInput.WriteAsync(inputJson);
        process.StandardInput.Close();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        bool completed = await Task.Run(() =>
            process.WaitForExit(timeoutSeconds * 1000)
        );

        stopwatch.Stop();

        if (!completed)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill timed-out process");
            }

            return new ProcessResult
            {
                Success = false,
                ErrorMessage = $"Script execution timed out after {timeoutSeconds} seconds",
                ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }

        // Wait for async output to complete
        await Task.Delay(100);

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        return new ProcessResult
        {
            Success = process.ExitCode == 0,
            Output = output,
            ErrorMessage = error,
            ExitCode = process.ExitCode,
            ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds
        };
    }
}
```

#### 4.4 RScriptExecutor.cs (Cross-Platform)

```csharp
public class RScriptExecutor : IScriptExecutor
{
    private readonly IPlatformHelper _platformHelper;
    private readonly ILogger<RScriptExecutor> _logger;

    public ScriptLanguage SupportedLanguage => ScriptLanguage.R;

    public RScriptExecutor(
        IPlatformHelper platformHelper,
        ILogger<RScriptExecutor> logger)
    {
        _platformHelper = platformHelper;
        _logger = logger;
    }

    public bool IsInterpreterAvailable()
    {
        try
        {
            var rCmd = _platformHelper.GetRCommand();
            var psi = new ProcessStartInfo
            {
                FileName = rCmd,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "R interpreter not found");
            return false;
        }
    }

    public async Task<ProcessResult> ExecuteScriptAsync(
        string scriptPath,
        string inputJson,
        string outputDirectory,
        Dictionary<string, object> parameters,
        int timeoutSeconds = 300)
    {
        var rCmd = _platformHelper.GetRCommand();

        var startInfo = new ProcessStartInfo
        {
            FileName = rCmd,
            Arguments = $"--vanilla \"{scriptPath}\" \"{outputDirectory}\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // R-specific environment variables
        startInfo.Environment["R_LIBS_USER"] = Environment.GetEnvironmentVariable("R_LIBS_USER") ?? "";

        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (s, e) => {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (s, e) => {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        var stopwatch = Stopwatch.StartNew();
        process.Start();

        // Write input to stdin
        await process.StandardInput.WriteAsync(inputJson);
        process.StandardInput.Close();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        bool completed = await Task.Run(() =>
            process.WaitForExit(timeoutSeconds * 1000)
        );

        stopwatch.Stop();

        if (!completed)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill timed-out R process");
            }

            return new ProcessResult
            {
                Success = false,
                ErrorMessage = $"R script execution timed out after {timeoutSeconds} seconds",
                ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }

        await Task.Delay(100);

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        return new ProcessResult
        {
            Success = process.ExitCode == 0,
            Output = output,
            ErrorMessage = error,
            ExitCode = process.ExitCode,
            ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds
        };
    }
}
```

#### 4.5 ScriptExecutorFactory.cs

```csharp
public class ScriptExecutorFactory
{
    private readonly IEnumerable<IScriptExecutor> _executors;
    private readonly ILogger<ScriptExecutorFactory> _logger;

    public ScriptExecutorFactory(
        IEnumerable<IScriptExecutor> executors,
        ILogger<ScriptExecutorFactory> logger)
    {
        _executors = executors;
        _logger = logger;
    }

    public IScriptExecutor GetExecutor(ScriptLanguage language)
    {
        var executor = _executors.FirstOrDefault(e => e.SupportedLanguage == language);

        if (executor == null)
        {
            throw new NotSupportedException($"No executor found for language: {language}");
        }

        if (!executor.IsInterpreterAvailable())
        {
            _logger.LogWarning("Interpreter for {Language} is not available", language);
        }

        return executor;
    }

    public IScriptExecutor GetExecutorForFile(string scriptPath)
    {
        var extension = Path.GetExtension(scriptPath).ToLowerInvariant();

        var language = extension switch
        {
            ".py" => ScriptLanguage.Python,
            ".r" => ScriptLanguage.R,
            ".sh" => ScriptLanguage.Shell,
            _ => ScriptLanguage.Unknown
        };

        if (language == ScriptLanguage.Unknown)
        {
            throw new NotSupportedException($"Unsupported script extension: {extension}");
        }

        return GetExecutor(language);
    }

    public Dictionary<ScriptLanguage, bool> GetAvailableLanguages()
    {
        return _executors.ToDictionary(
            e => e.SupportedLanguage,
            e => e.IsInterpreterAvailable()
        );
    }
}
```

---

### **Phase 5: Configuration & Deployment**

#### 5.1 Add to Program.cs
```csharp
// Register platform helper (reuse existing from Device domain if available)
builder.Services.AddSingleton<IPlatformHelper, PlatformHelper>();

// Register script executors
builder.Services.AddSingleton<IScriptExecutor, PythonScriptExecutor>();
builder.Services.AddSingleton<IScriptExecutor, RScriptExecutor>();

// Register factory
builder.Services.AddSingleton<ScriptExecutorFactory>();

// Register analysis service
builder.Services.AddScoped<IAnalysisService, AnalysisService>();

// Configure script directories
builder.Services.Configure<AnalysisOptions>(options =>
{
    options.ScriptDirectory = Path.Combine(
        builder.Environment.ContentRootPath,
        "analysis-scripts"
    );
    options.OutputDirectory = Path.Combine(
        builder.Environment.WebRootPath,
        "analysis-results"
    );
    options.MaxExecutionTimeSeconds = 300;
    options.MaxOutputFileSizeMB = 10;
});
```

#### 5.2 Cross-Platform Environment Setup

**Python Requirements (requirements.txt):**
```
matplotlib>=3.5.0
numpy>=1.21.0
pandas>=1.3.0
scipy>=1.7.0
seaborn>=0.11.0
```

**R Requirements (install_packages.R):**
```r
# Install required R packages
packages <- c("jsonlite", "ggplot2", "tidyverse", "gridExtra")
install.packages(packages, repos = "https://cloud.r-project.org/")
```

**Installation Instructions:**

**Windows:**
```powershell
# Python setup
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt

# R setup (after installing R from CRAN)
Rscript install_packages.R
```

**Linux (Ubuntu/Debian):**
```bash
# Install system dependencies
sudo apt-get update
sudo apt-get install -y python3 python3-pip python3-venv r-base

# Python setup
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt

# R setup
sudo Rscript install_packages.R
```

**Linux (RHEL/CentOS/Fedora):**
```bash
# Install system dependencies
sudo dnf install -y python3 python3-pip R

# Python setup
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt

# R setup
sudo Rscript install_packages.R
```

**macOS:**
```bash
# Install Homebrew if not installed
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

# Install dependencies
brew install python3 r

# Python setup
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt

# R setup
Rscript install_packages.R
```

#### 5.3 Linux-Specific Considerations

**Matplotlib Backend Configuration:**

For headless Linux servers (no display), create `.config/matplotlib/matplotlibrc`:
```
backend: Agg
```

Or set environment variable in the application:
```csharp
// In PythonScriptExecutor (already included above)
if (_platformHelper.IsLinux())
{
    startInfo.Environment["MPLBACKEND"] = "Agg";
}
```

**File Permissions:**

Ensure script files are executable on Linux:
```bash
chmod +x analysis-scripts/**/*.py
chmod +x analysis-scripts/**/*.R
```

**Service Configuration (systemd):**

If running as a systemd service on Linux:
```ini
[Unit]
Description=SmartLab Analysis Service

[Service]
WorkingDirectory=/path/to/project_smart_lab
Environment="ASPNETCORE_ENVIRONMENT=Production"
Environment="PYTHONIOENCODING=utf-8"
Environment="MPLBACKEND=Agg"
ExecStart=/usr/bin/dotnet run

[Install]
WantedBy=multi-user.target
```

---

## Security Considerations

### 1. **Script Sandboxing**
- Scripts run as separate processes with timeout limits
- No direct file system access (only designated output folder)
- Input validation before execution

### 2. **File Upload Security**
- Only allow script files from trusted sources (`.py`, `.R`, `.sh`)
- Validate file extensions and MIME types
- Scan scripts for dangerous patterns:
  - Python: `os.system`, `subprocess.call`, `eval`, `exec`, `__import__`
  - R: `system`, `system2`, `shell`, `source` (from untrusted paths)
- Store user scripts in isolated `custom/` folder
- Linux: Ensure scripts don't have setuid/setgid bits

### 3. **Resource Limits**
- Execution timeout: 5 minutes default
- Memory limits via process constraints (Linux: use cgroups if available)
- Max output file size: 10 MB
- Concurrent execution limits (prevent DoS)

### 4. **Platform-Specific Security**
**Linux:**
- Run scripts as non-privileged user (consider separate service account)
- Use seccomp or AppArmor profiles for additional sandboxing
- Disable ptrace to prevent debugging/injection attacks
- Mount output directory with `noexec` flag if possible

**Windows:**
- Run under restricted service account
- Use Windows Sandbox API for additional isolation (optional)

---

## Testing Strategy

### Unit Tests
1. PlatformHelper - OS detection and command resolution
2. PythonScriptExecutor with mock scripts (Windows/Linux)
3. RScriptExecutor with mock scripts (Windows/Linux)
4. ScriptExecutorFactory - language detection and selection
5. AnalysisService data transformation
6. Script parameter parsing

### Integration Tests
1. End-to-end Python script execution (both platforms)
2. End-to-end R script execution (both platforms)
3. Database persistence
4. File cleanup
5. Timeout handling
6. Error propagation

### Cross-Platform Testing
1. **Windows:**
   - Test with `python` and `py` launcher
   - Test with R from CRAN
   - Verify path handling with backslashes

2. **Linux:**
   - Test with `python3` command
   - Test matplotlib backend (Agg)
   - Verify UNIX path handling
   - Test file permissions

3. **Manual Testing:**
   - Run built-in Python scripts on sample datasets
   - Run built-in R scripts on sample datasets
   - Upload custom scripts and verify execution
   - Test error handling (syntax errors, timeouts, missing interpreters)
   - Test on WSL (Windows Subsystem for Linux)

---

## Future Enhancements

### Phase 6 (Optional):
1. **Script Marketplace** - Share scripts between users
2. **Batch Analysis** - Run same script on multiple datasets
3. **Real-time Preview** - Stream execution progress via WebSockets
4. **Additional Language Support** - Julia, MATLAB, Octave
5. **Interactive Parameters** - Sliders, color pickers, file uploads
6. **Export Results** - Download images + data as ZIP
7. **Docker Sandboxing** - Run scripts in isolated containers
8. **Script Versioning** - Git integration for script management
9. **Scheduled Analysis** - Cron-like scheduled script execution
10. **Collaborative Annotations** - Comments on analysis results

---

## File Structure Summary

```
project_smart_lab/
├── Domains/Analysis/           [NEW]
│   ├── Services/
│   │   ├── IAnalysisService.cs
│   │   ├── AnalysisService.cs
│   │   ├── IScriptExecutor.cs
│   │   ├── PythonScriptExecutor.cs
│   │   ├── RScriptExecutor.cs
│   │   └── ScriptExecutorFactory.cs
│   ├── Models/
│   │   ├── AnalysisScript.cs
│   │   ├── AnalysisResult.cs
│   │   ├── ScriptLanguage.cs
│   │   └── ProcessResult.cs
│   ├── Platform/
│   │   ├── IPlatformHelper.cs
│   │   └── PlatformHelper.cs
│   └── Database/
│       └── AnalysisResultEntity.cs
├── Pages/Data/
│   ├── ViewDataset.cshtml      [NEW]
│   └── ViewDataset.cshtml.cs   [NEW]
├── wwwroot/
│   ├── js/analysis.js          [NEW]
│   └── analysis-results/       [NEW - generated images]
├── analysis-scripts/           [NEW]
│   ├── _templates/
│   │   ├── python/
│   │   │   ├── template.py
│   │   │   └── README.md
│   │   └── r/
│   │       ├── template.R
│   │       └── README.md
│   ├── python/
│   │   ├── basic_plot.py
│   │   ├── histogram.py
│   │   └── scatter_plot.py
│   ├── r/
│   │   ├── ggplot_timeseries.R
│   │   ├── statistical_summary.R
│   │   └── correlation_matrix.R
│   └── custom/
│       ├── python/
│       └── r/
├── Migrations/
│   └── *_AddAnalysisResults.cs [NEW]
├── requirements.txt            [NEW - Python]
└── install_packages.R          [NEW - R]
```

---

## Implementation Summary

This strategy provides a complete, production-ready, **cross-platform** implementation that:

### Architecture Benefits
- **Cross-Platform:** Full Windows and Linux support with automatic platform detection
- **Multi-Language:** Support for Python, R, and extensible to other languages
- **Existing Patterns:** Leverages existing UI (View button) and reuses ProxyDevice process management patterns
- **DDD Architecture:** Follows your Domain-Driven Design pattern with clear separation of concerns
- **Scalability:** Factory pattern allows easy addition of new script languages
- **Security:** Comprehensive sandboxing and resource limits

### Platform Compatibility Matrix

| Feature | Windows | Linux | macOS |
|---------|---------|-------|-------|
| Python Scripts | ✅ | ✅ | ✅ |
| R Scripts | ✅ | ✅ | ✅ |
| Matplotlib (headless) | ✅ | ✅ (Agg backend) | ✅ |
| Path handling | ✅ | ✅ | ✅ |
| Process timeout | ✅ | ✅ | ✅ |
| UTF-8 encoding | ✅ | ✅ | ✅ |

### User Capabilities
1. View dataset details and data points
2. Select from available Python or R analysis scripts
3. Configure script parameters dynamically
4. Execute scripts on dataset data (cross-platform)
5. View generated visualizations inline
6. Review analysis history per dataset
7. Delete old analysis results
8. Add custom scripts in multiple languages

### Next Steps

**Phase 1:** Implement Core Infrastructure
- Create Analysis domain layer with platform abstraction
- Add database migration for AnalysisResults table
- Implement PlatformHelper for OS detection

**Phase 2:** Implement Script Executors
- Build PythonScriptExecutor with cross-platform support
- Build RScriptExecutor with cross-platform support
- Create ScriptExecutorFactory

**Phase 3:** Build UI
- Create ViewDataset Razor page
- Add JavaScript for dynamic parameters
- Update existing DataIndex to link to ViewDataset

**Phase 4:** Testing
- Unit tests for all executors on both platforms
- Integration tests with real Python/R scripts
- Cross-platform validation (Windows + WSL/Linux)

**Phase 5:** Deployment
- Document installation procedures for each platform
- Create setup scripts for Python/R environments
- Configure systemd service for Linux production deployment
