#!/usr/bin/env python3
"""
Script with syntax error - should fail syntax validation.
"""

import json
import sys

def main():
    input_data = json.load(sys.stdin)

    # Syntax error: missing closing parenthesis
    result = {
        "status": "success"

    print(json.dumps(result))

if __name__ == '__main__':
    main(
