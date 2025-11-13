# Analysis Script Development Guide

This guide will help you create and test your own analysis scripts for SmartLab.

## Quick Start

### 1. Test the Template Script

First, verify that the test infrastructure works:

```bash
python test_my_script.py analysis-scripts/_test-scripts/my_custom_analysis_template.py
```

This will:
- Generate synthetic test data
- Run the template script
- Display the results
- Create output images in the `test_output/` directory

### 2. Create Your Own Script

Copy the template and customize it:

```bash
cp analysis-scripts/_test-scripts/my_custom_analysis_template.py analysis-scripts/my_script.py
```

Then edit `analysis-scripts/my_script.py` to implement your analysis logic.

### 3. Test Your Script

```bash
python test_my_script.py analysis-scripts/my_script.py
```

## Script Structure

### Required Input Format

Your script receives JSON data via stdin:

```json
{
  "datasetId": "uuid-string",
  "datasetName": "Human-readable name",
  "dataPoints": [
    {
      "timestamp": "2025-11-13T10:00:00Z",
      "value": 25.5,
      "unit": "°C"
    }
  ],
  "parameters": {
    "customParam1": "value1"
  }
}
```

### Required Output Format

Your script must output JSON to stdout:

**Success:**
```json
{
  "status": "success",
  "imagePath": "output_filename.png",
  "statistics": {
    "mean": 25.5,
    "min": 20.0,
    "max": 30.0
  },
  "metadata": {
    "scriptVersion": "1.0.0",
    "scriptName": "My Analysis"
  }
}
```

**Error:**
```json
{
  "status": "error",
  "errorMessage": "Description of what went wrong",
  "errorType": "ValueError"
}
```

## Development Workflow

1. **Edit your script** - Modify the `analyze_data()` and `create_visualization()` functions
2. **Test locally** - Run `python test_my_script.py your_script.py`
3. **Check output** - View generated images in `test_output/` directory
4. **Iterate** - Refine your analysis and visualization
5. **Deploy** - Upload through SmartLab's web interface

## Common Customizations

### Custom Statistics

Modify the `analyze_data()` function:

```python
def analyze_data(data_points, parameters):
    values = [float(dp['value']) for dp in data_points]

    # Your custom analysis
    threshold = parameters.get('threshold', 30.0)
    above_threshold = sum(1 for v in values if v > threshold)

    return {
        'mean': sum(values) / len(values),
        'above_threshold': above_threshold,
        'threshold_percentage': (above_threshold / len(values)) * 100
    }
```

### Advanced Visualizations

```python
def create_visualization(values, timestamps, dataset_name, output_path, analysis_results):
    fig, (ax1, ax2) = plt.subplots(2, 1, figsize=(12, 10))

    # Time series plot
    ax1.plot(timestamps, values)
    ax1.set_title('Time Series')

    # Histogram
    ax2.hist(values, bins=20)
    ax2.set_title('Distribution')

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    plt.close()
```

### Use Custom Parameters

Parameters can be defined in the script metadata and passed from the UI:

```python
def analyze_data(data_points, parameters):
    # Access custom parameters
    window_size = parameters.get('window_size', 5)
    threshold = parameters.get('threshold', 25.0)

    # Use them in your analysis
    # ...
```

## Testing with Custom Data

Edit `test_my_script.py` to customize test data:

```python
def generate_test_data(num_points=20, base_value=25.0, variation=5.0):
    # Modify to generate data matching your use case
    # Examples:
    # - Sine wave pattern
    # - Step function
    # - Random walk
    # - Real data from a file
```

Or create a custom test data file:

```python
# custom_test_data.json
{
  "datasetId": "custom-test-123",
  "datasetName": "My Custom Test Data",
  "dataPoints": [
    {"timestamp": "2025-11-13T10:00:00Z", "value": 23.5},
    {"timestamp": "2025-11-13T11:00:00Z", "value": 24.1}
  ],
  "parameters": {
    "threshold": 25.0
  }
}
```

Then load it in your test:

```python
with open('custom_test_data.json') as f:
    test_data = json.load(f)
    success, result, error = run_script_test(script_path, test_data)
```

## Required Dependencies

Your script can use these pre-installed libraries:
- `matplotlib` - Plotting and visualization
- `numpy` - Numerical computations (if available)
- `scipy` - Scientific computing (if available)
- Standard library modules

Always use `matplotlib.use('Agg')` for non-interactive plotting.

## Script Validation

Before your script runs in SmartLab, it's validated for:
- **Syntax errors** - Must be valid Python
- **Forbidden imports** - No `os`, `subprocess`, `socket`, etc.
- **Security checks** - No dangerous operations

Test validation manually:

```bash
python analysis-scripts/_validators/validate_python.py your_script.py
```

## Debugging Tips

1. **Print to stderr** for debugging (won't interfere with JSON output):
   ```python
   import sys
   print(f"Debug: values = {values}", file=sys.stderr)
   ```

2. **Check the test output directory** for generated images

3. **Run with verbose error handling**:
   ```python
   try:
       # your code
   except Exception as e:
       import traceback
       print(json.dumps({
           "status": "error",
           "errorMessage": str(e),
           "errorType": type(e).__name__,
           "traceback": traceback.format_exc()
       }))
   ```

4. **Use the existing test harness** in the SmartLab codebase:
   ```bash
   dotnet run --project . --no-build -- TestAnalysisServices
   ```

## Examples

Check these built-in scripts for inspiration:
- `analysis-scripts/built-in/python/statistical_summary.py` - Comprehensive statistics
- `analysis-scripts/built-in/python/basic_line_plot.py` - Simple time series plot

## Uploading to SmartLab

Once your script is ready:
1. Navigate to the Analysis section in SmartLab
2. Click "Manage Scripts"
3. Upload your `.py` file
4. Fill in metadata (name, description, tags)
5. The script will be validated automatically
6. If validation passes, it's ready to use!

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Script times out | Optimize your analysis or request timeout increase |
| Image not generated | Check file path is relative, check permissions |
| Invalid JSON output | Ensure nothing else prints to stdout |
| Import errors | Check if library is available in SmartLab environment |
| Validation fails | Review forbidden operations and fix security issues |

## Advanced: Testing Integration

To test your script with real SmartLab data:

1. Export a dataset from SmartLab (future feature)
2. Or manually create JSON matching your measurement data
3. Use the test runner with real data:

```bash
python test_my_script.py my_script.py < real_data.json
```

## Support

For issues or questions:
- Check `TEST_RESULTS.md` for validation behavior
- Review `ANALYSIS_FEATURE_ELABORATION.md` for architecture details
- Examine existing scripts in `analysis-scripts/built-in/`
