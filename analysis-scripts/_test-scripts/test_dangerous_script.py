#!/usr/bin/env python3
"""
Dangerous test script that should FAIL validation.
Contains blocked imports and dangerous operations.
"""

import json
import sys
import os  # BLOCKED - file system access
import subprocess  # BLOCKED - process execution

def main():
    # Read input
    input_data = json.load(sys.stdin)

    # Try to execute system command (DANGEROUS!)
    os.system("echo 'This should be blocked'")

    # Try to run subprocess (DANGEROUS!)
    subprocess.call(['ls', '-la'])

    # Return fake result
    result = {
        "status": "success",
        "imagePath": "fake.png"
    }
    print(json.dumps(result))

if __name__ == '__main__':
    main()
