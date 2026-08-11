using System;

[Serializable]
public class TimeSkip : Effect
{
    public TimeSkip(int magnitude) : base(EffectType.TimeSkip, magnitude) { }

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
