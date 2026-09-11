using System;
using TMPro;
using UnityEngine;

public class TurnManager
{
    public event Action OnTurnStarted;
    public event Action OnTurnEnded;

    private bool _isActive;
    private int _currentTurn;
    private float _turnDuration;
    private float _elapsed;
    private TurnTimerOverlay _overlay;
    private TextMeshPro _turnText;

    public bool GetIsActive() => _isActive;
    public int GetCurrentTurn() => _currentTurn;
    public float GetTurnDuration() => _turnDuration;
    public float GetElapsedRatio() => _turnDuration > 0 ? Mathf.Clamp01(_elapsed / _turnDuration) : 0f;

    public TurnManager(float turnDuration, TurnTimerOverlay overlay, TextMeshPro turnText)
    {
        _turnDuration = turnDuration;
        _overlay = overlay;
        _turnText = turnText;
        UpdateTurnText();
    }

    private void UpdateTurnText()
    {
        if (_turnText != null)
            _turnText.text = $"{_currentTurn}";
    }

    public void StartTurn()
    {
        _isActive = true;
        _currentTurn++;
        _elapsed = 0f;
        _overlay?.SetFill(0f);
        UpdateTurnText();
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
        // 첫 StartTurn 호출이 0으로 증가시켜, BattleStarting 카운트다운(3-2-1) 직후 턴 표시가
        // 0부터 시작하도록 한다(3-2-1-0-1-2...).
        _currentTurn = -1;
        _elapsed = 0f;
        _overlay?.SetFill(0f);
    }

    // Call this from GameManager.Update() with Time.deltaTime
    public void Tick(float deltaTime)
    {
        if (!_isActive) return;

        _elapsed += deltaTime;
        _overlay?.SetFill(GetElapsedRatio());
        if (_elapsed >= _turnDuration)
            EndTurn();
    }
}
