# Quick Start: Analysis Scripts

## Get Started in 3 Steps

### 1. Test the Template

```bash
python test_my_script.py analysis-scripts/_test-scripts/my_custom_analysis_template.py
```

Check `test_output/` for the generated image.

### 2. Copy and Edit

```bash
cp analysis-scripts/_test-scripts/my_custom_analysis_template.py my_script.py
```

Edit `my_script.py` - focus on these two functions:

**`analyze_data()`** - Your analysis logic
```python
def analyze_data(data_points, parameters):
    values = [float(dp['value']) for dp in data_points]

    # Your analysis here
    mean = sum(values) / len(values)

    return {'mean': mean}, values, timestamps
```

**`create_visualization()`** - Your plot
```python
def create_visualization(values, timestamps, dataset_name, output_path, results):
    plt.figure(figsize=(12, 6))
    plt.plot(timestamps, values)
    plt.savefig(output_path, dpi=150)
    plt.close()
```

### 3. Test Your Script

```bash
# Test with synthetic data
python test_my_script.py my_script.py

# OR test with real data from a JSON file
python test_my_script.py my_script.py my_data.json
```

See `TESTING_WITH_REAL_DATA.md` for how to export data from SmartLab.

## Input Format

Your script receives this via stdin:

```json
{
  "datasetId": "uuid",
  "datasetName": "Temperature",
  "dataPoints": [
    {"timestamp": "2025-11-13T10:00:00Z", "value": 25.5}
  ],
  "parameters": {}
}
```

Access in Python:
```python
input_data = json.load(sys.stdin)
dataset_name = input_data['datasetName']
values = [float(dp['value']) for dp in input_data['dataPoints']]
```

## Output Format

Your script must print JSON to stdout:

```python
result = {
    "status": "success",
    "imagePath": "output.png",
    "statistics": {
        "mean": 25.5
    }
}
print(json.dumps(result))
```

## Common Patterns

### Extract timestamps and values
```python
from datetime import datetime

timestamps = [datetime.fromisoformat(dp['timestamp'].replace('Z', '+00:00'))
              for dp in data_points]
values = [float(dp['value']) for dp in data_points]
```

### Basic statistics
```python
import math

mean = sum(values) / len(values)
variance = sum((x - mean) ** 2 for x in values) / len(values)
std = math.sqrt(variance)
```

### Simple plot
```python
import matplotlib.pyplot as plt

plt.figure(figsize=(12, 6))
plt.plot(timestamps, values, marker='o')
plt.xlabel('Time')
plt.ylabel('Value')
plt.grid(True)
plt.savefig(output_path, dpi=150, bbox_inches='tight')
plt.close()
```

### Multiple subplots
```python
fig, (ax1, ax2) = plt.subplots(2, 1, figsize=(12, 10))

ax1.plot(timestamps, values)
ax1.set_title('Time Series')

ax2.hist(values, bins=20)
ax2.set_title('Distribution')

plt.tight_layout()
plt.savefig(output_path)
plt.close()
```

## Examples to Study

1. **Basic template**: `analysis-scripts/_test-scripts/my_custom_analysis_template.py`
   - Simple structure
   - Easy to modify

2. **Advanced example**: `analysis-scripts/_test-scripts/example_advanced_analysis.py`
   - Moving averages
   - Anomaly detection
   - Multi-panel plots

3. **Built-in scripts**:
   - `analysis-scripts/built-in/python/statistical_summary.py`
   - `analysis-scripts/built-in/python/basic_line_plot.py`

## Files Created for You

- `test_my_script.py` - Test runner
- `analysis-scripts/_test-scripts/my_custom_analysis_template.py` - Template
- `analysis-scripts/_test-scripts/example_advanced_analysis.py` - Advanced example
- `ANALYSIS_SCRIPT_DEVELOPMENT_GUIDE.md` - Full documentation

## Testing Workflow

```bash
# 1. Edit your script
nano my_script.py

# 2. Test it
python test_my_script.py my_script.py

# 3. Check output
ls test_output/

# 4. Iterate until satisfied
# (repeat steps 1-3)
```

## Common Issues

| Problem | Solution |
|---------|----------|
| Script times out | Add timeout parameter to test runner |
| Image not created | Check `output_path` and directory exists |
| JSON parse error | Ensure nothing else prints to stdout |
| Import error | Check library is available (`import matplotlib`) |

## Next Steps

1. Read `ANALYSIS_SCRIPT_DEVELOPMENT_GUIDE.md` for details
2. Test your script locally until it works
3. Upload to SmartLab via web interface
4. Run on real data!

## Customizing Test Data

Edit `test_my_script.py` to change test data:

```python
# In generate_test_data() function
def generate_test_data(num_points=50, base_value=100, variation=20):
    # Modify these parameters
    # Or load from a JSON file
```

Or create `test_data.json` and load it:

```python
import json

with open('test_data.json') as f:
    test_data = json.load(f)

run_script_test('my_script.py', test_data)
```
