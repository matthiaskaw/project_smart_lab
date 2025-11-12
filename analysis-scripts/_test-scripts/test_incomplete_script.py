#!/usr/bin/env python3
"""
Script missing required imports and structure - should get warnings.
"""

# Missing: import json
# Missing: import sys
import matplotlib.pyplot as plt

def analyze():
    # Process data (but no proper I/O)
    values = [1, 2, 3, 4, 5]

    plt.plot(values)
    plt.savefig('output.png')

    print("Done")

# Missing: if __name__ == '__main__' guard
analyze()
