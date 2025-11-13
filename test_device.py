#!/usr/bin/env python3
"""
Standalone Test Runner for SmartLab Device Scripts
===================================================

This script simulates the SmartLab app's IPC communication with device scripts,
allowing you to test device implementations locally without running the full app.

Usage:
    python test_device.py <path_to_device_script.py> [--parameters '{"key": "value"}']

Examples:
    # Basic test with default parameters
    python test_device.py ../smartlab_devices/dummy_finite_measurement.py

    # Test with custom parameters
    python test_device.py ../smartlab_devices/dummy_finite_measurement.py --parameters '{"dataPoints": 20}'
"""

import asyncio
import json
import sys
import subprocess
import time
import os
import platform
import uuid
from pathlib import Path
from datetime import datetime
import argparse

# Platform-specific imports
IS_WINDOWS = platform.system() == "Windows"
IS_LINUX = platform.system() == "Linux"

if IS_WINDOWS:
    import win32pipe
    import win32file
    import pywintypes
elif IS_LINUX:
    import socket


class DeviceTestHarness:
    """
    Test harness that simulates SmartLab's communication with a device.

    Creates named pipes/sockets, launches the device process, and
    executes the full command protocol workflow.
    """

    def __init__(self, device_script_path: str, custom_parameters: dict = None):
        """
        Initialize the test harness.

        Args:
            device_script_path: Path to the device Python script to test
            custom_parameters: Optional custom parameters to pass to device
        """
        self.device_script_path = Path(device_script_path)
        self.custom_parameters = custom_parameters or {}
        self.device_id = str(uuid.uuid4())
        self.device_process = None

        # Platform-specific pipe names
        if IS_WINDOWS:
            self.server_to_client_pipe = f"\\\\.\\pipe\\serverToClient_{self.device_id}"
            self.client_to_server_pipe = f"\\\\.\\pipe\\clientToServer_{self.device_id}"
            self.pipe_read_handle = None
            self.pipe_write_handle = None
        elif IS_LINUX:
            self.server_to_client_pipe = f"/tmp/CoreFxPipe_serverToClient_{self.device_id}"
            self.client_to_server_pipe = f"/tmp/CoreFxPipe_clientToServer_{self.device_id}"
            self.server_socket_read = None
            self.server_socket_write = None
            self.client_socket_read = None
            self.client_socket_write = None

        print(f"Platform: {platform.system()}")
        print(f"Device ID: {self.device_id}")

    async def run_test(self):
        """
        Execute the full device test workflow.

        Returns:
            Tuple of (success, results_dict, error_message)
        """
        try:
            print("\n" + "=" * 80)
            print("DEVICE TEST WORKFLOW")
            print("=" * 80)
            print(f"Device Script: {self.device_script_path}")
            print()

            # Step 1: Create pipes
            print("[1/7] Creating IPC pipes...")
            await self.create_pipes()
            print("  [OK] Pipes created\n")

            # Step 2: Launch device process
            print("[2/7] Launching device process...")
            await self.launch_device()
            print("  [OK] Device process started\n")

            await asyncio.sleep(1)  # Give device time to start

            # Step 3: Connect to pipes
            print("[3/7] Connecting to pipes...")
            await self.connect_to_pipes()
            print("  [OK] Connected to pipes\n")

            # Step 4: Initialize device
            print("[4/7] Initializing device (INITIALIZE command)...")
            device_name = await self.initialize_device()
            print(f"  [OK] Device initialized: {device_name}\n")

            # Step 5: Get and display parameters
            print("[5/7] Getting parameter definitions (GETPARAMETERS command)...")
            param_definitions = await self.get_parameters()
            print(f"  [OK] Received {len(param_definitions)} parameter definitions:")
            for param in param_definitions:
                print(f"      - {param.get('displayName', param['name'])}: {param['type']} (default: {param.get('defaultValue')})")
            print()

            # Step 6: Set parameters (merge defaults with custom)
            print("[6/7] Setting parameters (SETPARAMETERS command)...")
            parameters = self._build_parameters(param_definitions)
            await self.set_parameters(parameters)
            print(f"  [OK] Parameters set:")
            for key, value in parameters.items():
                print(f"      {key}: {value}")
            print()

            # Step 7: Get measurement data
            print("[7/7] Getting measurement data (GETDATA_STRUCTURED command)...")
            measurement_data = await self.get_data()
            print(f"  [OK] Received measurement data")
            print(f"      Raw data entries: {len(measurement_data.get('rawData', []))}")
            print(f"      Timestamp: {measurement_data.get('timestamp', 'N/A')}")
            print()

            # Display sample data
            print("-" * 80)
            print("MEASUREMENT DATA SAMPLE")
            print("-" * 80)
            raw_data = measurement_data.get('rawData', [])
            if raw_data:
                print(f"Total data points: {len(raw_data)}")
                print("\nFirst 5 data points:")
                for i, data_str in enumerate(raw_data[:5]):
                    try:
                        data_point = json.loads(data_str)
                        print(f"  {i+1}. {data_point}")
                    except:
                        print(f"  {i+1}. {data_str}")

                if len(raw_data) > 5:
                    print(f"\n... ({len(raw_data) - 5} more data points)")
            else:
                print("No data points received")
            print()

            # Display parameters used
            print("Parameters used in measurement:")
            for key, value in measurement_data.get('parameters', {}).items():
                print(f"  {key}: {value}")
            print()

            # Clean shutdown
            print("Sending FINISH command...")
            await self.finish_device()
            print("[OK] Device finished\n")

            print("=" * 80)
            print("TEST COMPLETED SUCCESSFULLY")
            print("=" * 80)

            return True, {
                'deviceName': device_name,
                'parameters': param_definitions,
                'measurementData': measurement_data
            }, None

        except Exception as e:
            print(f"\n[ERROR] Test failed: {e}")
            import traceback
            traceback.print_exc()
            return False, None, str(e)

        finally:
            await self.cleanup()

    def _build_parameters(self, param_definitions: list) -> dict:
        """
        Build parameter dictionary from definitions and custom overrides.

        Args:
            param_definitions: List of parameter definition dicts

        Returns:
            Dictionary of parameter name -> value
        """
        parameters = {}

        # Start with defaults from definitions
        for param in param_definitions:
            param_name = param['name']
            parameters[param_name] = param.get('defaultValue')

        # Override with custom parameters
        parameters.update(self.custom_parameters)

        return parameters

    # =========================================================================
    # Platform-Specific IPC Implementation
    # =========================================================================

    async def create_pipes(self):
        """Create named pipes or UNIX sockets depending on platform."""
        if IS_WINDOWS:
            await self._create_pipes_windows()
        elif IS_LINUX:
            await self._create_pipes_linux()

    async def _create_pipes_windows(self):
        """Create Windows named pipes."""
        # Create server-to-client pipe (we write, device reads)
        self.pipe_write_handle = win32pipe.CreateNamedPipe(
            self.server_to_client_pipe,
            win32pipe.PIPE_ACCESS_OUTBOUND,
            win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_WAIT,
            1, 65536, 65536, 0, None
        )

        # Create client-to-server pipe (we read, device writes)
        self.pipe_read_handle = win32pipe.CreateNamedPipe(
            self.client_to_server_pipe,
            win32pipe.PIPE_ACCESS_INBOUND,
            win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_WAIT,
            1, 65536, 65536, 0, None
        )

    async def _create_pipes_linux(self):
        """Create UNIX domain sockets for Linux."""
        # Clean up any existing socket files
        for path in [self.server_to_client_pipe, self.client_to_server_pipe]:
            if os.path.exists(path):
                os.remove(path)

        # Create server socket for server-to-client (we write, device reads)
        self.server_socket_write = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        self.server_socket_write.bind(self.server_to_client_pipe)
        self.server_socket_write.listen(1)

        # Create server socket for client-to-server (we read, device writes)
        self.server_socket_read = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        self.server_socket_read.bind(self.client_to_server_pipe)
        self.server_socket_read.listen(1)

    async def connect_to_pipes(self):
        """Wait for device to connect to pipes."""
        if IS_WINDOWS:
            await self._connect_pipes_windows()
        elif IS_LINUX:
            await self._connect_pipes_linux()

    async def _connect_pipes_windows(self):
        """Wait for device to connect to Windows named pipes."""
        # Wait for device to connect to both pipes
        print("  Waiting for device to connect to write pipe...")
        win32pipe.ConnectNamedPipe(self.pipe_write_handle, None)
        print("  Device connected to write pipe")

        print("  Waiting for device to connect to read pipe...")
        win32pipe.ConnectNamedPipe(self.pipe_read_handle, None)
        print("  Device connected to read pipe")

    async def _connect_pipes_linux(self):
        """Accept device connections on UNIX sockets."""
        print("  Waiting for device to connect to write socket...")
        self.client_socket_write, _ = self.server_socket_write.accept()
        print("  Device connected to write socket")

        print("  Waiting for device to connect to read socket...")
        self.client_socket_read, _ = self.server_socket_read.accept()
        print("  Device connected to read socket")

    async def launch_device(self):
        """Launch the device script as a subprocess."""
        python_cmd = "python3" if not IS_WINDOWS else "python"

        self.device_process = subprocess.Popen(
            [python_cmd, str(self.device_script_path), self.device_id],
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1
        )

        print(f"  Device process PID: {self.device_process.pid}")

    async def send_command(self, command: str):
        """
        Send a command to the device.

        Args:
            command: Command string to send
        """
        command_bytes = (command + '\n').encode('utf-8')

        if IS_WINDOWS:
            win32file.WriteFile(self.pipe_write_handle, command_bytes)
        elif IS_LINUX:
            self.client_socket_write.sendall(command_bytes)

    async def receive_response(self, timeout: float = 10.0) -> str:
        """
        Receive a response from the device.

        Args:
            timeout: Maximum time to wait for response in seconds

        Returns:
            Response string from device
        """
        if IS_WINDOWS:
            return await self._receive_response_windows(timeout)
        elif IS_LINUX:
            return await self._receive_response_linux(timeout)

    async def _receive_response_windows(self, timeout: float) -> str:
        """Receive response from Windows named pipe."""
        start_time = time.time()

        while time.time() - start_time < timeout:
            try:
                result, data = win32file.ReadFile(self.pipe_read_handle, 64*1024)
                if data:
                    return data.decode('utf-8').strip()
            except pywintypes.error as e:
                if e.args[0] == 109:  # ERROR_BROKEN_PIPE
                    raise ConnectionError("Pipe broken")
                await asyncio.sleep(0.1)

        raise TimeoutError(f"No response received within {timeout} seconds")

    async def _receive_response_linux(self, timeout: float) -> str:
        """Receive response from UNIX socket."""
        self.client_socket_read.settimeout(timeout)

        buffer = b''
        while b'\n' not in buffer:
            chunk = self.client_socket_read.recv(4096)
            if not chunk:
                raise ConnectionError("Socket closed by device")
            buffer += chunk

        line, _ = buffer.split(b'\n', 1)
        return line.decode('utf-8').strip()

    # =========================================================================
    # Device Command Protocol
    # =========================================================================

    async def initialize_device(self) -> str:
        """
        Send INITIALIZE command and return device name.

        Returns:
            Device name string
        """
        await self.send_command("INITIALIZE")
        response = await self.receive_response()
        return response

    async def get_parameters(self) -> list:
        """
        Send GETPARAMETERS command and return parameter definitions.

        Returns:
            List of parameter definition dictionaries
        """
        await self.send_command("GETPARAMETERS")
        response = await self.receive_response()

        # Response format: "PARAMETERS {json}"
        if not response.startswith("PARAMETERS "):
            raise ValueError(f"Unexpected response format: {response}")

        json_part = response[11:]  # Remove "PARAMETERS " prefix
        return json.loads(json_part)

    async def set_parameters(self, parameters: dict):
        """
        Send SETPARAMETERS command with parameter values.

        Args:
            parameters: Dictionary of parameter name -> value
        """
        json_params = json.dumps(parameters)
        await self.send_command(f"SETPARAMETERS:{json_params}")
        response = await self.receive_response()

        if response != "PARAMS_SET":
            raise ValueError(f"Failed to set parameters: {response}")

    async def get_data(self) -> dict:
        """
        Send GETDATA_STRUCTURED command and return measurement data.

        Returns:
            Dictionary with measurement data in format:
            {
                "rawData": ["json_string1", "json_string2", ...],
                "timestamp": "iso_timestamp",
                "parameters": {...}
            }
        """
        await self.send_command("GETDATA_STRUCTURED")
        response = await self.receive_response(timeout=60.0)  # Longer timeout for measurements

        # Response format: "DATA:{json}"
        if not response.startswith("DATA:"):
            raise ValueError(f"Unexpected response format: {response[:100]}")

        json_part = response[5:]  # Remove "DATA:" prefix
        return json.loads(json_part)

    async def finish_device(self):
        """Send FINISH command to cleanly shut down device."""
        await self.send_command("FINISH")
        response = await self.receive_response()

        if response != "FINISHED":
            print(f"  Warning: Unexpected finish response: {response}")

    # =========================================================================
    # Cleanup
    # =========================================================================

    async def cleanup(self):
        """Clean up resources and terminate device process."""
        print("\nCleaning up...")

        # Close pipe handles
        try:
            if IS_WINDOWS:
                if self.pipe_read_handle:
                    win32file.CloseHandle(self.pipe_read_handle)
                if self.pipe_write_handle:
                    win32file.CloseHandle(self.pipe_write_handle)
            elif IS_LINUX:
                if self.client_socket_read:
                    self.client_socket_read.close()
                if self.client_socket_write:
                    self.client_socket_write.close()
                if self.server_socket_read:
                    self.server_socket_read.close()
                if self.server_socket_write:
                    self.server_socket_write.close()

                # Clean up socket files
                for path in [self.server_to_client_pipe, self.client_to_server_pipe]:
                    if os.path.exists(path):
                        os.remove(path)
        except Exception as e:
            print(f"Error closing pipes: {e}")

        # Terminate device process if still running
        if self.device_process:
            try:
                if self.device_process.poll() is None:
                    print("Terminating device process...")
                    self.device_process.terminate()
                    self.device_process.wait(timeout=5)

                # Read any remaining output
                if self.device_process.stdout:
                    remaining = self.device_process.stdout.read()
                    if remaining:
                        print("\nDevice output:")
                        print(remaining)
            except Exception as e:
                print(f"Error terminating device process: {e}")

        print("Cleanup complete")


async def main():
    """Main entry point for the device test runner."""
    parser = argparse.ArgumentParser(
        description="Test SmartLab device implementations locally",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Basic test with default parameters
  python test_device.py ../smartlab_devices/dummy_finite_measurement.py

  # Test with custom parameters
  python test_device.py ../smartlab_devices/dummy_finite_measurement.py --parameters '{"dataPoints": 20}'
        """
    )

    parser.add_argument('device_script', help='Path to the device Python script')
    parser.add_argument('--parameters', '-p', help='Custom parameters as JSON string', default='{}')

    args = parser.parse_args()

    # Parse custom parameters
    try:
        custom_parameters = json.loads(args.parameters)
    except json.JSONDecodeError as e:
        print(f"ERROR: Invalid JSON in --parameters: {e}")
        sys.exit(1)

    # Check if device script exists
    device_path = Path(args.device_script)
    if not device_path.exists():
        print(f"ERROR: Device script not found: {device_path}")
        sys.exit(1)

    # Run the test
    harness = DeviceTestHarness(args.device_script, custom_parameters)
    success, results, error = await harness.run_test()

    if not success:
        print(f"\nTEST FAILED: {error}")
        sys.exit(1)

    sys.exit(0)


if __name__ == '__main__':
    asyncio.run(main())
