"""
EyeGestures to Unity UDP Bridge (Enhanced)

This script runs EyeGestures tracking and sends gaze data to Unity via UDP.
Supports calibration mode, headless operation, and bidirectional communication.

Requirements:
    Auto-installed from GitHub on first run via run_eye_tracker.bat

Usage:
    python eyeGestures_to_unity.py              # Normal mode with preview
    python eyeGestures_to_unity.py --headless   # No preview window
    python eyeGestures_to_unity.py --calibrate  # Start in calibration mode
"""

import socket
import json
import argparse
import threading
import time
import os
import sys
import subprocess
from enum import Enum
from pathlib import Path

# ── Local lib path (dependencies installed here by run_eye_tracker.bat) ──
LIB_DIR = Path(__file__).parent / "lib"
if LIB_DIR.exists():
    sys.path.insert(0, str(LIB_DIR))

GITHUB_REPO = "git+https://github.com/NativeSensors/EyeGestures.git"
REQUIRED_PACKAGES = {
    "eyeGestures": "eyeGestures",
    "cv2": "opencv-contrib-python",
    "mediapipe": "mediapipe",
    "numpy": "numpy",
}

def ensure_dependencies():
    """Check for missing packages and auto-install to local lib/ folder."""
    missing = []
    for module_name, pip_name in REQUIRED_PACKAGES.items():
        try:
            __import__(module_name)
        except ImportError:
            missing.append(pip_name)

    if not missing:
        return True

    print(f"[Bridge] Missing packages: {missing}")
    print(f"[Bridge] Installing to {LIB_DIR}...")

    try:
        # Install eyeGestures from GitHub (pulls all deps)
        subprocess.check_call([
            sys.executable, "-m", "pip", "install",
            "--target", str(LIB_DIR),
            GITHUB_REPO
        ])

        # Ensure lib is on path after install
        if str(LIB_DIR) not in sys.path:
            sys.path.insert(0, str(LIB_DIR))

        print("[Bridge] Dependencies installed successfully.")
        return True
    except subprocess.CalledProcessError as e:
        print(f"[Bridge] Auto-install failed: {e}")
        print("[Bridge] Try running: run_eye_tracker.bat to install manually.")
        return False

# ── Validate & import EyeGestures ──
ensure_dependencies()

import cv2

try:
    from eyeGestures import EyeGestures_v3
    from eyeGestures.utils import VideoCapture
    EYEGESTURES_AVAILABLE = True
    print("[Bridge] Successfully imported EyeGestures_v3")
except ImportError as e:
    print(f"[WARNING] EyeGestures import failed: {e}")
    print("[WARNING] Running in mock mode (circular cursor movement).")
    EYEGESTURES_AVAILABLE = False


class TrackerState(Enum):
    """State machine for the eye tracker."""
    INITIALIZING = "initializing"
    RUNNING = "running"
    CALIBRATING = "calibrating"
    PAUSED = "paused"
    ERROR = "error"
    STOPPED = "stopped"


class EyeGesturesBridge:
    """
    Main bridge class that handles eye tracking and Unity communication.
    Supports calibration, headless mode, and bidirectional UDP.
    """
    
    # Configuration
    UDP_IP = "127.0.0.1"
    UDP_SEND_PORT = 5005      # Send gaze data to Unity
    UDP_RECEIVE_PORT = 5006   # Receive commands from Unity
    BUFFER_SIZE = 4096
    HEARTBEAT_INTERVAL = 1.0  # seconds
    
    # Calibration data file path
    CALIBRATION_FILE = Path(__file__).parent / "eye_calibration.json"
    
    def __init__(self, headless: bool = False):
        self.headless = headless
        self.state = TrackerState.INITIALIZING
        self.running = False
        self.calibration_points = []
        self.current_calibration_index = 0
        
        # Screen dimensions
        self._get_screen_dimensions()
        
        # UDP sockets
        self.send_socket = None
        self.receive_socket = None
        self.command_thread = None
        
        # EyeGestures
        self.gestures = None
        self.cap = None
        
        # Heartbeat tracking
        self.last_heartbeat = 0
        self.last_data_time = 0
        
        # Thread lock for state
        self.state_lock = threading.Lock()
        
    def _get_screen_dimensions(self):
        """Get screen dimensions using Windows API."""
        try:
            import ctypes
            user32 = ctypes.windll.user32
            self.screen_width = user32.GetSystemMetrics(0)
            self.screen_height = user32.GetSystemMetrics(1)
        except Exception:
            # Fallback to common resolution
            self.screen_width = 1920
            self.screen_height = 1080
        print(f"[Bridge] Screen: {self.screen_width}x{self.screen_height}")
    
    def initialize(self) -> bool:
        """Initialize all components. Returns True on success."""
        print("[Bridge] Initializing...")
        
        # Initialize UDP sockets
        try:
            self.send_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            
            self.receive_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            self.receive_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.receive_socket.bind((self.UDP_IP, self.UDP_RECEIVE_PORT))
            self.receive_socket.settimeout(0.1)  # Non-blocking with short timeout
            
            print(f"[Bridge] UDP send port: {self.UDP_SEND_PORT}")
            print(f"[Bridge] UDP receive port: {self.UDP_RECEIVE_PORT}")
        except Exception as e:
            print(f"[Bridge] Socket error: {e}")
            self.state = TrackerState.ERROR
            return False
        
        # Initialize EyeGestures
        if EYEGESTURES_AVAILABLE:
            try:
                print("[Bridge] Initializing EyeGestures_v3...")
                self.gestures = EyeGestures_v3(calibration_radius=1000)
                self.gestures.setFixation(0.8)
                
                print("[Bridge] Initializing camera...")
                self.cap = VideoCapture(0)
                
                # Try to load existing calibration
                self._load_calibration()
                
            except Exception as e:
                print(f"[Bridge] EyeGestures init error: {e}")
                self.state = TrackerState.ERROR
                return False
        else:
            print("[Bridge] Running in MOCK mode (no eye tracking)")
        
        # Start command listener thread
        self.running = True
        self.command_thread = threading.Thread(target=self._command_listener, daemon=True)
        self.command_thread.start()
        
        self.state = TrackerState.RUNNING
        print("[Bridge] Initialization complete")
        return True
    
    def _command_listener(self):
        """Background thread to listen for commands from Unity."""
        print("[Bridge] Command listener started")
        
        while self.running:
            try:
                data, addr = self.receive_socket.recvfrom(self.BUFFER_SIZE)
                message = json.loads(data.decode('utf-8'))
                self._handle_command(message)
            except socket.timeout:
                continue
            except json.JSONDecodeError as e:
                print(f"[Bridge] Invalid JSON: {e}")
            except Exception as e:
                if self.running:
                    print(f"[Bridge] Command error: {e}")
    
    def _handle_command(self, message: dict):
        """Process a command from Unity."""
        command = message.get("command", "")
        data = message.get("data", {})
        
        print(f"[Bridge] Received command: {command}")
        
        with self.state_lock:
            if command == "CHECK_CALIBRATION":
                self._send_status()
                
            elif command == "CALIBRATE_START":
                self.state = TrackerState.CALIBRATING
                self.calibration_points = []
                self.current_calibration_index = 0
                print("[Bridge] Calibration started")
                
            elif command == "CALIBRATE_POINT":
                if self.state == TrackerState.CALIBRATING:
                    point = {
                        "index": data.get("index", len(self.calibration_points)),
                        "x": data.get("x", 0),
                        "y": data.get("y", 0),
                        "timestamp": time.time()
                    }
                    self.calibration_points.append(point)
                    print(f"[Bridge] Calibration point {point['index']}: ({point['x']:.2f}, {point['y']:.2f})")
                    
                    # If using real EyeGestures, calibrate at this point
                    if self.gestures and self.cap:
                        self._calibrate_at_point(point)
                
            elif command == "CALIBRATE_END":
                if self.state == TrackerState.CALIBRATING:
                    self._save_calibration()
                    self.state = TrackerState.RUNNING
                    print("[Bridge] Calibration completed and saved")
                    self._send_status()
                    
            elif command == "CALIBRATE_CANCEL":
                self.state = TrackerState.RUNNING
                self.calibration_points = []
                print("[Bridge] Calibration cancelled")
                
            elif command == "QUIT":
                print("[Bridge] Quit command received")
                self.running = False
    
    def _calibrate_at_point(self, point: dict):
        """Perform calibration at the given point."""
        if not self.gestures or not self.cap:
            return
            
        try:
            # Read frame and run calibration step
            ret, frame = self.cap.read()
            if ret:
                frame = cv2.flip(frame, 1)
                
                # Convert normalized coords to screen coords
                screen_x = int(point["x"] * self.screen_width)
                screen_y = int(point["y"] * self.screen_height)
                
                # Run calibration step
                event, cevent = self.gestures.step(
                    frame,
                    calibration=True,
                    width=self.screen_width,
                    height=self.screen_height,
                    context="unity"
                )
        except Exception as e:
            print(f"[Bridge] Calibration point error: {e}")
    
    def _save_calibration(self):
        """Save calibration data to JSON file."""
        try:
            data = {
                "version": 1,
                "timestamp": time.time(),
                "screen_width": self.screen_width,
                "screen_height": self.screen_height,
                "points": self.calibration_points
            }
            
            with open(self.CALIBRATION_FILE, 'w') as f:
                json.dump(data, f, indent=2)
            
            print(f"[Bridge] Calibration saved to {self.CALIBRATION_FILE}")
        except Exception as e:
            print(f"[Bridge] Failed to save calibration: {e}")
    
    def _load_calibration(self) -> bool:
        """Load calibration data from file. Returns True if valid data exists."""
        if not self.CALIBRATION_FILE.exists():
            print("[Bridge] No calibration file found")
            return False
            
        try:
            with open(self.CALIBRATION_FILE, 'r') as f:
                data = json.load(f)
            
            # Check if calibration is recent (within 24 hours)
            age = time.time() - data.get("timestamp", 0)
            if age > 86400:  # 24 hours
                print("[Bridge] Calibration data expired")
                return False
            
            # Check screen dimensions match
            if (data.get("screen_width") != self.screen_width or 
                data.get("screen_height") != self.screen_height):
                print("[Bridge] Screen resolution changed, recalibration needed")
                return False
            
            self.calibration_points = data.get("points", [])
            print(f"[Bridge] Loaded calibration ({len(self.calibration_points)} points)")
            return True
            
        except Exception as e:
            print(f"[Bridge] Failed to load calibration: {e}")
            return False
    
    def _send_status(self):
        """Send current status to Unity."""
        is_calibrated = len(self.calibration_points) > 0
        status = {
            "type": "status",
            "state": self.state.value,
            "calibrated": is_calibrated,
            "point_count": len(self.calibration_points)
        }
        self._send_data(status)
    
    def _send_data(self, data: dict):
        """Send data to Unity via UDP."""
        try:
            message = json.dumps(data).encode('utf-8')
            self.send_socket.sendto(message, (self.UDP_IP, self.UDP_SEND_PORT))
        except Exception as e:
            print(f"[Bridge] Send error: {e}")
    
    def _send_heartbeat(self):
        """Send heartbeat to Unity if no data sent recently."""
        now = time.time()
        if now - self.last_data_time > self.HEARTBEAT_INTERVAL:
            self._send_data({
                "type": "heartbeat",
                "timestamp": now,
                "state": self.state.value
            })
            self.last_heartbeat = now
    
    def run(self):
        """Main tracking loop."""
        print("[Bridge] Starting main loop...")
        print("[Bridge] Press 'q' to quit" if not self.headless else "[Bridge] Running headless")
        
        frame_count = 0
        
        while self.running:
            # Check state
            with self.state_lock:
                current_state = self.state
            
            if current_state == TrackerState.PAUSED:
                time.sleep(0.1)
                continue
            
            # Process frame
            if EYEGESTURES_AVAILABLE and self.cap:
                ret, frame = self.cap.read()
                if not ret:
                    print("[Bridge] Failed to read frame")
                    time.sleep(0.1)
                    continue
                
                # Mirror flip
                frame = cv2.flip(frame, 1)
                
                # Run EyeGestures (only track in RUNNING state)
                is_calibrating = (current_state == TrackerState.CALIBRATING)
                
                event, cevent = self.gestures.step(
                    frame,
                    calibration=is_calibrating,
                    width=self.screen_width,
                    height=self.screen_height,
                    context="unity"
                )
                
                # Send gaze data (only in RUNNING state)
                if event is not None and current_state == TrackerState.RUNNING:
                    gaze_x, gaze_y = event.point
                    
                    # Normalize coordinates (0.0 - 1.0)
                    norm_x = max(0.0, min(1.0, gaze_x / self.screen_width))
                    norm_y = max(0.0, min(1.0, gaze_y / self.screen_height))
                    
                    data = {
                        "type": "gaze",
                        "gaze_x": float(gaze_x),
                        "gaze_y": float(gaze_y),
                        "norm_x": float(norm_x),
                        "norm_y": float(norm_y),
                        "blink": bool(event.blink),
                        "fixation": float(event.fixation),
                        "saccade": bool(event.saccades),
                        "frame": frame_count,
                        "timestamp": time.time()
                    }
                    
                    self._send_data(data)
                    self.last_data_time = time.time()
                
                # Display preview (unless headless)
                if not self.headless:
                    if event is not None:
                        cv2.circle(frame, (int(event.point[0]), int(event.point[1])), 20, (0, 255, 0), 2)
                        
                        # Show state indicator
                        state_color = (0, 255, 0) if current_state == TrackerState.RUNNING else (0, 255, 255)
                        cv2.putText(frame, current_state.value.upper(), (10, 30),
                                   cv2.FONT_HERSHEY_SIMPLEX, 1, state_color, 2)
                    
                    cv2.imshow("EyeGestures Preview", frame)
                    
                    if cv2.waitKey(1) & 0xFF == ord('q'):
                        break
            else:
                # Mock mode: send simulated data
                if current_state == TrackerState.RUNNING:
                    # Simple circular motion for testing
                    t = time.time()
                    mock_x = 0.5 + 0.3 * math.cos(t)
                    mock_y = 0.5 + 0.3 * math.sin(t)
                    
                    data = {
                        "type": "gaze",
                        "gaze_x": mock_x * self.screen_width,
                        "gaze_y": mock_y * self.screen_height,
                        "norm_x": mock_x,
                        "norm_y": mock_y,
                        "blink": False,
                        "fixation": 0.8,
                        "saccade": False,
                        "frame": frame_count,
                        "timestamp": time.time()
                    }
                    self._send_data(data)
                    self.last_data_time = time.time()
                
                time.sleep(1/60)  # ~60 FPS
            
            # Heartbeat
            self._send_heartbeat()
            frame_count += 1
        
        self.cleanup()
    
    def cleanup(self):
        """Clean up resources."""
        print("[Bridge] Cleaning up...")
        self.running = False
        self.state = TrackerState.STOPPED
        
        if self.cap:
            self.cap.close()
        
        if not self.headless:
            cv2.destroyAllWindows()
        
        if self.send_socket:
            self.send_socket.close()
        
        if self.receive_socket:
            self.receive_socket.close()
        
        print("[Bridge] Stopped")


def main():
    parser = argparse.ArgumentParser(description="EyeGestures to Unity Bridge")
    parser.add_argument("--headless", action="store_true", 
                       help="Run without preview window")
    parser.add_argument("--calibrate", action="store_true",
                       help="Start in calibration mode")
    args = parser.parse_args()
    
    # Import math for mock mode
    global math
    import math
    
    bridge = EyeGesturesBridge(headless=args.headless)
    
    if bridge.initialize():
        if args.calibrate:
            bridge.state = TrackerState.CALIBRATING
        bridge.run()
    else:
        print("[Bridge] Initialization failed")
        return 1
    
    return 0


if __name__ == "__main__":
    exit(main())
