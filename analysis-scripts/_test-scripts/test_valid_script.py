#!/usr/bin/env python3
"""
Valid test script that follows all SmartLab conventions.
This should pass all validation checks.
"""

import json
import sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from pathlib import Path

def main():
    # Read input from stdin
    input_data = json.load(sys.stdin)

    dataset_id = input_data['datasetId']
    dataset_name = input_data['datasetName']
    data_points = input_data['dataPoints']
    params = input_data.get('parameters', {})

    # Get output directory from command line
    output_dir = sys.argv[1] if len(sys.argv) > 1 else '.'

    # Process data
    timestamps = [dp['timestamp'] for dp in data_points]
    values = [float(dp['value']) for dp in data_points]

    # Calculate statistics
    mean_value = sum(values) / len(values) if values else 0
    min_value = min(values) if values else 0
    max_value = max(values) if values else 0

    # Create visualization
    plt.figure(figsize=(10, 6))
    plt.plot(range(len(values)), values, marker='o', linestyle='-', color='blue')
    plt.title(f'Test Analysis: {dataset_name}')
    plt.xlabel('Data Point Index')
    plt.ylabel('Value')
    plt.grid(True, alpha=0.3)
    plt.tight_layout()

    # Save image
    output_filename = f"test_result_{dataset_id[:8]}.png"
    output_path = Path(output_dir) / output_filename
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    plt.close()

    # Return result as JSON
    result = {
        "status": "success",
        "imagePath": output_filename,
        "statistics": {
            "mean": mean_value,
            "min": min_value,
            "max": max_value,
            "count": len(values)
        },
        "metadata": {
            "scriptVersion": "1.0.0",
            "testScript": True
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
