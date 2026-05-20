using System;
using UnityEngine;

public class VirtualInputState
{
    private static VirtualInputState _instance;
    private readonly object _syncRoot = new();
    private Vector2 _gazePosition;
    private bool _hasGazePosition;
    private float _emgValue;
    private bool _isEmgActive;
    private DateTime _lastEmgChangeUtc = DateTime.MinValue;

    public static VirtualInputState Instance
    {
        get
        {
            if (_instance == null)
                _instance = new VirtualInputState();

            return _instance;
        }
    }

    private VirtualInputState() { }

    public Vector2 GazePosition
    {
        get
        {
            lock (_syncRoot)
                return _gazePosition;
        }
    }

    public bool HasGazePosition
    {
        get
        {
            lock (_syncRoot)
                return _hasGazePosition;
        }
    }

    public float EmgValue
    {
        get
        {
            lock (_syncRoot)
                return _emgValue;
        }
    }

    public bool IsEmgActive
    {
        get
        {
            lock (_syncRoot)
                return _isEmgActive;
        }
    }

    public DateTime LastEmgChangeUtc
    {
        get
        {
            lock (_syncRoot)
                return _lastEmgChangeUtc;
        }
    }

    public void SetGazePosition(Vector2 gazePosition)
    {
        lock (_syncRoot)
        {
            _gazePosition = gazePosition;
            _hasGazePosition = true;
        }
    }

    public void ClearGazePosition()
    {
        lock (_syncRoot)
            _hasGazePosition = false;
    }

    public void SetEmgPrediction(bool isActive, float emgValue = 0f)
    {
        lock (_syncRoot)
        {
            if (_isEmgActive != isActive)
                _lastEmgChangeUtc = DateTime.UtcNow;

            _isEmgActive = isActive;
            _emgValue = emgValue;
        }
    }
}
