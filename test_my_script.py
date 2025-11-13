#!/usr/bin/env python3
"""
Standalone Test Runner for SmartLab Analysis Scripts
=====================================================

This script allows you to test your custom analysis scripts locally
without running the full SmartLab application.

Usage:
    python test_my_script.py <path_to_your_script.py>

Example:
    python test_my_script.py analysis-scripts/_test-scripts/my_custom_analysis_template.py
"""

import json
import sys
import subprocess
from datetime import datetime, timedelta
from pathlib import Path
import random


def generate_test_data(num_points=20, base_value=25.0, variation=5.0):
    """
    Generate synthetic test data for analysis.

    Args:
        num_points: Number of data points to generate
        base_value: Base value for measurements
        variation: Range of random variation

    Returns:
        Dictionary in SmartLab input format
    """
    data_points = []
    start_time = datetime.now() - timedelta(hours=num_points)

    for i in range(num_points):
        timestamp = start_time + timedelta(hours=i)
        # Generate value with some random variation and a slight trend
        value = base_value + random.uniform(-variation, variation) + (i * 0.1)

        data_points.append({
            "timestamp": timestamp.isoformat(),
            "value": value,
            "unit": "°C"
        })

    return {
        "datasetId": "test-dataset-12345678",
        "datasetName": "Test Temperature Measurements",
        "dataPoints": data_points,
        "parameters": {
            # Add custom parameters here if your script needs them
            # "threshold": 30.0,
            # "window_size": 5
        }
    }


def run_script_test(script_path, test_data=None, output_dir=None):
    """
    Run an analysis script with test data.

    Args:
        script_path: Path to the Python script to test
        test_data: Optional custom test data (will generate if not provided)
        output_dir: Optional output directory (will use temp if not provided)

    Returns:
        Tuple of (success, result_dict, error_message)
    """
    script_path = Path(script_path)

    if not script_path.exists():
        return False, None, f"Script not found: {script_path}"

    # Generate test data if not provided
    if test_data is None:
        test_data = generate_test_data()

    # Create output directory
    if output_dir is None:
        output_dir = Path("test_output")
    else:
        output_dir = Path(output_dir)

    output_dir.mkdir(exist_ok=True)

    # Convert test data to JSON
    input_json = json.dumps(test_data, indent=2)

    print("=" * 80)
    print("TESTING ANALYSIS SCRIPT")
    print("=" * 80)
    print(f"Script:      {script_path}")
    print(f"Output Dir:  {output_dir}")
    print(f"Data Points: {len(test_data['dataPoints'])}")
    print(f"Dataset:     {test_data['datasetName']}")
    print()

    # Run the script
    try:
        # Determine Python command
        python_cmd = "python3" if sys.platform != "win32" else "python"

        process = subprocess.Popen(
            [python_cmd, str(script_path), str(output_dir)],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True
        )

        stdout, stderr = process.communicate(input=input_json, timeout=30)

        print("-" * 80)
        print("EXECUTION RESULTS")
        print("-" * 80)
        print(f"Exit Code: {process.returncode}")
        print()

        if stderr:
            print("STDERR Output:")
            print(stderr)
            print()

        if stdout:
            print("STDOUT Output:")
            print(stdout)
            print()

            # Try to parse the output as JSON
            try:
                result = json.loads(stdout)
                print("-" * 80)
                print("PARSED RESULT")
                print("-" * 80)
                print(json.dumps(result, indent=2))
                print()

                # Check if the script succeeded
                if result.get("status") == "success":
                    print("[OK] Script executed successfully!")

                    # Check for generated image
                    if "imagePath" in result:
                        image_path = output_dir / result["imagePath"]
                        if image_path.exists():
                            print(f"[OK] Generated image: {image_path}")
                            print(f"  File size: {image_path.stat().st_size} bytes")
                        else:
                            print(f"[ERROR] Image file not found: {image_path}")

                    # Display statistics
                    if "statistics" in result:
                        print("\nStatistics:")
                        for key, value in result["statistics"].items():
                            print(f"  {key}: {value}")

                    # Display metadata
                    if "metadata" in result:
                        print("\nMetadata:")
                        for key, value in result["metadata"].items():
                            print(f"  {key}: {value}")

                    return True, result, None

                elif result.get("status") == "error":
                    print(f"[ERROR] Script returned error: {result.get('errorMessage')}")
                    return False, result, result.get("errorMessage")

            except json.JSONDecodeError as e:
                print(f"[ERROR] Failed to parse output as JSON: {e}")
                return False, None, f"Invalid JSON output: {e}"

        return process.returncode == 0, None, stderr if stderr else None

    except subprocess.TimeoutExpired:
        print("[ERROR] Script execution timed out (30 seconds)")
        return False, None, "Execution timeout"

    except Exception as e:
        print(f"[ERROR] Error running script: {e}")
        return False, None, str(e)


def parse_csv_to_datapoints(csv_content, dataset_name):
    """
    Parse CSV data downloaded from SmartLab into dataPoints format.

    Expected CSV format (with header):
    timestamp,value
    2025-11-13T10:00:00Z,25.5
    2025-11-13T11:00:00Z,26.1

    Or with unit:
    timestamp,value,unit
    2025-11-13T10:00:00Z,25.5,°C

    Args:
        csv_content: String content of CSV file
        dataset_name: Name for the dataset

    Returns:
        Dictionary in SmartLab input format
    """
    import csv
    import io

    lines = csv_content.strip().split('\n')
    if len(lines) < 2:
        raise ValueError("CSV file must have at least a header and one data row")

    reader = csv.DictReader(io.StringIO(csv_content))
    data_points = []

    for row in reader:
        # Try to find timestamp and value columns (case-insensitive)
        timestamp = None
        value = None
        unit = None

        for key in row.keys():
            key_lower = key.lower().strip()
            if key_lower in ['timestamp', 'time', 'date', 'datetime']:
                timestamp = row[key].strip()
            elif key_lower in ['value', 'val', 'measurement']:
                value = row[key].strip()
            elif key_lower in ['unit', 'units']:
                unit = row[key].strip()

        if timestamp and value:
            data_point = {
                "timestamp": timestamp,
                "value": float(value)
            }
            if unit:
                data_point["unit"] = unit
            data_points.append(data_point)

    if not data_points:
        raise ValueError("No valid data points found in CSV. Expected columns: timestamp, value")

    return {
        'datasetId': 'custom-dataset-12345678',
        'datasetName': dataset_name,
        'dataPoints': data_points,
        'parameters': {}
    }


def load_data_from_file(data_file_path):
    """
    Load test data from a file.

    Supports:
    1. JSON - Complete SmartLab format: {"datasetId": ..., "dataPoints": [...], ...}
    2. JSON - Simple array: [{"timestamp": ..., "value": ...}, ...]
    3. CSV/TXT - Downloaded from SmartLab: timestamp,value format

    Args:
        data_file_path: Path to data file (JSON, CSV, or TXT)

    Returns:
        Dictionary in SmartLab input format
    """
    file_path = Path(data_file_path)
    dataset_name = file_path.stem

    with open(data_file_path, 'r') as f:
        content = f.read()

    # Try to parse as JSON first
    try:
        data = json.loads(content)

        # Check if it's already in SmartLab format
        if isinstance(data, dict) and 'dataPoints' in data:
            # Ensure required fields exist
            if 'datasetId' not in data:
                data['datasetId'] = 'custom-dataset-12345678'
            if 'datasetName' not in data:
                data['datasetName'] = dataset_name
            if 'parameters' not in data:
                data['parameters'] = {}
            return data

        # If it's just an array, assume it's dataPoints
        if isinstance(data, list):
            return {
                'datasetId': 'custom-dataset-12345678',
                'datasetName': dataset_name,
                'dataPoints': data,
                'parameters': {}
            }

        raise ValueError("Invalid JSON format")

    except json.JSONDecodeError:
        # Not JSON, try parsing as CSV
        try:
            return parse_csv_to_datapoints(content, dataset_name)
        except Exception as csv_error:
            raise ValueError(
                f"Failed to parse file as JSON or CSV.\n"
                f"JSON error: Not valid JSON\n"
                f"CSV error: {csv_error}\n\n"
                f"Expected formats:\n"
                f"1. JSON: {{'dataPoints': [...], 'datasetName': '...', ...}}\n"
                f"2. JSON: [{{'timestamp': '...', 'value': ...}}, ...]\n"
                f"3. CSV: timestamp,value format with header"
            )


def main():
    """Main entry point for the test runner."""
    if len(sys.argv) < 2:
        print("Usage: python test_my_script.py <path_to_script.py> [data_file.json]")
        print("\nExamples:")
        print("  # Test with synthetic data:")
        print("  python test_my_script.py analysis-scripts/_test-scripts/my_custom_analysis_template.py")
        print("\n  # Test with real data from JSON file:")
        print("  python test_my_script.py my_analysis.py real_data.json")
        print("\nData file format:")
        print("  Option 1 - Complete format:")
        print('    {"datasetId": "...", "datasetName": "...", "dataPoints": [...], "parameters": {...}}')
        print("\n  Option 2 - Just data points:")
        print('    [{"timestamp": "2025-11-13T10:00:00Z", "value": 25.5}, ...]')
        sys.exit(1)

    script_path = sys.argv[1]
    test_data = None

    # Check if a data file was provided
    if len(sys.argv) >= 3:
        data_file = sys.argv[2]
        try:
            print(f"Loading data from: {data_file}")
            test_data = load_data_from_file(data_file)
            print(f"Loaded {len(test_data['dataPoints'])} data points")
            print()
        except FileNotFoundError:
            print(f"ERROR: Data file not found: {data_file}")
            sys.exit(1)
        except json.JSONDecodeError as e:
            print(f"ERROR: Invalid JSON in data file: {e}")
            sys.exit(1)
        except Exception as e:
            print(f"ERROR: Failed to load data file: {e}")
            sys.exit(1)
    else:
        print("No data file provided, using synthetic test data")
        print()

    success, result, error = run_script_test(script_path, test_data)

    print("=" * 80)
    if success:
        print("TEST PASSED [OK]")
    else:
        print("TEST FAILED [ERROR]")
        if error:
            print(f"Error: {error}")
    print("=" * 80)

    sys.exit(0 if success else 1)


if __name__ == '__main__':
    main()
