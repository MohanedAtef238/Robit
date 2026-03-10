import importlib.util
import pathlib
import sys
import time

import pytest

_EMG_PATH = pathlib.Path(__file__).resolve().parents[1] / "emg.py"
_SPEC = importlib.util.spec_from_file_location("emg_module", _EMG_PATH)
_EMG_MODULE = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(_EMG_MODULE)
EMGReader = _EMG_MODULE.EMGReader


class _FakeSerial:
    def __init__(self, lines):
        self._lines = list(lines)
        self._index = 0
        self.closed = False

    def readline(self):
        # Slow down the loop a bit to avoid tight CPU spinning in tests.
        time.sleep(0.002)
        if self._index < len(self._lines):
            line = self._lines[self._index]
            self._index += 1
            return line
        return b""

    def close(self):
        self.closed = True


class _FakeSerialModule:
    def __init__(self, lines):
        self._lines = lines
        self.instance = None

    def Serial(self, *args, **kwargs):
        self.instance = _FakeSerial(self._lines)
        return self.instance


def _wait_until(predicate, timeout=0.3, poll=0.005):
    start = time.time()
    while time.time() - start < timeout:
        if predicate():
            return True
        time.sleep(poll)
    return False


@pytest.fixture
def fake_serial_module(monkeypatch):
    created = {}

    def install(lines):
        module = _FakeSerialModule(lines)
        created["module"] = module
        monkeypatch.setitem(sys.modules, "serial", module)
        return module

    return install


def test_emg_reader_parses_valid_line(fake_serial_module):
    module = fake_serial_module([b"1.5,2.5\n"])
    reader = EMGReader(port="COM_TEST")
    try:
        assert _wait_until(lambda: reader.envelope == 2.5)
        assert reader.filtered == 1.5
        assert reader.envelope == 2.5
    finally:
        reader.stop()

    assert module.instance.closed is True


def test_emg_reader_recovers_after_malformed_line(fake_serial_module):
    fake_serial_module([b"bad_line\n", b"3,4\n"])
    reader = EMGReader(port="COM_TEST")
    try:
        # The malformed line should be ignored, and then valid data should update state.
        assert _wait_until(lambda: reader.filtered == 3.0 and reader.envelope == 4.0)
    finally:
        reader.stop()


def test_emg_reader_stop_stops_loop_and_closes_serial(fake_serial_module):
    module = fake_serial_module([b"1,1\n"])
    reader = EMGReader(port="COM_TEST")

    reader.stop()
    time.sleep(0.01)

    assert reader.running is False
    assert module.instance.closed is True
