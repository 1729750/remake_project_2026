using UnityEngine;

public class CardEffect_Multi : CardEffect
{
    public CardEffect_Multi(
        Effect_Multi effect,
        EffectTarget target)
        : base(effect, target)
    {
    }

    public new Effect_Multi GetEffect()
    {
        return base.GetEffect() as Effect_Multi;
    }

    public CharacterManager_Multi GetTarget(
        CharacterManager_Multi user)
    {
        if (user == null)
            return null;

        if (GetEffectTarget() == EffectTarget.User)
            return user;

        return BattleManager_Multi.Instance != null
            ? BattleManager_Multi.Instance.GetOpponent(user)
            : null;
    }
}
