using System;
using UnityEngine;

[Serializable]
public class CardEffect
{
    [SerializeField] private Effect _effect;
    [SerializeField] private EffectTarget _target;
    private float multiplier = 1f;
    private int adder = 0;

    public void Reset()
    {
        multiplier = 1f;
        adder = 0;
    }

    public void Add(int i)
    {
        adder += i;
    }
    
    public void Multiply(float i)
    {
        multiplier *= i;
    }
    public CardEffect(Effect effect, EffectTarget target)
    {
        _effect = effect;
        _target = target;
    }

    public Effect GetEffect() => _effect;
    public BattleManager GetTarget(BattleManager user)
    {
        return _target == EffectTarget.User ? user : GameManager.Instance.GetOpponent(user);
    }

    public int GetMagnitude()
    {
        return (int)Math.Floor(_effect.GetMagnitude() * multiplier) + adder;
    }
}
