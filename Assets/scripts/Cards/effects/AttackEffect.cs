using System;

[Serializable]
public class AttackEffect : Effect
{
    public AttackEffect(int magnitude) : base(EffectType.Attack, magnitude) { }

    // 공격과 그 자식만 Instant다(시전 시간이 끝나는 순간 한 번 떨어진다). 나머지 효과는 전부 Continuous.
    public override EffectCategory SupportedCategories => EffectCategory.Instant;

    public override EffectTargetPolarity TargetPolarity => EffectTargetPolarity.Negative;

    // 공격 계열이 큐의 Defend 카드로 상쇄되는지. 방어를 무시하는 공격 변종을 만들 때 false로
    // override한다. CharacterManager.ApplyEffect가 OnApply를 부르기 전에 확인한다.
    public virtual bool ConsumesDefense => true;

    // 공격 계열의 공통 뼈대: 데미지를 입히고(DealDamage), 실제로 1 이상 들어갔으면 OnDamaging을
    // 부른다. sealed로 막아서 자식들은 OnApply가 아니라 DealDamage/OnDamaging만 override하게 강제한다
    // — OnDamaging이 항상 "실제로 때렸을 때"에만, 정확히 한 번 불리는 걸 보장하기 위함이다.
    public sealed override void OnApply(CharacterManager subject)
    {
        int dealt = DealDamage(subject);
        if (dealt >= 1) OnDamaging(subject, dealt);
    }

    // 실제 데미지 적용. 기본은 즉발 피해(subject.Attacked가 guard 반감까지 반영한 실피해를 돌려준다).
    // Burning처럼 즉발 피해 대신 DOT를 쌓는 변종은 이 메서드를 override하고, 그 순간 들어간 즉발
    // 피해가 없다면 0을 돌려줘서(OnDamaging을 트리거하지 않도록) 의미를 지킨다.
    protected virtual int DealDamage(CharacterManager subject) => subject.Attacked(_magnitude);

    // 이 공격이 실제로 1 이상의 피해를 입혔을 때 호출된다. AttackEffect의 자식만 override할 수
    // 있다 — 일반 skill 효과(Effect 기반)는 이 훅에 접근할 수 없다(요청사항).
    protected virtual void OnDamaging(CharacterManager defender, int damageDealt) { }
}
