#!/usr/bin/env python3
"""
Python script validator using AST analysis.
This script is called by SmartLab's ScriptValidationService to perform
deep security and structural analysis of user-uploaded Python scripts.
"""

import ast
import sys
import json


BLOCKED_MODULES = {
    'os', 'subprocess', 'socket', 'multiprocessing',
    'threading', 'ctypes', 'pickle', 'marshal',
    'urllib', 'requests', 'http', 'ftplib', 'smtplib',
    'shutil', 'glob'  # File system operations (except pathlib for Path objects)
}

BLOCKED_FUNCTIONS = {
    'eval', 'exec', 'compile', '__import__',
    'open', 'input', 'raw_input'
}

ALLOWED_MODULES = {
    'json', 'sys', 'math', 'statistics', 'datetime',
    'matplotlib', 'matplotlib.pyplot', 'numpy', 'scipy', 'pandas',
    'pathlib'  # Allowed for Path object manipulation (safer than os.path)
}


def validate_security(code):
    """Perform AST-based security validation."""
    violations = []
    warnings = []

    try:
        tree = ast.parse(code)
    except SyntaxError as e:
        return {
            'violations': [f"Syntax error at line {e.lineno}: {e.msg}"],
            'warnings': []
        }

    for node in ast.walk(tree):
        # Check imports
        if isinstance(node, ast.Import):
            for alias in node.names:
                module_name = alias.name.split('.')[0]
                if module_name in BLOCKED_MODULES:
                    violations.append({
                        'code': 'BLOCKED_IMPORT',
                        'message': f"Blocked import: {alias.name}",
                        'line': node.lineno
                    })
                elif module_name not in ALLOWED_MODULES and not module_name.startswith('_'):
                    warnings.append({
                        'code': 'UNKNOWN_IMPORT',
                        'message': f"Unknown/unverified import: {alias.name}",
                        'line': node.lineno
                    })

        if isinstance(node, ast.ImportFrom):
            module_name = (node.module or '').split('.')[0]
            if module_name in BLOCKED_MODULES:
                violations.append({
                    'code': 'BLOCKED_IMPORT',
                    'message': f"Blocked import from: {node.module}",
                    'line': node.lineno
                })
            elif module_name not in ALLOWED_MODULES and not module_name.startswith('_'):
                warnings.append({
                    'code': 'UNKNOWN_IMPORT',
                    'message': f"Unknown/unverified import from: {node.module}",
                    'line': node.lineno
                })

        # Check function calls
        if isinstance(node, ast.Call):
            func_name = None
            if isinstance(node.func, ast.Name):
                func_name = node.func.id
            elif isinstance(node.func, ast.Attribute):
                func_name = node.func.attr

            if func_name in BLOCKED_FUNCTIONS:
                violations.append({
                    'code': 'BLOCKED_FUNCTION',
                    'message': f"Blocked function call: {func_name}",
                    'line': node.lineno
                })

        # Check for dangerous attribute access
        if isinstance(node, ast.Attribute):
            if node.attr in ['__import__', '__builtins__', '__globals__']:
                violations.append({
                    'code': 'DANGEROUS_ATTRIBUTE',
                    'message': f"Dangerous attribute access: {node.attr}",
                    'line': node.lineno
                })

    return {
        'violations': violations,
        'warnings': warnings
    }


def validate_structure(code):
    """Validate script structure (required imports, patterns)."""
    warnings = []

    # Check for required imports
    has_json = 'import json' in code
    has_sys = 'import sys' in code
    has_matplotlib = 'matplotlib' in code

    if not has_json:
        warnings.append({
            'code': 'MISSING_JSON_IMPORT',
            'message': 'Script should import json for I/O',
            'line': None
        })

    if not has_sys:
        warnings.append({
            'code': 'MISSING_SYS_IMPORT',
            'message': 'Script should import sys for stdin/stdout',
            'line': None
        })

    # Check for main guard
    if "if __name__" not in code:
        warnings.append({
            'code': 'MISSING_MAIN_GUARD',
            'message': 'Script should use if __name__ == "__main__" guard',
            'line': None
        })

    # Check for matplotlib backend setting
    if has_matplotlib and "matplotlib.use" not in code:
        warnings.append({
            'code': 'MISSING_MATPLOTLIB_BACKEND',
            'message': "matplotlib should set backend with matplotlib.use('Agg')",
            'line': None
        })

    return warnings


def main():
    """Main entry point."""
    if len(sys.argv) > 1:
        # Read from file if path provided
        with open(sys.argv[1], 'r', encoding='utf-8') as f:
            code = f.read()
    else:
        # Read from stdin
        code = sys.stdin.read()

    # Perform security validation
    security_result = validate_security(code)

    # Perform structure validation
    structure_warnings = validate_structure(code)

    # Combine results
    result = {
        'violations': security_result['violations'],
        'warnings': security_result['warnings'] + structure_warnings
    }

    # Output JSON result
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
