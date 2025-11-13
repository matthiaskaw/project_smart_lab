#!/usr/bin/env python3
"""
Custom Analysis Script Template
================================
Use this template to create your own analysis scripts for SmartLab.

Replace this description with information about what your script does.

Author: Your Name
Version: 1.0.0
"""

import json
import sys
import matplotlib
matplotlib.use('Agg')  # Required for non-interactive plotting
import matplotlib.pyplot as plt
from pathlib import Path
from datetime import datetime


def analyze_data(data_points, parameters):
    """
    Main analysis logic - customize this function for your analysis.

    Args:
        data_points: List of dictionaries with keys:
            - timestamp: ISO 8601 formatted string
            - value: numeric value
            - unit: (optional) measurement unit
        parameters: Dictionary of custom parameters passed from the UI

    Returns:
        Dictionary containing your analysis results
    """
    # Extract values and timestamps
    values = [float(dp['value']) for dp in data_points]
    timestamps = [datetime.fromisoformat(dp['timestamp'].replace('Z', '+00:00'))
                  for dp in data_points]

    # Example: Calculate basic statistics
    count = len(values)
    mean = sum(values) / count if count > 0 else 0
    min_val = min(values) if values else 0
    max_val = max(values) if values else 0

    # TODO: Add your custom analysis logic here
    # Examples:
    # - Calculate custom statistics
    # - Perform curve fitting
    # - Detect anomalies
    # - Analyze trends
    # - Compare against thresholds

    results = {
        'count': count,
        'mean': mean,
        'min': min_val,
        'max': max_val,
        'range': max_val - min_val,
        # Add your custom results here
    }

    return results, values, timestamps


def create_visualization(values, timestamps, dataset_name, output_path, analysis_results):
    """
    Create visualization - customize this function for your plots.

    Args:
        values: List of numeric values
        timestamps: List of datetime objects
        dataset_name: Name of the dataset
        output_path: Path object where to save the image
        analysis_results: Results from analyze_data function
    """
    # Example: Create a simple plot
    fig, ax = plt.subplots(figsize=(12, 6))

    # TODO: Customize your visualization here
    # Examples:
    # - Multiple subplots
    # - Different plot types (scatter, bar, histogram, etc.)
    # - Annotations and markers
    # - Custom styling and colors

    ax.plot(timestamps, values, marker='o', linestyle='-', linewidth=2, label='Data')
    ax.axhline(analysis_results['mean'], color='r', linestyle='--',
               label=f"Mean: {analysis_results['mean']:.2f}")

    ax.set_title(f'{dataset_name} - Custom Analysis', fontsize=14, fontweight='bold')
    ax.set_xlabel('Time', fontsize=12)
    ax.set_ylabel('Value', fontsize=12)
    ax.legend()
    ax.grid(True, alpha=0.3)
    plt.xticks(rotation=45)

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    plt.close()


def main():
    """
    Main entry point - handles input/output.
    You typically don't need to modify this function.
    """
    try:
        # Read input from stdin (provided by SmartLab)
        input_data = json.load(sys.stdin)

        # Get output directory from command line argument
        output_dir = sys.argv[1] if len(sys.argv) > 1 else '.'

        # Extract input data
        dataset_id = input_data['datasetId']
        dataset_name = input_data['datasetName']
        data_points = input_data['dataPoints']
        parameters = input_data.get('parameters', {})

        # Validate input
        if not data_points:
            raise ValueError("No data points provided")

        if len(data_points) < 2:
            raise ValueError("At least 2 data points required for analysis")

        # Perform analysis
        analysis_results, values, timestamps = analyze_data(data_points, parameters)

        # Generate visualization
        output_filename = f"custom_analysis_{dataset_id[:8]}.png"
        output_path = Path(output_dir) / output_filename
        create_visualization(values, timestamps, dataset_name, output_path, analysis_results)

        # Prepare success result
        # REQUIRED FIELDS: status, imagePath
        # OPTIONAL FIELDS: statistics, metadata, customData
        result = {
            "status": "success",
            "imagePath": output_filename,
            "statistics": {
                # Round all numeric values for clean output
                key: round(value, 4) if isinstance(value, float) else value
                for key, value in analysis_results.items()
            },
            "metadata": {
                "scriptVersion": "1.0.0",
                "scriptName": "Custom Analysis Template",
                "dataPointsAnalyzed": len(data_points)
            }
        }

        # Output result as JSON to stdout
        print(json.dumps(result))

    except Exception as e:
        # Error handling - return error information
        error_result = {
            "status": "error",
            "errorMessage": str(e),
            "errorType": type(e).__name__
        }
        print(json.dumps(error_result))
        sys.exit(1)


if __name__ == '__main__':
    main()
