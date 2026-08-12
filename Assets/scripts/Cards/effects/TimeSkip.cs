using System;

[Serializable]
public class TimeSkip : Effect
{
    public TimeSkip(int magnitude) : base(EffectType.TimeSkip, magnitude) { }

    // TimeSkip은 subject 자신의 큐 쿨다운을 깎아주는 자기 강화 유틸리티이므로 양수 magnitude는
    // User, 음수는 Opponent(효과 약화)를 향한다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

    // subject의 큐에서 cooldownLeft가 가장 높은 카드를 찾아 magnitude만큼 쿨다운을 깎는다.
    public override void OnApply(CharacterManager subject)
    {
        CardInstance target = null;
        foreach (CardInstance queued in subject.GetQueue())
        {
            if (queued == null) continue;
            if (target == null || queued.GetCooldownLeft() > target.GetCooldownLeft())
                target = queued;
        }

        target?.ChangeCooldownLeft(-_magnitude);
    }
}
