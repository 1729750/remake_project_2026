using System;

// 하이리스크 방어: 큐에 올라가는 순간엔 방어도가 0이다(선투자 없음). 큐에 머무는 동안 맞을 때마다
// (OnDamaged) magnitude만큼의 "보너스 방어도"를 그때그때 얻는다 — 상대의 초대형 공격이 먼저 오면
// 그대로 다 맞고, 잦은 잽을 버텨내면 뒤이어 오는 큰 공격을 막아낼 수도 있는 승부수형 카드.
// 이 효과가 완전히 사라질 때(OnExpired — 큐의 마지막 사본이 빠져나가 continuous 풀이 바닥났을 때)
// 그동안 쌓아준 보너스 방어도를 전부 회수한다(영구 적립 방지).
[Serializable]
public class LastStandEffect : Effect
{
    private int _totalGranted;

    public LastStandEffect(int magnitude) : base(EffectType.LastStand, magnitude) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Positive;

    public override EffectCategory SupportedCategories => EffectCategory.Continuous;

    public override void OnDamaged(CharacterManager subject, int damageTaken)
    {
        int grant = GetMagnitude();
        subject.AddBonusDefense(grant);
        _totalGranted += grant;
    }

    public override void OnExpired(CharacterManager subject)
    {
        if (_totalGranted <= 0) return;
        subject.AddBonusDefense(-_totalGranted);
        _totalGranted = 0;
    }
}
