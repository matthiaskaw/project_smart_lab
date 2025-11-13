#!/usr/bin/env python3
"""
Advanced Analysis Example - Anomaly Detection
==============================================
Demonstrates advanced analysis techniques including:
- Moving average smoothing
- Anomaly detection using standard deviation
- Multi-panel visualization
- Rich statistics output

Author: SmartLab Team
Version: 1.0.0
"""

import json
import sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from pathlib import Path
from datetime import datetime
import math

def calculate_moving_average(values, window_size=5):
    """Calculate simple moving average."""
    if len(values) < window_size:
        return values

    moving_avg = []
    for i in range(len(values)):
        if i < window_size - 1:
            # Not enough data points yet
            moving_avg.append(sum(values[:i+1]) / (i+1))
        else:
            # Full window
            window = values[i-window_size+1:i+1]
            moving_avg.append(sum(window) / window_size)

    return moving_avg


def detect_anomalies(values, threshold_std=2.0):
    """
    Detect anomalies using standard deviation method.

    Args:
        values: List of numeric values
        threshold_std: Number of standard deviations for anomaly threshold

    Returns:
        List of indices where anomalies were detected
    """
    if len(values) < 3:
        return []

    mean = sum(values) / len(values)
    variance = sum((x - mean) ** 2 for x in values) / len(values)
    std = math.sqrt(variance)

    anomaly_indices = []
    for i, value in enumerate(values):
        if abs(value - mean) > threshold_std * std:
            anomaly_indices.append(i)

    return anomaly_indices


def analyze_data(data_points, parameters):
    """
    Advanced analysis with moving average and anomaly detection.
    """
    # Extract parameters
    window_size = int(parameters.get('window_size', 5))
    anomaly_threshold = float(parameters.get('anomaly_threshold', 2.0))

    # Extract data
    values = [float(dp['value']) for dp in data_points]
    timestamps = [datetime.fromisoformat(dp['timestamp'].replace('Z', '+00:00'))
                  for dp in data_points]

    # Basic statistics
    n = len(values)
    mean_val = sum(values) / n
    variance = sum((x - mean_val) ** 2 for x in values) / n
    std_val = math.sqrt(variance)

    sorted_values = sorted(values)
    median_val = sorted_values[n // 2] if n % 2 == 1 else \
                 (sorted_values[n // 2 - 1] + sorted_values[n // 2]) / 2

    # Advanced analysis
    moving_avg = calculate_moving_average(values, window_size)
    anomalies = detect_anomalies(values, anomaly_threshold)

    # Calculate trend (simple linear regression slope)
    x_coords = list(range(n))
    x_mean = sum(x_coords) / n
    y_mean = mean_val

    numerator = sum((x_coords[i] - x_mean) * (values[i] - y_mean) for i in range(n))
    denominator = sum((x - x_mean) ** 2 for x in x_coords)

    slope = numerator / denominator if denominator != 0 else 0

    # Determine trend direction
    if abs(slope) < std_val * 0.1:
        trend = "stable"
    elif slope > 0:
        trend = "increasing"
    else:
        trend = "decreasing"

    results = {
        'count': n,
        'mean': mean_val,
        'std': std_val,
        'median': median_val,
        'min': sorted_values[0],
        'max': sorted_values[-1],
        'range': sorted_values[-1] - sorted_values[0],
        'anomaly_count': len(anomalies),
        'anomaly_percentage': (len(anomalies) / n * 100) if n > 0 else 0,
        'trend': trend,
        'trend_slope': slope,
        'window_size_used': window_size
    }

    return results, values, timestamps, moving_avg, anomalies


def create_visualization(values, timestamps, dataset_name, output_path,
                        analysis_results, moving_avg, anomalies):
    """
    Create comprehensive multi-panel visualization.
    """
    fig = plt.figure(figsize=(16, 10))
    gs = fig.add_gridspec(3, 2, hspace=0.3, wspace=0.3)

    # Panel 1: Time series with moving average and anomalies
    ax1 = fig.add_subplot(gs[0, :])
    ax1.plot(timestamps, values, 'b-', alpha=0.6, linewidth=1.5, label='Raw Data')
    ax1.plot(timestamps, moving_avg, 'r-', linewidth=2, label='Moving Average')

    # Highlight anomalies
    if anomalies:
        anomaly_times = [timestamps[i] for i in anomalies]
        anomaly_values = [values[i] for i in anomalies]
        ax1.scatter(anomaly_times, anomaly_values, color='red', s=100,
                   marker='o', edgecolors='darkred', linewidth=2,
                   label=f'Anomalies ({len(anomalies)})', zorder=5)

    ax1.set_title(f'{dataset_name} - Time Series Analysis', fontsize=14, fontweight='bold')
    ax1.set_xlabel('Time')
    ax1.set_ylabel('Value')
    ax1.legend(loc='best')
    ax1.grid(True, alpha=0.3)
    ax1.tick_params(axis='x', rotation=45)

    # Panel 2: Distribution histogram
    ax2 = fig.add_subplot(gs[1, 0])
    n, bins, patches = ax2.hist(values, bins=20, density=False, alpha=0.7,
                                 color='skyblue', edgecolor='black')

    ax2.axvline(analysis_results['mean'], color='r', linestyle='--',
               linewidth=2, label=f"Mean: {analysis_results['mean']:.2f}")
    ax2.axvline(analysis_results['median'], color='g', linestyle='--',
               linewidth=2, label=f"Median: {analysis_results['median']:.2f}")

    # Show ±1σ range
    ax2.axvline(analysis_results['mean'] - analysis_results['std'],
               color='orange', linestyle=':', linewidth=1.5, alpha=0.7)
    ax2.axvline(analysis_results['mean'] + analysis_results['std'],
               color='orange', linestyle=':', linewidth=1.5, alpha=0.7,
               label='±1σ')

    ax2.set_title('Distribution', fontsize=12, fontweight='bold')
    ax2.set_xlabel('Value')
    ax2.set_ylabel('Frequency')
    ax2.legend()
    ax2.grid(True, alpha=0.3, axis='y')

    # Panel 3: Deviation from moving average
    ax3 = fig.add_subplot(gs[1, 1])
    deviations = [values[i] - moving_avg[i] for i in range(len(values))]
    colors = ['red' if i in anomalies else 'blue' for i in range(len(deviations))]

    ax3.bar(range(len(deviations)), deviations, color=colors, alpha=0.6, edgecolor='black')
    ax3.axhline(0, color='black', linestyle='-', linewidth=1)
    ax3.set_title('Deviation from Moving Average', fontsize=12, fontweight='bold')
    ax3.set_xlabel('Data Point Index')
    ax3.set_ylabel('Deviation')
    ax3.grid(True, alpha=0.3, axis='y')

    # Panel 4: Statistics summary
    ax4 = fig.add_subplot(gs[2, 0])
    ax4.axis('off')

    stats_text = f"""
Statistical Summary
{'=' * 45}

Data Points:      {analysis_results['count']}
Mean:             {analysis_results['mean']:.4f}
Median:           {analysis_results['median']:.4f}
Std Deviation:    {analysis_results['std']:.4f}
Min:              {analysis_results['min']:.4f}
Max:              {analysis_results['max']:.4f}
Range:            {analysis_results['range']:.4f}

Trend:            {analysis_results['trend'].upper()}
Trend Slope:      {analysis_results['trend_slope']:.6f}

Anomalies Found:  {analysis_results['anomaly_count']}
Anomaly Rate:     {analysis_results['anomaly_percentage']:.2f}%
Window Size:      {analysis_results['window_size_used']}
    """

    ax4.text(0.1, 0.5, stats_text, fontfamily='monospace',
            fontsize=10, va='center')

    # Panel 5: Box plot
    ax5 = fig.add_subplot(gs[2, 1])
    bp = ax5.boxplot(values, vert=True, patch_artist=True,
                     widths=0.6, showmeans=True)

    bp['boxes'][0].set_facecolor('lightblue')
    bp['boxes'][0].set_edgecolor('black')
    bp['boxes'][0].set_linewidth(2)

    for element in ['whiskers', 'fliers', 'caps']:
        plt.setp(bp[element], color='black', linewidth=1.5)

    plt.setp(bp['medians'], color='red', linewidth=2)
    plt.setp(bp['means'], marker='D', markeredgecolor='green',
            markerfacecolor='green', markersize=8)

    ax5.set_title('Box Plot', fontsize=12, fontweight='bold')
    ax5.set_ylabel('Value')
    ax5.grid(True, alpha=0.3, axis='y')

    plt.suptitle(f'Advanced Analysis Report', fontsize=16, fontweight='bold', y=0.995)
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    plt.close()


def main():
    try:
        # Read input
        input_data = json.load(sys.stdin)
        output_dir = sys.argv[1] if len(sys.argv) > 1 else '.'

        dataset_id = input_data['datasetId']
        dataset_name = input_data['datasetName']
        data_points = input_data['dataPoints']
        parameters = input_data.get('parameters', {})

        # Validate
        if not data_points:
            raise ValueError("No data points provided")

        if len(data_points) < 5:
            raise ValueError("At least 5 data points required for this analysis")

        # Analyze
        results, values, timestamps, moving_avg, anomalies = \
            analyze_data(data_points, parameters)

        # Visualize
        output_filename = f"advanced_analysis_{dataset_id[:8]}.png"
        output_path = Path(output_dir) / output_filename
        create_visualization(values, timestamps, dataset_name, output_path,
                           results, moving_avg, anomalies)

        # Return result
        result = {
            "status": "success",
            "imagePath": output_filename,
            "statistics": {
                key: round(value, 4) if isinstance(value, float) else value
                for key, value in results.items()
            },
            "metadata": {
                "scriptVersion": "1.0.0",
                "scriptName": "Advanced Analysis - Anomaly Detection",
                "features": [
                    "Moving average smoothing",
                    "Anomaly detection",
                    "Trend analysis",
                    "Multi-panel visualization"
                ]
            }
        }

        print(json.dumps(result))

    except Exception as e:
        error_result = {
            "status": "error",
            "errorMessage": str(e),
            "errorType": type(e).__name__
        }
        print(json.dumps(error_result))
        sys.exit(1)


if __name__ == '__main__':
    main()
