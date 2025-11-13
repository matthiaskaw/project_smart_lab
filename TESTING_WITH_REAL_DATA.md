# Testing Analysis Scripts with Real Data

The test script supports loading real data from files, making it easy to debug your analysis scripts with actual measurement data downloaded from SmartLab.

## Quick Start

### Option 1: Test with Synthetic Data (Default)

```bash
python test_my_script.py my_analysis.py
```

### Option 2: Test with Downloaded Data from SmartLab

```bash
# 1. Download a dataset from SmartLab web interface
#    (Navigate to Data -> Click download button on a dataset)

# 2. Test your script with the downloaded file
python test_my_script.py my_analysis.py downloaded_data.txt
```

## Supported File Formats

The test script automatically detects and parses multiple formats:

### Format 1: CSV/TXT (Downloaded from SmartLab)

```csv
timestamp,value,unit
2025-11-13T08:00:00Z,22.5,°C
2025-11-13T09:00:00Z,23.1,°C
2025-11-13T10:00:00Z,24.3,°C
```

- Column names are case-insensitive
- Accepts: `timestamp`/`time`/`date`, `value`/`val`/`measurement`, `unit`/`units`
- This is the format you get when downloading from SmartLab

### Format 2: JSON - Simple Array

```json
[
  {"timestamp": "2025-11-13T08:00:00Z", "value": 22.5},
  {"timestamp": "2025-11-13T09:00:00Z", "value": 23.1}
]
```

- Quick and easy for manual testing
- Auto-generates dataset name from filename

### Format 3: JSON - Complete SmartLab Format

```json
{
  "datasetId": "my-dataset-123",
  "datasetName": "Office Temperature",
  "dataPoints": [
    {"timestamp": "2025-11-13T08:00:00Z", "value": 22.5, "unit": "°C"}
  ],
  "parameters": {
    "threshold": 25.0,
    "window_size": 5
  }
}
```

- Full control over dataset name and parameters
- Useful for testing scripts that use custom parameters

## Complete Workflow

### 1. Download Data from SmartLab

1. Open SmartLab in your browser
2. Navigate to **Data** section
3. Find the dataset you want to test with
4. Click the **Download** button
5. Save the file (e.g., `Temperature_20251113.txt`)

### 2. Test Your Script

```bash
python test_my_script.py my_analysis.py Temperature_20251113.txt
```

### 3. View Results

- Check console output for statistics and errors
- View generated images in `test_output/` directory
- Iterate on your script until it works correctly

### 4. Upload to SmartLab

Once your script works locally, upload it through the SmartLab web interface.

## Example Workflows

### Workflow A: Debug with Real Production Data

```bash
# 1. Download dataset from SmartLab that's causing issues
#    (Use the download button in the web interface)

# 2. Test your script with it
python test_my_script.py my_analysis.py problem_dataset.txt

# 3. Fix issues in your script

# 4. Test again until it works
python test_my_script.py my_analysis.py problem_dataset.txt
```

### Workflow B: Create Custom Test Data

Create `custom_test.json`:
```json
[
  {"timestamp": "2025-11-13T10:00:00Z", "value": 100},
  {"timestamp": "2025-11-13T11:00:00Z", "value": 200},
  {"timestamp": "2025-11-13T12:00:00Z", "value": 150}
]
```

Test:
```bash
python test_my_script.py my_analysis.py custom_test.json
```

### Workflow C: Test with Parameters

Create `data_with_params.json`:
```json
{
  "datasetName": "Test Data",
  "dataPoints": [
    {"timestamp": "2025-11-13T10:00:00Z", "value": 25.5},
    {"timestamp": "2025-11-13T11:00:00Z", "value": 26.1}
  ],
  "parameters": {
    "threshold": 26.0,
    "smoothing_window": 3,
    "enable_outlier_detection": true
  }
}
```

Access parameters in your script:
```python
def analyze_data(data_points, parameters):
    threshold = parameters.get('threshold', 25.0)
    window = parameters.get('smoothing_window', 5)
    detect_outliers = parameters.get('enable_outlier_detection', False)
    # ... your analysis
```

## Tips

### Quick Edit-Test Cycle

Keep testing the same dataset while editing your script:

```bash
# Windows PowerShell
while ($true) {
    cls
    python test_my_script.py my_script.py real_data.txt
    Write-Host "Press Ctrl+C to stop, or Enter to test again..."
    Read-Host
}
```

```bash
# Linux/Mac
while true; do
    clear
    python test_my_script.py my_script.py real_data.txt
    echo "Press Ctrl+C to stop, or Enter to test again..."
    read
done
```

### Test with Multiple Datasets

```bash
# Download several datasets from SmartLab
# Then test against all of them
python test_my_script.py my_script.py dataset1.txt
python test_my_script.py my_script.py dataset2.txt
python test_my_script.py my_script.py dataset3.txt
```

### Create Edge Case Test Data

Test with challenging scenarios:

**Empty data:**
```json
[]
```

**Single point:**
```json
[{"timestamp": "2025-11-13T10:00:00Z", "value": 25.5}]
```

**Large values:**
```json
[
  {"timestamp": "2025-11-13T10:00:00Z", "value": 1000000},
  {"timestamp": "2025-11-13T11:00:00Z", "value": 999999}
]
```

**Sparse data (missing timestamps):**
```csv
timestamp,value
2025-11-13T10:00:00Z,25
2025-11-13T15:00:00Z,30
```

## Troubleshooting

### "Failed to parse file as JSON or CSV"

**Problem:** The file format isn't recognized.

**Solution:**
- For CSV files, make sure they have a header row with column names
- For JSON files, validate with: `python -m json.tool your_file.json`
- Check the file actually contains data (not empty or corrupted)

### "No valid data points found in CSV"

**Problem:** CSV columns aren't recognized.

**Solution:**
The script looks for these column names (case-insensitive):
- Timestamp: `timestamp`, `time`, `date`, `datetime`
- Value: `value`, `val`, `measurement`
- Unit: `unit`, `units`

Make sure your CSV has at least `timestamp` and `value` columns.

### Script works with synthetic data but fails with real data

**Problem:** Real data has edge cases your script doesn't handle.

**Common issues:**
- Missing or null values
- Unexpected timestamp formats
- Values outside expected range
- Non-numeric values

**Solution:** Add error handling:
```python
try:
    values = [float(dp['value']) for dp in data_points]
except (ValueError, KeyError) as e:
    print(f"Error processing data point: {e}", file=sys.stderr)
    raise ValueError(f"Invalid data point format: {e}")
```

### Downloaded file from SmartLab won't parse

**Problem:** The device sent data in a custom format.

**Solution:**
- Check what the file looks like (open in text editor)
- If it's CSV-like but with different columns, you may need to:
  1. Manually convert it to the expected format, or
  2. Create a custom parser for your device's format

## Example Files

The project includes example data files:
- `example_data_simple.json` - Simple JSON array format
- `example_data_complete.json` - Complete format with parameters
- `example_data_csv.txt` - CSV format (like SmartLab downloads)

Test with these:
```bash
python test_my_script.py my_script.py example_data_csv.txt
```

## See Also

- `QUICK_START_ANALYSIS_SCRIPTS.md` - Quick reference for script development
- `ANALYSIS_SCRIPT_DEVELOPMENT_GUIDE.md` - Complete development guide
