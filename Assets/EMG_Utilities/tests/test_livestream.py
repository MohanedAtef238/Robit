import importlib.util
import pathlib
import sys
import types

import pytest


def _load_livestream_module(monkeypatch, load_model_impl=None):
    livestream_path = pathlib.Path(__file__).resolve().parents[1] / "livestream.py"

    # Build lightweight fake numpy module used by livestream.py.
    class _FakeVector(list):
        def reshape(self, rows, _cols):
            if rows != 1:
                raise ValueError("This fake reshape only supports one row")
            return _FakeMatrix([list(self)])

    class _FakeMatrix:
        def __init__(self, data):
            self._data = data
            self.shape = (len(data), len(data[0]) if data else 0)

        def __getitem__(self, idx):
            return self._data[idx]

    fake_numpy = types.ModuleType("numpy")
    fake_numpy.array = lambda x: list(x)
    fake_numpy.zeros = lambda n: [0.0] * n

    def _concatenate(parts):
        flattened = []
        for part in parts:
            flattened.extend(list(part))
        return _FakeVector(flattened)

    fake_numpy.concatenate = _concatenate

    # Build lightweight fake tensorflow package tree so import does not require real TF.
    tf_module = types.ModuleType("tensorflow")
    keras_module = types.ModuleType("tensorflow.keras")
    keras_models_module = types.ModuleType("tensorflow.keras.models")
    keras_layers_module = types.ModuleType("tensorflow.keras.layers")

    if load_model_impl is None:
        load_model_impl = lambda path: object()

    keras_models_module.load_model = load_model_impl
    keras_models_module.Sequential = type("Sequential", (), {})
    keras_layers_module.Dense = type("Dense", (), {})
    keras_layers_module.Conv1D = type("Conv1D", (), {})
    keras_layers_module.MaxPooling1D = type("MaxPooling1D", (), {})
    keras_layers_module.Flatten = type("Flatten", (), {})
    keras_layers_module.LSTM = type("LSTM", (), {})

    tf_module.keras = types.SimpleNamespace(models=keras_models_module, layers=keras_layers_module)
    keras_module.models = keras_models_module
    keras_module.layers = keras_layers_module

    # Stub local imports used by livestream.py.
    feature_engineering_module = types.ModuleType("feature_engineering")
    feature_engineering_module.calculate_emg_features = lambda segment: {
        "WL": 0.0,
        "AAC": 0.0,
        "DASDV": 0.0,
        "AR_Coeffs": [0.0, 0.0, 0.0, 0.0],
        "Cepstral_Coeffs": [0.0, 0.0, 0.0, 0.0],
    }

    model_module = types.ModuleType("model")
    model_module.predict = lambda model, input_data: False

    emg_module = types.ModuleType("emg")
    emg_module.EMGReader = type("EMGReader", (), {})

    monkeypatch.setitem(sys.modules, "numpy", fake_numpy)
    monkeypatch.setitem(sys.modules, "tensorflow", tf_module)
    monkeypatch.setitem(sys.modules, "tensorflow.keras", keras_module)
    monkeypatch.setitem(sys.modules, "tensorflow.keras.models", keras_models_module)
    monkeypatch.setitem(sys.modules, "tensorflow.keras.layers", keras_layers_module)
    monkeypatch.setitem(sys.modules, "feature_engineering", feature_engineering_module)
    monkeypatch.setitem(sys.modules, "model", model_module)
    monkeypatch.setitem(sys.modules, "emg", emg_module)

    spec = importlib.util.spec_from_file_location("livestream_module_under_test", livestream_path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_process_livestream_returns_early_when_model_load_fails(monkeypatch, capsys):
    def _raise_load_error(_):
        raise RuntimeError("model load failed")

    module = _load_livestream_module(monkeypatch, load_model_impl=_raise_load_error)
    result = module.process_livestream([1.0, 2.0, 3.0])

    assert result is None
    out = capsys.readouterr().out
    assert "Error loading model" in out


def test_process_livestream_does_not_predict_before_buffer_reaches_50(monkeypatch):
    module = _load_livestream_module(monkeypatch, load_model_impl=lambda _: object())

    calls = {"features": 0, "predict": 0}

    def fake_features(_segment):
        calls["features"] += 1
        return {
            "WL": 1.0,
            "AAC": 2.0,
            "DASDV": 3.0,
            "AR_Coeffs": [4.0, 5.0, 6.0, 7.0],
            "Cepstral_Coeffs": [8.0, 9.0, 10.0, 11.0],
        }

    def fake_predict(_model, _input_data):
        calls["predict"] += 1
        return True

    module.calculate_emg_features = fake_features
    module.predict = fake_predict

    module.process_livestream(range(49))

    assert calls["features"] == 0
    assert calls["predict"] == 0


def test_process_livestream_builds_expected_feature_vector_and_shape(monkeypatch):
    module = _load_livestream_module(monkeypatch, load_model_impl=lambda _: "fake_model")

    captured = {}

    def fake_features(segment):
        captured["segment_len"] = len(segment)
        return {
            "WL": 1.0,
            "AAC": 2.0,
            "DASDV": 3.0,
            "AR_Coeffs": [4.0, 5.0, 6.0, 7.0],
            "Cepstral_Coeffs": [8.0, 9.0, 10.0, 11.0],
        }

    def fake_predict(model, input_data):
        captured["model"] = model
        captured["shape"] = input_data.shape
        captured["vector"] = input_data[0].copy()
        return True

    module.calculate_emg_features = fake_features
    module.predict = fake_predict

    module.process_livestream(range(50))

    assert captured["segment_len"] == 50
    assert captured["model"] == "fake_model"
    assert captured["shape"] == (1, 11)
    assert captured["vector"] == [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0]


def test_process_livestream_predicts_for_each_new_value_after_buffer_full(monkeypatch):
    module = _load_livestream_module(monkeypatch, load_model_impl=lambda _: object())

    calls = {"predict": 0}

    module.calculate_emg_features = lambda _segment: {
        "WL": 0.0,
        "AAC": 0.0,
        "DASDV": 0.0,
        "AR_Coeffs": [0.0, 0.0, 0.0, 0.0],
        "Cepstral_Coeffs": [0.0, 0.0, 0.0, 0.0],
    }

    def fake_predict(_model, _input_data):
        calls["predict"] += 1
        return False

    module.predict = fake_predict

    # For 52 values and maxlen=50, predict should run on items 50, 51, and 52 => 3 calls.
    module.process_livestream(range(52))
    assert calls["predict"] == 3
