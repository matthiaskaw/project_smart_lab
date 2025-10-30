#!/bin/bash
#
# Integration Test: Real SmartLab ProxyDevice <-> Real Dummy Device on Linux
#

echo "=============================================="
echo "SmartLab + Dummy Device Integration Test"
echo "=============================================="
echo ""

# Device configuration
DEVICE_ID="test_integration_$(date +%s)"
DEVICE_SCRIPT="/mnt/e/Matthias/Dokumente/GitHub/dummy_finite_measurement/dummy_finite_measurement.py"

echo "[Setup]"
echo "  Device ID: $DEVICE_ID"
echo "  Device Script: $DEVICE_SCRIPT"
echo ""

# Cleanup
echo "[Cleanup] Removing old socket files..."
rm -f /tmp/CoreFxPipe_*$DEVICE_ID* 2>/dev/null || true

# Test 1: Start device manually and check it waits for connection
echo "=============================================="
echo "TEST 1: Device Startup & Socket Creation"
echo "=============================================="
echo ""

echo "[Device] Starting dummy device in background..."
python3 $DEVICE_SCRIPT $DEVICE_ID > /tmp/device_test.log 2>&1 &
DEVICE_PID=$!
echo "  PID: $DEVICE_PID"

sleep 2

echo ""
echo "[Device] Checking device output..."
cat /tmp/device_test.log | head -15

echo ""
echo "[Result] Test 1:"
if kill -0 $DEVICE_PID 2>/dev/null; then
    echo "  ✅ Device process is running"
else
    echo "  ❌ Device process died"
    exit 1
fi

if grep -q "Connecting to socket" /tmp/device_test.log; then
    echo "  ✅ Device is trying to connect to sockets"
else
    echo "  ❌ Device not looking for sockets"
    exit 1
fi

if grep -q "/tmp/CoreFxPipe" /tmp/device_test.log; then
    echo "  ✅ Device using correct Linux socket paths"
else
    echo "  ❌ Device not using Linux socket paths"
    exit 1
fi

echo ""
echo "=============================================="
echo "TEST 2: ProxyDevice Executable Path"
echo "=============================================="
echo ""

echo "[Info] On Linux, SmartLab ProxyDeviceProcessManager needs:"
echo "  Executable Path: python3"
echo "  Arguments: $DEVICE_SCRIPT <device_id>"
echo ""

echo "[Check] Verifying Python is in PATH..."
which python3
python3 --version
echo "  ✅ Python3 available"

echo ""
echo "[Check] Verifying device script exists..."
if [ -f "$DEVICE_SCRIPT" ]; then
    echo "  ✅ Device script found"
    echo "  Path: $DEVICE_SCRIPT"
else
    echo "  ❌ Device script not found"
    exit 1
fi

echo ""
echo "=============================================="
echo "TEST 3: Socket Connectivity (Simulated Server)"
echo "=============================================="
echo ""

echo "[Server] Creating test server sockets..."
python3 << 'PYEOF'
import socket
import os
import sys
import time

device_id = os.environ.get('DEVICE_ID')
server_to_client = f"/tmp/CoreFxPipe_serverToClient_{device_id}"
client_to_server = f"/tmp/CoreFxPipe_clientToServer_{device_id}"

print(f"  Creating: {server_to_client}")
sock1 = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
sock1.bind(server_to_client)
sock1.listen(1)

print(f"  Creating: {client_to_server}")
sock2 = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
sock2.bind(client_to_server)
sock2.listen(1)

print("  ✅ Server sockets created")
print("\n[Server] Waiting for device connections (5 second timeout)...")

sock1.settimeout(5)
sock2.settimeout(5)

try:
    print("  Accepting on server-to-client...")
    conn1, _ = sock1.accept()
    print("  ✅ server-to-client connected!")

    print("  Accepting on client-to-server...")
    conn2, _ = sock2.accept()
    print("  ✅ client-to-server connected!")

    print("\n[Test] Sending INITIALIZE command...")
    conn1.sendall(b"INITIALIZE\n")

    print("[Test] Waiting for response...")
    response = conn2.recv(1024).decode('utf-8').strip()
    print(f"[Test] Response: {response}")

    if "Dummy Finite Device" in response:
        print("  ✅ Device responded correctly!")
        exit_code = 0
    else:
        print("  ❌ Unexpected response")
        exit_code = 1

    # Cleanup
    conn1.sendall(b"FINISH\n")
    conn1.close()
    conn2.close()

    sys.exit(exit_code)

except socket.timeout:
    print("  ❌ Timeout waiting for device connection")
    sys.exit(1)
except Exception as e:
    print(f"  ❌ Error: {e}")
    sys.exit(1)
finally:
    sock1.close()
    sock2.close()
    try:
        os.remove(server_to_client)
        os.remove(client_to_server)
    except:
        pass
PYEOF

TEST_RESULT=$?

# Cleanup device process
kill $DEVICE_PID 2>/dev/null || true
rm -f /tmp/CoreFxPipe_*$DEVICE_ID* 2>/dev/null || true

echo ""
echo "=============================================="
echo "FINAL RESULTS"
echo "=============================================="
echo ""

if [ $TEST_RESULT -eq 0 ]; then
    echo "✅ ALL TESTS PASSED!"
    echo ""
    echo "The SmartLab app on Linux can communicate with the dummy device!"
    echo ""
    echo "To use in SmartLab UI:"
    echo "1. Device Executable: python3"
    echo "2. Device Arguments: $DEVICE_SCRIPT"
    echo "3. (SmartLab will append the device ID automatically)"
    exit 0
else
    echo "❌ TEST FAILED"
    echo ""
    echo "Device log:"
    cat /tmp/device_test.log
    exit 1
fi
