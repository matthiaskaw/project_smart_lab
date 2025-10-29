#!/usr/bin/env python3
"""
Test script to verify SmartLab <-> Dummy Device communication on Linux.
This simulates what the SmartLab C# app does.
"""

import socket
import os
import sys
import time
import subprocess
import signal

# Test configuration
DEVICE_ID = "test_comm_device_123"
SERVER_TO_CLIENT_SOCKET = f"/tmp/CoreFxPipe_serverToClient_{DEVICE_ID}"
CLIENT_TO_SERVER_SOCKET = f"/tmp/CoreFxPipe_clientToServer_{DEVICE_ID}"

def cleanup_sockets():
    """Clean up any existing socket files."""
    for path in [SERVER_TO_CLIENT_SOCKET, CLIENT_TO_SERVER_SOCKET]:
        try:
            if os.path.exists(path):
                os.remove(path)
                print(f"✓ Cleaned up {path}")
        except Exception as e:
            print(f"⚠ Could not remove {path}: {e}")

def create_server_sockets():
    """Create server-side UNIX domain sockets (simulating SmartLab)."""
    print("\n[Server] Creating server sockets...")

    # Socket for server → client (we write to this)
    server_to_client = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    server_to_client.bind(SERVER_TO_CLIENT_SOCKET)
    server_to_client.listen(1)
    print(f"✓ Server-to-client socket listening at {SERVER_TO_CLIENT_SOCKET}")

    # Socket for client → server (we read from this)
    client_to_server = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    client_to_server.bind(CLIENT_TO_SERVER_SOCKET)
    client_to_server.listen(1)
    print(f"✓ Client-to-server socket listening at {CLIENT_TO_SERVER_SOCKET}")

    return server_to_client, client_to_server

def start_dummy_device():
    """Start the dummy device process."""
    print("\n[Device] Starting dummy device...")

    device_script = "/mnt/e/Matthias/Dokumente/GitHub/dummy_finite_measurement/dummy_finite_measurement.py"
    process = subprocess.Popen(
        ["python3", device_script, DEVICE_ID],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=1
    )

    print(f"✓ Dummy device started with PID {process.pid}")
    time.sleep(1)  # Give it time to start

    return process

def test_communication(server_to_client_sock, client_to_server_sock, device_process):
    """Test the actual communication."""
    print("\n[Test] Waiting for device connections...")

    # Accept connections from device
    print("  Accepting server-to-client connection...")
    write_conn, _ = server_to_client_sock.accept()
    print("  ✓ Server-to-client connected")

    print("  Accepting client-to-server connection...")
    read_conn, _ = client_to_server_sock.accept()
    print("  ✓ Client-to-server connected")

    print("\n[Test] Both pipes connected! Testing communication...\n")

    # Test 1: INITIALIZE command
    print("─" * 60)
    print("Test 1: INITIALIZE command")
    print("─" * 60)
    command = "INITIALIZE\n"
    print(f"→ Sending: {command.strip()}")
    write_conn.sendall(command.encode('utf-8'))

    response = read_conn.recv(4096).decode('utf-8').strip()
    print(f"← Received: {response}")

    if "Dummy Finite Device" in response:
        print("✅ INITIALIZE test PASSED")
    else:
        print("❌ INITIALIZE test FAILED")

    # Test 2: GETPARAMETERS command
    print("\n" + "─" * 60)
    print("Test 2: GETPARAMETERS command")
    print("─" * 60)
    command = "GETPARAMETERS\n"
    print(f"→ Sending: {command.strip()}")
    write_conn.sendall(command.encode('utf-8'))

    response = read_conn.recv(4096).decode('utf-8').strip()
    print(f"← Received: {response[:100]}..." if len(response) > 100 else f"← Received: {response}")

    if "PARAMETERS" in response and "dataPoints" in response:
        print("✅ GETPARAMETERS test PASSED")
    else:
        print("❌ GETPARAMETERS test FAILED")

    # Test 3: SETPARAMETERS command
    print("\n" + "─" * 60)
    print("Test 3: SETPARAMETERS command")
    print("─" * 60)
    import json
    params = {"dataPoints": 5, "measurementType": "temperature"}
    command = f"SETPARAMETERS:{json.dumps(params)}\n"
    print(f"→ Sending: SETPARAMETERS:{params}")
    write_conn.sendall(command.encode('utf-8'))

    response = read_conn.recv(4096).decode('utf-8').strip()
    print(f"← Received: {response}")

    if "PARAMS_SET" in response:
        print("✅ SETPARAMETERS test PASSED")
    else:
        print("❌ SETPARAMETERS test FAILED")

    # Test 4: FINISH command
    print("\n" + "─" * 60)
    print("Test 4: FINISH command (shutdown)")
    print("─" * 60)
    command = "FINISH\n"
    print(f"→ Sending: {command.strip()}")
    write_conn.sendall(command.encode('utf-8'))

    response = read_conn.recv(4096).decode('utf-8').strip()
    print(f"← Received: {response}")

    if "FINISHED" in response:
        print("✅ FINISH test PASSED")
    else:
        print("❌ FINISH test FAILED")

    # Close connections
    write_conn.close()
    read_conn.close()

    # Wait for device to exit
    device_process.wait(timeout=5)

    print("\n" + "═" * 60)
    print("✅ ALL COMMUNICATION TESTS PASSED!")
    print("═" * 60)

def main():
    """Main test function."""
    print("=" * 60)
    print("SmartLab <-> Dummy Device Communication Test (Linux)")
    print("=" * 60)

    device_process = None
    server_to_client = None
    client_to_server = None

    try:
        # Step 1: Cleanup
        cleanup_sockets()

        # Step 2: Create server sockets
        server_to_client, client_to_server = create_server_sockets()

        # Step 3: Start device
        device_process = start_dummy_device()

        # Step 4: Test communication
        test_communication(server_to_client, client_to_server, device_process)

        return 0

    except Exception as e:
        print(f"\n❌ Test failed with error: {e}")
        import traceback
        traceback.print_exc()
        return 1

    finally:
        # Cleanup
        print("\n[Cleanup]")
        if device_process and device_process.poll() is None:
            print("  Terminating device process...")
            device_process.terminate()
            device_process.wait(timeout=5)

        if server_to_client:
            server_to_client.close()
        if client_to_server:
            client_to_server.close()

        cleanup_sockets()
        print("  ✓ Cleanup complete")

if __name__ == "__main__":
    sys.exit(main())
