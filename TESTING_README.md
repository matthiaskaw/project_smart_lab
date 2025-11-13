# SmartLab Testing Tools

This directory contains standalone test scripts for developing SmartLab components without running the full application.

## Available Test Tools

### 1. Analysis Script Testing (`test_my_script.py`)

Test custom analysis scripts with synthetic or real data.

**Purpose**: Develop and debug Python analysis scripts that process measurement data.

**Quick Start**:
```bash
# Test with synthetic data
python test_my_script.py analysis-scripts/_test-scripts/my_custom_analysis_template.py

# Test with downloaded data
python test_my_script.py my_analysis.py downloaded_data.json
```

**Documentation**: See `QUICK_START_ANALYSIS_SCRIPTS.md` and `ANALYSIS_SCRIPT_DEVELOPMENT_GUIDE.md`

---

### 2. Device Testing (`test_device.py`)

Test device implementations that communicate with SmartLab via IPC.

**Purpose**: Develop and debug device scripts that acquire measurement data from hardware or simulations.

**Quick Start**:
```bash
# Test with default parameters
python test_device.py ../smartlab_devices/dummy_finite_measurement.py

# Test with custom parameters
python test_device.py ../smartlab_devices/dummy_finite_measurement.py --parameters '{"dataPoints": 20}'
```

**Documentation**: See `DEVICE_TESTING_GUIDE.md`

---

## Workflow Comparison

| Aspect | Analysis Script Testing | Device Testing |
|--------|------------------------|----------------|
| **Tests** | Data analysis and visualization | Data acquisition and device communication |
| **Input** | JSON data (synthetic or downloaded) | Device parameters |
| **Output** | Statistics, plots, images | Raw measurement data |
| **Communication** | stdin/stdout (simple) | Named pipes/sockets (IPC) |
| **Typical Use** | Developing data processing logic | Developing hardware interfaces |

---

## Example Workflows

### Developing an Analysis Script

1. Create your analysis script using the template:
   ```bash
   cp analysis-scripts/_test-scripts/my_custom_analysis_template.py my_analysis.py
   ```

2. Test with synthetic data:
   ```bash
   python test_my_script.py my_analysis.py
   ```

3. Download real data from SmartLab's UI (Data > View Dataset > Download)

4. Test with real data:
   ```bash
   python test_my_script.py my_analysis.py real_dataset.json
   ```

5. Iterate until satisfied, then deploy to `analysis-scripts/` directory

---

### Developing a Device Script

1. Create your device script inheriting from `BaseFiniteDevice`:
   ```python
   from base_finite_device import BaseFiniteDevice

   class MyDevice(BaseFiniteDevice):
       def get_device_name(self) -> str:
           return "My Custom Device"
       # ... implement abstract methods
   ```

2. Test locally:
   ```bash
   python test_device.py my_device.py
   ```

3. Test with specific parameters:
   ```bash
   python test_device.py my_device.py --parameters '{"sampleRate": 100}'
   ```

4. Verify data format and parameter validation

5. Deploy to devices directory and register in SmartLab

---

## When to Use Which Tool

### Use `test_my_script.py` when:
- Developing data analysis algorithms
- Testing statistical calculations
- Debugging visualization code
- Processing downloaded datasets
- Creating custom plots or reports

### Use `test_device.py` when:
- Developing new device drivers
- Testing device communication protocols
- Debugging IPC pipe issues
- Verifying parameter validation
- Testing data acquisition logic

---

## Directory Structure

```
project_smart_lab/
├── test_my_script.py              # Analysis script test runner
├── test_device.py                 # Device test runner
├── TESTING_README.md              # This file
├── DEVICE_TESTING_GUIDE.md        # Device testing documentation
├── QUICK_START_ANALYSIS_SCRIPTS.md   # Analysis quick start
├── ANALYSIS_SCRIPT_DEVELOPMENT_GUIDE.md  # Analysis detailed guide
│
├── analysis-scripts/              # Deployed analysis scripts
│   ├── _test-scripts/            # Templates and examples
│   │   ├── my_custom_analysis_template.py
│   │   └── example_advanced_analysis.py
│   └── built-in/                 # Built-in analysis scripts
│
└── test_output/                   # Test output files
    ├── *.png                      # Generated plots
    └── *.json                     # Analysis results
```

---

## Tips for Effective Testing

### General
- Always test locally before deploying to SmartLab
- Use version control (git) to track changes
- Keep test data files in a separate `test-data/` directory
- Document expected input/output formats

### Analysis Scripts
- Start with the provided templates
- Use synthetic data first to verify basic logic
- Test with edge cases (empty data, single point, etc.)
- Verify JSON output format matches expected schema

### Device Scripts
- Test parameter validation thoroughly
- Verify data format is what you expect
- Test both success and error paths
- Check cleanup and shutdown behavior
- Test on target platform (Windows/Linux)

---

## Troubleshooting

### Analysis Script Issues
- **Script doesn't run**: Check Python path and dependencies
- **Invalid JSON output**: Ensure script prints only JSON to stdout
- **Image not generated**: Verify output directory exists and is writable
- **Data format errors**: Check that input matches expected format

### Device Issues
- **Pipe connection fails**: Ensure base_finite_device.py is accessible
- **Device crashes**: Check device output for exceptions
- **Timeout**: Increase timeout for slow measurements
- **Platform issues**: Test on both Windows and Linux if cross-platform

---

## Next Steps

After successful local testing:

1. **Analysis Scripts**: Copy to `analysis-scripts/` directory
2. **Device Scripts**: Register in SmartLab's device configuration
3. **Run in SmartLab**: Test the full integration
4. **Monitor logs**: Check for any issues in production use

---

## Related Documentation

- SmartLab User Guide (in-app)
- Device Development Guide: `DEVICE_TESTING_GUIDE.md`
- Analysis Script Guide: `ANALYSIS_SCRIPT_DEVELOPMENT_GUIDE.md`
- API Reference: Check source code for detailed interfaces
