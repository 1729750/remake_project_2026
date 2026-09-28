using System;

[Serializable]
public class WeakEffect_Multi : Effect_Multi
{
    private const int Priority = 1;

    public WeakEffect_Multi(int magnitude)
        : base(
            EffectType.Weak,
            magnitude,
            Priority)
    {
    }

    public override EffectTargetPolarity
        TargetPolarity =>
        EffectTargetPolarity.Negative;

    public override void OnApplyingOther(
        CharacterManager_Multi subject,
        CardEffect_Multi effect,
        bool actualUse)
    {
        if (effect.GetEffect()
                .GetEffectType() ==
            EffectType.Attack)
        {
            effect.Multiply(0.8f);
        }

        if (!actualUse)
            return;

        _magnitude--;

        if (_magnitude <= 0)
        {
            subject
                .RemoveEffect<
                    WeakEffect_Multi>();
        }
    }

    public override void OnTurnEnded(
        CharacterManager_Multi subject)
    {
        _magnitude -= 1;

        if (_magnitude <= 0)
        {
            // Single 소스는 여기서 BurningEffect를 제거하고 있다.
            // 동작을 임의로 바꾸지 않기 위해 같은 의미를 보존한다.
            subject
                .RemoveEffect<
                    BurningEffect_Multi>();
        }
    }
}
