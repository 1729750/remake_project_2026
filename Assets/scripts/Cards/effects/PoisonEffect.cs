using System;

// 영구 DOT. 화상과 달리 카드의 큐 체류와 무관하게 사라지지 않고(회복 안 되는 독), 방어를 무시한다
// (TakeDamage를 직접 호출). 디자이너가 카드에 직접 얹을 수 있는 타입이 아니다 — 오직
// VenomousAttackEffect.OnDamaging이 적중 시 1씩만 부여한다(RewardManager.EnhanceableEffectTypes에서도
// 제외되어 강화로도 얻을 수 없다).
[Serializable]
public class PoisonEffect : Effect
{
    public PoisonEffect(int magnitude) : base(EffectType.Poison, magnitude) { }

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    public override void OnTurnEnded(CharacterManager subject)
    {
        subject.TakeDamage(_magnitude);
    }
}
