#!/usr/bin/env python3
"""
Statistical Summary - Built-in Analysis Script
==============================================
Generates comprehensive statistical analysis with visualizations.

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
import math

def calculate_statistics(values):
    """Calculate comprehensive statistics."""
    n = len(values)
    if n == 0:
        return None

    sorted_values = sorted(values)
    mean_val = sum(values) / n

    # Standard deviation
    variance = sum((x - mean_val) ** 2 for x in values) / n
    std_val = math.sqrt(variance)

    # Median and quartiles
    median_val = sorted_values[n // 2] if n % 2 == 1 else (sorted_values[n // 2 - 1] + sorted_values[n // 2]) / 2
    q1_val = sorted_values[n // 4]
    q3_val = sorted_values[3 * n // 4]

    return {
        'count': n,
        'mean': mean_val,
        'std': std_val,
        'min': sorted_values[0],
        'max': sorted_values[-1],
        'median': median_val,
        'q1': q1_val,
        'q3': q3_val,
        'iqr': q3_val - q1_val,
        'range': sorted_values[-1] - sorted_values[0]
    }

def create_visualization(data_points, stats, output_path, dataset_name):
    """Create multi-panel statistical visualization."""
    fig, axes = plt.subplots(2, 2, figsize=(14, 10))
    fig.suptitle(f'Statistical Analysis: {dataset_name}', fontsize=16, fontweight='bold')

    values = [float(dp['value']) for dp in data_points]
    timestamps = [datetime.fromisoformat(dp['timestamp'].replace('Z', '+00:00')) for dp in data_points]

    # Panel 1: Time series with mean line
    axes[0, 0].plot(timestamps, values, 'b-', alpha=0.7, linewidth=2, label='Data')
    axes[0, 0].axhline(stats['mean'], color='r', linestyle='--', linewidth=2, label=f'Mean: {stats["mean"]:.2f}')
    axes[0, 0].fill_between(
        range(len(values)),
        stats['mean'] - stats['std'],
        stats['mean'] + stats['std'],
        alpha=0.2, color='r', label='±1σ'
    )
    axes[0, 0].set_title('Time Series', fontsize=12, fontweight='bold')
    axes[0, 0].set_xlabel('Time')
    axes[0, 0].set_ylabel('Value')
    axes[0, 0].legend()
    axes[0, 0].grid(True, alpha=0.3)
    axes[0, 0].tick_params(axis='x', rotation=45)

    # Panel 2: Histogram
    axes[0, 1].hist(values, bins=20, density=False, alpha=0.7, color='skyblue', edgecolor='black')
    axes[0, 1].axvline(stats['mean'], color='r', linestyle='--', linewidth=2, label=f'Mean: {stats["mean"]:.2f}')
    axes[0, 1].axvline(stats['median'], color='g', linestyle='--', linewidth=2, label=f'Median: {stats["median"]:.2f}')
    axes[0, 1].set_title('Distribution', fontsize=12, fontweight='bold')
    axes[0, 1].set_xlabel('Value')
    axes[0, 1].set_ylabel('Frequency')
    axes[0, 1].legend()
    axes[0, 1].grid(True, alpha=0.3, axis='y')

    # Panel 3: Box plot
    bp = axes[1, 0].boxplot(values, vert=True, patch_artist=True, widths=0.5)
    bp['boxes'][0].set_facecolor('lightblue')
    bp['boxes'][0].set_edgecolor('black')
    bp['boxes'][0].set_linewidth(2)
    for element in ['whiskers', 'fliers', 'means', 'medians', 'caps']:
        plt.setp(bp[element], color='black', linewidth=2)
    axes[1, 0].set_title('Box Plot', fontsize=12, fontweight='bold')
    axes[1, 0].set_ylabel('Value')
    axes[1, 0].grid(True, alpha=0.3, axis='y')

    # Panel 4: Statistics table
    axes[1, 1].axis('off')
    stats_text = f"""
Statistical Summary
{'=' * 40}

Count:         {stats['count']}
Mean:          {stats['mean']:.4f}
Std Dev:       {stats['std']:.4f}
Min:           {stats['min']:.4f}
Q1 (25%):      {stats['q1']:.4f}
Median (50%):  {stats['median']:.4f}
Q3 (75%):      {stats['q3']:.4f}
Max:           {stats['max']:.4f}

Range:         {stats['range']:.4f}
IQR:           {stats['iqr']:.4f}
CV:            {(stats['std'] / stats['mean'] * 100):.2f}%
    """
    axes[1, 1].text(0.1, 0.5, stats_text, fontfamily='monospace', fontsize=11, va='center')

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    plt.close()

def main():
    # Read input
    input_data = json.load(sys.stdin)
    output_dir = sys.argv[1] if len(sys.argv) > 1 else '.'

    dataset_id = input_data['datasetId']
    dataset_name = input_data['datasetName']
    data_points = input_data['dataPoints']

    # Validate data
    if not data_points:
        raise ValueError("No data points provided")

    values = [float(dp['value']) for dp in data_points]
    stats = calculate_statistics(values)

    if stats is None:
        raise ValueError("Failed to calculate statistics")

    # Generate visualization
    output_filename = f"stats_{dataset_id[:8]}.png"
    output_path = Path(output_dir) / output_filename
    create_visualization(data_points, stats, output_path, dataset_name)

    # Return result
    result = {
        "status": "success",
        "imagePath": output_filename,
        "statistics": {
            "count": stats['count'],
            "mean": round(stats['mean'], 4),
            "std": round(stats['std'], 4),
            "min": round(stats['min'], 4),
            "q1": round(stats['q1'], 4),
            "median": round(stats['median'], 4),
            "q3": round(stats['q3'], 4),
            "max": round(stats['max'], 4),
            "range": round(stats['range'], 4),
            "iqr": round(stats['iqr'], 4)
        },
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
        error_result = {
            "status": "error",
            "errorMessage": str(e),
            "errorType": type(e).__name__
        }
        print(json.dumps(error_result))
        sys.exit(1)
