using System;
using System.Linq;

// 공격 계열(AttackEffect 자식) — 일반 Attack과 동일하게 즉발 피해를 입히고 방어를 소모하지만,
// 실제로 적중했을 때(OnDamaging) 대상에게 Poison을 정확히 1씩만 부여한다. 독은 "맞혀야만" 쌓이게
// 만들어서 스택이 쉽게 쌓이지 않도록 한다 — 이 카드 자신의 magnitude(공격력)와는 무관하게 항상 1.
[Serializable]
public class VenomousAttackEffect : AttackEffect
{
    public VenomousAttackEffect(int magnitude) : base(magnitude) { }

    protected override void OnDamaging(CharacterManager defender, int damageDealt)
    {
        Effect existing = defender.GetEffects().FirstOrDefault(e => e is PoisonEffect);
        if (existing != null)
            existing.AddMagnitude(1);
        else
            defender.AddEffect(new PoisonEffect(1));
    }
}
