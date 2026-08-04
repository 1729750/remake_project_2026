using System.Collections.Generic;
using UnityEngine;

public class CardInstance
{
    private CardDefinition _definition;
    private bool _isPlayed;
    private int _cooldownLeft; 
    private int _cooldown;
    private int _cost;
    private CardType cardType;
    private List<CardEffect> _effects;

    private CharacterManager _owner;
    private CardVisual _visual;
    // effect will be added later

    public CardInstance(CardDefinition definition, CharacterManager owner)
    {
        _definition = definition;
        _owner = owner;
        RefreshInstance();
        _isPlayed = false;
        _cooldownLeft = _definition.GetCooldown();
    }

    // CardVisual은 자기 자신의 데이터를 갖지 않는다 — 이 카드가 화면에 어떻게 보일지는
    // 전부 CardInstance가 값을 넣어주는 방식으로 밀어넣는다(pull이 아니라 push).
    public CardVisual GetVisual() => _visual;

    public void SetVisual(CardVisual visual)
    {
        _visual = visual;
        if (_visual == null) return;

        _visual.SetCardDefinition(_definition);
        RefreshCooldownDisplay();
    }

    public void SetSelected(bool selected)
    {
        if (_visual != null) _visual.SetSelected(selected);
    }

    public void SetFace(bool front)
    {
        if (_visual != null) _visual.SetFace(front);
    }

    public void SetLayer(string sortingLayerName)
    {
        if (_visual != null) _visual.SetLayer(sortingLayerName);
    }

    public void SetSize(Vector2 targetSize)
    {
        if (_visual != null) _visual.SetSize(targetSize);
    }

    public void MoveTo(Vector3 targetPosition, float duration = 0.3f)
    {
        if (_visual != null) _visual.MoveTo(targetPosition, duration);
    }

    public Vector2 GetBackgroundSize() => _visual != null ? _visual.GetBackgroundSize() : Vector2.zero;

    private void RefreshCooldownDisplay()
    {
        if (_visual == null) return;
        _visual.SetCooldownText(_cooldownLeft.ToString());
    }

    private void RefreshInstance()
    {
        _cooldown =  _definition.GetCooldown();
        _cooldownLeft = _definition.GetCooldown();
        _cost  = _definition.GetCost();
        _effects = new List<CardEffect>();
        foreach (CardEffect cardEffect in _definition.GetEffects())
        {
            _effects.Add(cardEffect);
        }
    }
    public CardDefinition GetDefinition() => _definition;
    public List<CardEffect> GetEffects() => _effects;
    public int GetCost() => _cost;
    public int GetCooldown() => _cooldown;
    public bool GetIsPlayed() => _isPlayed;
    public int GetCooldownLeft() => _cooldownLeft;
    public bool IsReady() => _cooldownLeft <= 0;

    public void Use()
    {
        _cooldownLeft = _definition.GetCooldown();
        _isPlayed = true;
        RefreshCooldownDisplay();
    }

    public void TickCooldown(int tick=1)
    {
            _cooldownLeft-=tick;
            RefreshCooldownDisplay();
    }

    public void Play(CharacterManager characterManager)
    {
        Debug.Log($"[{characterManager.gameObject.name}] Activated card: {_definition.name}");
        foreach (CardEffect cardEffect in _effects)
        {
           cardEffect.Reset();
            Effect[] ownerEffects = characterManager.GetEffectPrioritize();
            for (int i = ownerEffects.Length - 1; i >= 0; i--)
                ownerEffects[i].OnApplyingOther(characterManager, cardEffect, true);
            CharacterManager resolved = cardEffect.GetTarget(characterManager);
            resolved.ApplyEffect(cardEffect);
        }
    }
}
