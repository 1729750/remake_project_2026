using System.Collections.Generic;
using UnityEngine;

public class CardInstance
{
    private CardDefinition _definition;
    private bool _isPlayed = false;
    private int _cooldownLeft; 
    private int cooldown;
    private int cost;
    private CardType cardType;
    private List<CardEffect> _effects;

    private CharacterManager _owner;
    // effect will be added later

    public CardInstance(CardDefinition definition, CharacterManager owner)
    {
        _definition = definition;
        _owner = owner;
        RefreshInstance();
        _isPlayed = false;
        _cooldownLeft = 0;
    }

    private void RefreshInstance()
    {
        cooldown =  _definition.GetCooldown();
        cost  = _definition.GetCost();
        _effects = new List<CardEffect>();
        foreach (CardEffect cardEffect in _definition.GetEffects())
        {
            _effects.Add(cardEffect);
        }
    }
    public CardDefinition GetDefinition() => _definition;
    public int GetCost() => cost;
    public int GetCooldownLeft() => _cooldownLeft;
    public bool IsReady() => _cooldownLeft <= 0;

    public void Use()
    {
        _cooldownLeft = _definition.GetCooldown();
        _isPlayed = true;
    }

    public void TickCooldown()
    {
        if (_cooldownLeft > 0)
            _cooldownLeft--;
    }

    public void Play(CharacterManager characterManager)
    {
        Debug.Log($"[{characterManager.gameObject.name}] Activated card: {_definition.name}");
        foreach (CardEffect cardEffect in _effects)
        {
           cardEffect.Reset();
            Effect[] ownerEffects = characterManager.GetEffects();
            for (int i = ownerEffects.Length - 1; i >= 0; i--)
                ownerEffects[i].OnApplying(characterManager, cardEffect, true);
            CharacterManager resolved = cardEffect.GetTarget(characterManager);
            resolved.ApplyEffect(cardEffect);
        }
        characterManager.ReturnToDeck(this);
    }
}
