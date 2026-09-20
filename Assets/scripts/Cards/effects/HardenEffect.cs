using UnityEngine;

public class HardenEffect : Effect
{
    private const int Priority = 2;

    public HardenEffect(int magnitude) : base(EffectType.Harden, magnitude, Priority) { }

    // Harden은 자신의 방어 획득을 강화하는 버프이므로 양수 magnitude는 User, 음수는 Opponent(방어 획득 약화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

    // 단순 스택형 상태 효과라 큐에 머무는 동안 유지되는 형태로도 부여할 수 있다.
    public override EffectCategory SupportedCategories => EffectCategory.Instant | EffectCategory.Continuous;

    // Instant 경로: 카드가 큐에서 다 됐을 때(CardInstance.Play → CharacterManager.ApplyEffect)
    // 호출된다. Continuous 경로(OnEnterQueue, 여기선 override 안 함)는 이 메서드를 타지 않고 별도
    // continuous 풀(IsInfinite==true)에만 합산하므로, instant 몫은 여기 base 그대로에 맡겨도 항상
    // continuous 몫과 분리된 채 유지된다. Continuous와 대칭이 보이도록 명시적으로 override해둔다.
    public override void OnApply(CharacterManager subject) => base.OnApply(subject);

    // Harden: every defense gain is increased by a flat amount equal to magnitude
    public override void OnApplyingOther(CharacterManager b, CardEffect effect,bool a)
    {
        if (effect.GetEffect().GetEffectType() == EffectType.Defend)
        {
            effect.Add(_magnitude);
        }
    }
}
