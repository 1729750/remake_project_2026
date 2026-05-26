using System;
using System.Collections.Generic;
using UnityEngine;

public class BattleManager
{
    private int _maxHealth;
    private int _health;
    private int _defense;
    private List<Effect> _effects;
    private float _damageMultiplier=1f;
    private int _damageAdder = 0;
    private float _defenseMultiplier=1f;
    private int _defenseAdder = 0;
    [SerializeField] private CardDefinition[] _startDeck;
    private List<CardInstance> _deck;
    
    public BattleManager(int maxHealth)
    {
        _maxHealth = maxHealth;
        _effects = new List<Effect>();
        Init();
    }

    public int getmaxHealth() => _maxHealth;
    public int GetHealth() => _health;
    public int GetDefense() => _defense;
    public Effect[] GetEffects() => _effects.ToArray();

    public void Init()
    { 
        Clear();
        if (_startDeck != null)
            foreach (var def in _startDeck)
                _deck.Add(new CardInstance(def, this));
        for (int i = _deck.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
        }
    }

    public void Clear()
    {
        _effects.Clear();
        _deck = new List<CardInstance>();
        _damageAdder = 0;
        _damageMultiplier = 1;
        _defenseAdder = 0;
        _defenseMultiplier = 1;
        _health = _maxHealth;
    }

    public void Attacked(int damage)
    {
        TakeDamage(Mathf.RoundToInt(damage * _damageMultiplier + _damageAdder));
    }

    public void MultiplyDamageMultiplier(float multiplier)
    {
        _damageMultiplier *= multiplier;
    }

    public void AddDamageAdder(int adder)
    {
        _damageAdder += adder;
    }
    
    public void TakeDamage(int amount)
    {
        if (amount <= 0)
            return;
        _defense -= amount;
        if(_defense<0)
        {
            _health += _defense;
            _defense = 0;
        }
    }

    public void Heal(int amount)
    {
        _health += amount;
    }

    public void AddDefense(int amount)
    {
        _defense += Mathf.RoundToInt(amount * _defenseMultiplier + _defenseAdder);
    }

    public void MultiplyDefenseMultiplier(float multiplier)
    {
        _defenseMultiplier *= multiplier;
    }

    public void AddDefenseAdder(int adder)
    {
        _defenseAdder += adder;
    }

    public CardInstance DrawCard()
    {
        if (_deck.Count == 0) return null;
        var card = _deck[0];
        _deck.RemoveAt(0);
        return card;
    }

    public void ReturnToDeck(CardInstance card)
    {
        _deck.Add(card);
    }

    public void RemoveEffect<T>() where T : Effect
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i] is T)
            {
                _effects[i].OnExpired(this);
                _effects.RemoveAt(i);
                return;
            }
        }
    }

    public void ApplyEffect(CardEffect cardEffect)
    {
        Effect effect = cardEffect.GetEffect();
        switch (effect.GetEffectType())
        {
            case(EffectType.Attack):
                Attacked(effect.GetMagnitude());
                break;
            case(EffectType.Defend):
                AddDefense(effect.GetMagnitude());
                break;
            default:
                for (int i = 0; i < _effects.Count; i++){
                    if(_effects[i]==null)continue;
                    if (_effects[i].GetType() == effect.GetType())
                    {
                        _effects[i].AddMagnitude(effect.GetMagnitude());
                        effect = _effects[i];
                        break;
                    }
                }
                _effects.Add(effect);
                effect.OnApplied(this);
                break;
        }
    }
}
