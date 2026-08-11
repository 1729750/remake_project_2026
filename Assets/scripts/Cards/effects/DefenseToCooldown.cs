using System;

[Serializable]
public class DefenseToCooldown : Effect
{
    public DefenseToCooldown(int magnitude) : base(EffectType.DefenseToCooldown, magnitude) { }

    public override void OnExpired(CharacterManager subject) { }
    public override void OnTurnStarted(CharacterManager subject) { }
    public override void OnTurnEnded(CharacterManager subject) { }

    // subject의 방어도만큼 자신(self)의 쿨다운을 깎는다. actualUse일 때만 subject의 방어도를
    // 실제로 그만큼 차감한다(ChangeCooldownLeft가 이미 0 밑으로는 못 내려가게 clamp한다).
    public override void OnUse(CharacterManager subject, CardInstance self, bool actualUse)
    {
        if (subject == null || self == null) return;

        int defense = subject.GetDefense();
        self.ChangeCooldownLeft(-defense);

        if (actualUse)
            subject.AddDefense(-defense);
    }

    public override void OnUsingOther(CharacterManager subject, CardInstance cardInstance, bool actualUse) { }
    public override void OnTick(CharacterManager subject) { }
    public override void OnApplyingOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
    public override void OnAppliedOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
}
