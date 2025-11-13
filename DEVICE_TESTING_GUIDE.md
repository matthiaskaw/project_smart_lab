# Device Testing Guide

This guide explains how to test SmartLab device implementations locally without running the full SmartLab application.

## Overview

The `test_device.py` script simulates the SmartLab app's IPC communication protocol, allowing you to:
- Test device scripts in isolation
- Debug device implementations quickly
- Verify device behavior without database/UI complexity
- Test cross-platform compatibility (Windows/Linux)

## Quick Start

### Basic Usage

Test a device with default parameters:

```bash
python test_device.py ../smartlab_devices/dummy_finite_measurement.py
```

### Custom Parameters

Test a device with custom parameters:

```bash
python test_device.py ../smartlab_devices/dummy_finite_measurement.py --parameters '{"dataPoints": 50}'
```

## What the Test Does

The test script executes the complete device communication workflow:

1. **Creates IPC Pipes** - Sets up named pipes (Windows) or UNIX sockets (Linux)
2. **Launches Device Process** - Starts your device script as a subprocess
3. **Connects to Pipes** - Establishes bidirectional communication
4. **Initializes Device** - Sends `INITIALIZE` command, receives device name
5. **Gets Parameters** - Sends `GETPARAMETERS` command, receives parameter definitions
6. **Sets Parameters** - Sends `SETPARAMETERS:{"key":"value"}` with merged defaults + custom parameters
7. **Gets Measurement Data** - Sends `GETDATA_STRUCTURED` command, receives measurement data
8. **Finishes** - Sends `FINISH` command for clean shutdown

## Example Output

```
================================================================================
DEVICE TEST WORKFLOW
================================================================================
Device Script: ..\smartlab_devices\dummy_finite_measurement.py

[1/7] Creating IPC pipes...
  [OK] Pipes created

[2/7] Launching device process...
  Device process PID: 12345
  [OK] Device process started

[3/7] Connecting to pipes...
  Waiting for device to connect to write pipe...
  Device connected to write pipe
  Waiting for device to connect to read pipe...
  Device connected to read pipe
  [OK] Connected to pipes

[4/7] Initializing device (INITIALIZE command)...
  [OK] Device initialized: Dummy Finite Device

[5/7] Getting parameter definitions (GETPARAMETERS command)...
  [OK] Received 1 parameter definitions:
      - Number of Data Points: Integer (default: 10)

[6/7] Setting parameters (SETPARAMETERS command)...
  [OK] Parameters set:
      dataPoints: 10

[7/7] Getting measurement data (GETDATA_STRUCTURED command)...
  [OK] Received measurement data
      Raw data entries: 10
      Timestamp: 2025-11-13T15:30:45.123456

--------------------------------------------------------------------------------
MEASUREMENT DATA SAMPLE
--------------------------------------------------------------------------------
Total data points: 10

First 5 data points:
  1. {'value': 26.73}
  2. {'value': 27.23}
  3. {'value': 27.73}
  4. {'value': 28.23}
  5. {'value': 28.73}

... (5 more data points)

Parameters used in measurement:
  dataPoints: 10

Sending FINISH command...
[OK] Device finished

================================================================================
TEST COMPLETED SUCCESSFULLY
================================================================================
```

## Creating a Test Device

Here's a minimal device implementation template:

```python
from base_finite_device import BaseFiniteDevice
from typing import List, Dict, Tuple
import asyncio

class MyTestDevice(BaseFiniteDevice):
    """Your custom device implementation."""

    def get_device_name(self) -> str:
        """Return device display name."""
        return "My Test Device"

    async def get_parameter_definitions(self) -> List[Dict]:
        """Define parameters this device accepts."""
        return [
            {
                "name": "sampleRate",
                "displayName": "Sample Rate",
                "type": "Integer",
                "defaultValue": 100,
                "isRequired": True,
                "unit": "Hz",
                "description": "Sampling frequency"
            }
        ]

    async def generate_measurement_data(self) -> List[Dict]:
        """Generate or acquire measurement data."""
        sample_rate = self.measurement_parameters.get("sampleRate", 100)
        data_points = []

        for i in range(sample_rate):
            await asyncio.sleep(0.01)  # Simulate measurement
            data_points.append({
                "value": 20.0 + i * 0.1
            })

        return data_points

    def validate_parameters(self, parameters: Dict) -> Tuple[bool, str]:
        """Validate parameters before accepting."""
        if "sampleRate" not in parameters:
            return False, "sampleRate is required"

        rate = parameters["sampleRate"]
        if not isinstance(rate, int):
            return False, "sampleRate must be an integer"

        if rate < 1 or rate > 1000:
            return False, "sampleRate must be between 1 and 1000"

        return True, ""

# Standard device entry point
if __name__ == "__main__":
    import sys
    if len(sys.argv) < 2:
        print("Usage: python my_test_device.py <device_id>")
        sys.exit(1)

    device = MyTestDevice(sys.argv[1])
    import asyncio
    asyncio.run(device.run())
```

Test your device:

```bash
python test_device.py my_test_device.py --parameters '{"sampleRate": 200}'
```

## Command-Line Options

```
usage: test_device.py [-h] [--parameters PARAMETERS] device_script

positional arguments:
  device_script         Path to the device Python script

optional arguments:
  -h, --help            show this help message and exit
  --parameters PARAMETERS, -p PARAMETERS
                        Custom parameters as JSON string
```

## Tips for Device Development

### 1. Start Simple
Begin with a minimal device that returns basic data, then add complexity.

### 2. Use Print Statements
The test harness captures device stdout, so use `print(..., flush=True)` for debugging.

### 3. Test Parameters
Try both valid and invalid parameters to verify validation logic:

```bash
# Valid parameters
python test_device.py my_device.py --parameters '{"dataPoints": 10}'

# Invalid parameters (test error handling)
python test_device.py my_device.py --parameters '{"dataPoints": -1}'
python test_device.py my_device.py --parameters '{"invalid": "key"}'
```

### 4. Test Data Format
Remember: SmartLab stores raw data as-is. Your device controls the format:

```python
# Simple format (just values)
data_points.append({"value": 23.5})

# With timestamps
data_points.append({
    "timestamp": datetime.now().isoformat(),
    "value": 23.5
})

# With additional metadata
data_points.append({
    "value": 23.5,
    "unit": "°C",
    "sensor": "temp_01"
})
```

### 5. Cross-Platform Testing
If developing on Windows, test on Linux (WSL) before deploying:

```bash
# On Windows
python test_device.py my_device.py

# In WSL (Linux)
python3 test_device.py my_device.py
```

## Troubleshooting

### Pipe Connection Timeout
If the device fails to connect to pipes:
- Check the device script path is correct
- Verify `base_finite_device.py` is accessible
- Check for Python dependency issues (win32pipe on Windows, etc.)

### Device Process Crashes
Check device output in the test harness output for error messages.

### Invalid Response Format
Ensure your device:
- Sends responses with newline terminators
- Uses correct command response formats (e.g., "PARAMETERS {json}")
- Handles all required commands (INITIALIZE, GETPARAMETERS, etc.)

### Data Not Received
- Verify `generate_measurement_data()` returns a list of dicts
- Check that data is JSON-serializable
- Ensure the function completes without exceptions

## Related Files

- `test_my_script.py` - Test runner for analysis scripts
- `base_finite_device.py` - Base class for device implementations
- `dummy_finite_measurement.py` - Example device implementation

## Architecture Notes

### Why This Approach?

SmartLab maintains a clean separation:
- **Device Layer** (your code): Defines data format, handles hardware, validates parameters
- **SmartLab App**: Stores raw data, provides UI, runs analysis

The test harness simulates the app's side of the IPC protocol, letting you verify device behavior without the full app stack.

### IPC Protocol Summary

```
SmartLab → Device        Device → SmartLab
================        ================
INITIALIZE              → <device_name>
GETPARAMETERS           → PARAMETERS <json_array>
SETPARAMETERS:<json>    → PARAMS_SET | PARAMS_ERROR
GETDATA_STRUCTURED      → DATA:<json_object>
FINISH                  → FINISHED
```

### Platform Differences

**Windows**: Uses native Win32 named pipes (`\\.\pipe\<name>`)
**Linux**: Uses UNIX domain sockets (`/tmp/CoreFxPipe_<name>`)

The test harness and base device class handle these differences automatically.
