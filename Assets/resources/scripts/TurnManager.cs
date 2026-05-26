using System;

public class TurnManager
{
    public event Action OnTurnStarted;
    public event Action OnTurnEnded;

    private bool _isActive;
    private int _currentTurn;
    private float _turnDuration;
    private float _elapsed;

    public bool GetIsActive() => _isActive;
    public int GetCurrentTurn() => _currentTurn;
    public float GetTurnDuration() => _turnDuration;

    public TurnManager(float turnDuration)
    {
        _turnDuration = turnDuration;
    }

    public void StartTurn()
    {
        _isActive = true;
        _currentTurn++;
        _elapsed = 0f;
        OnTurnStarted?.Invoke();
    }

    public void EndTurn()
    {
        _isActive = false;
        _elapsed = 0f;
        OnTurnEnded?.Invoke();
    }

    public void Reset()
    {
        _isActive = false;
        _currentTurn = 0;
        _elapsed = 0f;
    }

    // Call this from GameManager.Update() with Time.deltaTime
    public void Tick(float deltaTime)
    {
        if (!_isActive) return;

        _elapsed += deltaTime;
        if (_elapsed >= _turnDuration)
            EndTurn();
    }
}
