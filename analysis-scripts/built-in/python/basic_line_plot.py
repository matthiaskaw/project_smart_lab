#!/usr/bin/env python3
"""
Basic Line Plot - Built-in Analysis Script
===========================================
Creates a simple time-series line plot of measurement data.

Author: SmartLab Team
Version: 1.0.0
License: MIT
"""

import json
import sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from pathlib import Path
from datetime import datetime

def main():
    # Read input from stdin
    input_data = json.load(sys.stdin)

    dataset_id = input_data['datasetId']
    dataset_name = input_data['datasetName']
    data_points = input_data['dataPoints']
    params = input_data.get('parameters', {})

    # Get output directory
    output_dir = sys.argv[1] if len(sys.argv) > 1 else '.'

    # Extract data
    timestamps = []
    values = []
    unit = None

    for dp in data_points:
        try:
            timestamps.append(datetime.fromisoformat(dp['timestamp'].replace('Z', '+00:00')))
            values.append(float(dp['value']))
            if unit is None and 'unit' in dp:
                unit = dp['unit']
        except (ValueError, KeyError) as e:
            continue

    if not values:
        raise ValueError("No valid data points found")

    # Create visualization
    plt.figure(figsize=(12, 6))
    plt.plot(timestamps, values, marker='o', linestyle='-', linewidth=2, markersize=6)
    plt.title(f'{dataset_name}', fontsize=14, fontweight='bold')
    plt.xlabel('Time', fontsize=12)
    plt.ylabel(f'Value{f" ({unit})" if unit else ""}', fontsize=12)
    plt.grid(True, alpha=0.3, linestyle='--')
    plt.xticks(rotation=45)
    plt.tight_layout()

    # Save image
    output_filename = f"line_plot_{dataset_id[:8]}.png"
    output_path = Path(output_dir) / output_filename
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    plt.close()

    # Calculate statistics
    mean_val = sum(values) / len(values)
    min_val = min(values)
    max_val = max(values)

    # Return result
    result = {
        "status": "success",
        "imagePath": output_filename,
        "statistics": {
            "mean": round(mean_val, 4),
            "min": round(min_val, 4),
            "max": round(max_val, 4),
            "count": len(values),
            "range": round(max_val - min_val, 4)
        },
        "metadata": {
            "scriptVersion": "1.0.0",
            "scriptName": "Basic Line Plot"
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
