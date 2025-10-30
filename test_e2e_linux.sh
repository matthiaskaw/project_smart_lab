#!/bin/bash
#
# End-to-End Test: SmartLab App + Dummy Device on Linux
#

set -e

echo "=============================================="
echo "End-to-End Linux Test"
echo "=============================================="
echo ""

# Cleanup function
cleanup() {
    echo ""
    echo "[Cleanup] Stopping processes..."
    pkill -f "smartlab" || true
    pkill -f "dummy_finite_measurement.py" || true
    # Clean up socket files
    rm -f /tmp/CoreFxPipe_* || true
    echo "[Cleanup] Done"
}

trap cleanup EXIT

# Clean up any existing processes
cleanup

echo "[1] Starting SmartLab app..."
cd /mnt/e/Matthias/Dokumente/GitHub/project_smart_lab/publish/linux-x64
./smartlab > /tmp/smartlab_e2e.log 2>&1 &
SMARTLAB_PID=$!
echo "    SmartLab PID: $SMARTLAB_PID"

# Wait for SmartLab to start
sleep 3

# Check if SmartLab is running
if ! kill -0 $SMARTLAB_PID 2>/dev/null; then
    echo "    ERROR: SmartLab failed to start"
    cat /tmp/smartlab_e2e.log
    exit 1
fi

echo "    ✓ SmartLab started"
echo ""

echo "[2] Testing SmartLab web interface..."
if curl -s http://localhost:5000 > /dev/null; then
    echo "    ✓ Web interface accessible at http://localhost:5000"
else
    echo "    ERROR: Web interface not accessible"
    exit 1
fi
echo ""

echo "[3] Starting dummy device..."
cd /mnt/e/Matthias/Dokumente/GitHub/dummy_finite_measurement
DEVICE_ID="linux_e2e_test_$(date +%s)"
python3 dummy_finite_measurement.py $DEVICE_ID > /tmp/dummy_device_e2e.log 2>&1 &
DEVICE_PID=$!
echo "    Device ID: $DEVICE_ID"
echo "    Device PID: $DEVICE_PID"

# Wait for device to start
sleep 2

# Check if device is running
if ! kill -0 $DEVICE_PID 2>/dev/null; then
    echo "    ERROR: Device failed to start"
    cat /tmp/dummy_device_e2e.log
    exit 1
fi

echo "    ✓ Dummy device started"
echo ""

echo "[4] Checking socket files..."
SOCKET1="/tmp/CoreFxPipe_serverToClient_$DEVICE_ID"
SOCKET2="/tmp/CoreFxPipe_clientToServer_$DEVICE_ID"

sleep 2

if [ -S "$SOCKET1" ] && [ -S "$SOCKET2" ]; then
    echo "    ✓ Socket files exist (device is listening)"
    ls -la /tmp/CoreFxPipe_*$DEVICE_ID* || true
else
    echo "    ⚠ Socket files not found yet (SmartLab hasn't connected)"
    echo "    This is normal - sockets are created when SmartLab connects"
fi
echo ""

echo "[5] System Status:"
echo "    SmartLab: Running (PID $SMARTLAB_PID)"
echo "    Device:   Running (PID $DEVICE_PID)"
echo "    Device ID: $DEVICE_ID"
echo ""

echo "=============================================="
echo "✓ Both systems running on Linux!"
echo "=============================================="
echo ""
echo "To test the connection:"
echo "1. Open browser to http://localhost:5000"
echo "2. Add a new device with:"
echo "   - Device ID: $DEVICE_ID"
echo "   - Executable: /mnt/e/Matthias/Dokumente/GitHub/dummy_finite_measurement/dummy_finite_measurement.py"
echo "3. Start a measurement"
echo ""
echo "Logs:"
echo "  SmartLab: /tmp/smartlab_e2e.log"
echo "  Device:   /tmp/dummy_device_e2e.log"
echo ""
echo "Press Ctrl+C to stop both processes..."

# Keep running
wait $SMARTLAB_PID
