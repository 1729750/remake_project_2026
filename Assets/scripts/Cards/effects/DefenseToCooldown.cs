using System;

[Serializable]
public class DefenseToCooldown : Effect
{
    public DefenseToCooldown(int magnitude) : base(EffectType.DefenseToCooldown, magnitude) { }

    // DefenseToCooldown은 subject 자신의 방어도를 자기 카드 쿨다운으로 바꿔주는 자기 강화
    // 유틸리티이므로 User를 향한다. magnitude를 쓰지 않는 타입(DoesntUseMagnitude)이라 부호로
    // 방향을 가르는 의미는 없지만, 다른 자기 강화 유틸리티 타입들과 동일하게 Positive로 맞춰둔다.
    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

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
