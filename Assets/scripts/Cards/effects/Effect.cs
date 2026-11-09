using System;
using UnityEngine;

[Serializable]
public class Effect
{
   [SerializeField] private EffectType _effectType;
   [SerializeField] protected int _magnitude;
    private int _effectPriority;

    // 카드가 큐에 머무는 동안만 사는 continuous 풀 인스턴스(OnEnterQueue가 만든 것)인지. 큐 카드가
    // 아니라 다른 경로로 직접 붙은 효과(VenomousAttack이 거는 Poison)는 false다.
    public bool IsInfinite { get; set; }

    // continuous 풀(IsInfinite)에 지금 기여 중인 큐 카드 수. 음수 magnitude(상대에게 거는 Strength -N 등)나
    // 부호가 섞인 기여가 한 풀에 합쳐질 수 있어 "magnitude <= 0이면 제거"로는 판단할 수 없다 —
    // 기여한 카드가 전부 큐를 떠났을 때만 풀을 제거한다.
    public int ContinuousSources { get; set; }

    // 공격(AttackEffect와 그 자식)만 Instant — 카드 쿨타임이 다 됐을 때 한 번 발동한다. 그 외 모든
    // 효과(스킬)는 Continuous — 카드가 큐에 들어가는 순간부터 큐를 떠날 때까지만 유지된다.
    // Preserve/Disposable은 효과가 아니라 카드 자체의 속성 표시라 어느 카드에든 붙을 수 있게 둘 다 허용한다.
    public virtual EffectCategory SupportedCategories => IsCardMarker
        ? EffectCategory.Instant | EffectCategory.Continuous
        : EffectCategory.Continuous;

    private bool IsCardMarker => _effectType == EffectType.Preserve || _effectType == EffectType.Disposable;

    public Effect(EffectType effectType, int magnitude, int effectPriority = 0)
    {
        _effectType = effectType;
        _magnitude = magnitude;
        _effectPriority = effectPriority;
    }
    public EffectType GetEffectType() => _effectType;
    public int GetMagnitude() => _magnitude;
    public int GetEffectPriority() => _effectPriority;

    public void AddMagnitude(int magnitude) => _magnitude += magnitude;

    // 이 Effect가 강화 후보로 뽑혔을 때(RewardManager.RollEnhanceOption) magnitude 부호로부터
    // EffectTarget(User/Opponent)을 정하는 데 쓰는 극성. Attack/Defend처럼 별도 서브클래스가 없는
    // 타입은 _effectType으로 구분하고, 서브클래스는 자신의 성격(버프/디버프)에 맞게 override한다.
    public virtual EffectTargetPolarity TargetPolarity
    {
        get
        {
            switch (_effectType)
            {
                case EffectType.Attack: return EffectTargetPolarity.Negative;
                case EffectType.Defend: return EffectTargetPolarity.Positive;
                case EffectType.EnergyHeal: return EffectTargetPolarity.Positive;
                // Preserve/Disposable은 서브클래스 없이 이 베이스 클래스 그대로 쓰이는 카드 자신의
                // 성질(마커) 플래그라 User를 향한다.
                case EffectType.Preserve: return EffectTargetPolarity.Positive;
                case EffectType.Disposable: return EffectTargetPolarity.Positive;
                default:                return EffectTargetPolarity.Neutral;
            }
        }
    }

    // true면 EffectDisplay가 수치를 표시하지 않고(SetEffect), 강화 후보 가격 계산에서 magnitude를
    // 무조건 1로 취급하고(RewardManager.RollEnhanceOption), 강화 대상 덱 표시에서 이미 이 효과를
    // 가진 카드를 후보에서 제외한다(RewardManager.ShowEnhanceDeckSelection) — magnitude가 의미를
    // 갖지 않는 타입들의 공통 성격이다. 배율형 효과(Vulnerable ×1.2 등)는 continuous가 되면서 유지
    // 기간이 카드의 큐 체류로 정해지므로 magnitude가 더 이상 아무 의미가 없다.
    public virtual bool DoesntUseMagnitude
    {
        get
        {
            switch (_effectType)
            {
                case EffectType.Disposable:
                case EffectType.Preserve:
                case EffectType.Overcharge:
                case EffectType.Vulnerable:
                case EffectType.Weak:
                case EffectType.PoseBreak:
                case EffectType.Guard:
                case EffectType.Quicker:
                    return true;
                default:
                    return false;
            }
        }
    }

    public static Effect Create(EffectType type, int magnitude)
    {
        switch (type)
        {
            case EffectType.Attack:         return new AttackEffect(magnitude);
            case EffectType.Defend:         return new DefendEffect(magnitude);
            case EffectType.EnergyHeal:     return new EnergyHealEffect(magnitude);
            case EffectType.Vulnerable:     return new VulnerableEffect(magnitude);
            case EffectType.Weak:           return new WeakEffect(magnitude);
            case EffectType.Strength:       return new StrengthEffect(magnitude);
            case EffectType.Harden:         return new HardenEffect(magnitude);
            case EffectType.Guard:          return new GuardEffect(magnitude);
            case EffectType.PoseBreak:      return new PoseBreakEffect(magnitude);
            case EffectType.Burning:        return new BurningEffect(magnitude);
            case EffectType.AddDump:        return new AddDumpEffect(magnitude);
            case EffectType.Quicker:        return new QuickerEffect(magnitude);
            case EffectType.EnergyDrain:    return new EnergyDrainEffect(magnitude);
            case EffectType.Poison:         return new PoisonEffect(magnitude);
            case EffectType.Overcharge:     return new OverchargeEffect(magnitude);
            case EffectType.LastStand:      return new LastStandEffect(magnitude);
            case EffectType.VenomousAttack: return new VenomousAttackEffect(magnitude);
            default:                        return new Effect(type, magnitude);
        }
    }

    // CardEffect._effect는 SerializeField라 서브클래스로 역직렬화되지 않으므로(항상 base Effect로
    // 들어옴), "이 EffectType이 실질적으로 공격 계열(AttackEffect 및 그 자식)인가"는 타입 비교로는
    // 알 수 없다. Create로 진짜 런타임 서브클래스를 만들어 class 자체를 검사한다
    // (CardEffect.GetSupportedCategories()가 쓰는 것과 같은 패턴) — 이렇게 하면 VenomousAttack처럼
    // EffectType.Attack이 아닌 공격 계열도 전부 잡힌다.
    public static bool IsAttackType(EffectType type) => Create(type, 0) is AttackEffect;

    public virtual void OnExpired(CharacterManager subject) { }
    public virtual void OnTurnStarted(CharacterManager subject) { }
    public virtual void OnTurnEnded(CharacterManager subject) { }
    // subject(이 effect의 소유자)가 1 이상의 피해를 받을 때마다 CharacterManager.TakeDamage가 호출한다.
    // continuous(skill) 효과가 "나는 맞았다"는 사건에 반응하는 용도(예: 맞으면 사라지는 버프, 맞을
    // 때마다 방어도를 얻는 효과) — 공격 쪽의 대응 훅(AttackEffect.OnDamaging, "내가 때렸다")과 대칭이다.
    public virtual void OnDamaged(CharacterManager subject, int damageTaken) { }
    // Instant 효과(공격 계열)가 카드 쿨타임이 다 됐을 때 대상에게 적용되는 지점(CharacterManager.ApplyEffect).
    // 스킬은 전부 Continuous라 이 경로를 타지 않는다 — AttackEffect만 구현한다.
    public virtual void OnApply(CharacterManager subject) { }
    public virtual void OnApplyingOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
    public virtual void OnAppliedOther(CharacterManager subject, CardEffect effect, bool actualUse) { }

    // 스킬(Continuous) 효과의 기본 동작: 카드가 큐에 들어가는 순간(CardInstance.EnterQueue) 대상에게
    // 이 효과를 부여한다. 같은 타입의 풀이 이미 있으면 거기에 magnitude를 합산하고, 없으면 새 풀을
    // 만든다 — 풀은 기여한 카드(들)가 큐에 머무는 동안만 존재하다가 OnExitQueue로 정확히 되돌아간다.
    // 방어(DefendEffect)처럼 큐 카드 자신의 잔여 magnitude로 방어도를 관리하는 특수 케이스는 이
    // 기본 동작을 오버라이드해서 쓰지 않는다.
    public virtual void OnEnterQueue(CharacterManager subject, CardInstance self, CardEffect cardEffect)
    {
        if (IsCardMarker) return;
        CharacterManager target = cardEffect.GetTarget(subject);

        foreach (Effect existing in target.GetEffects())
        {
            if (existing.GetEffectType() != GetEffectType() || !existing.IsInfinite) continue;
            existing.AddMagnitude(_magnitude);
            existing.ContinuousSources++;
            PlayApplySound();
            return;
        }

        IsInfinite = true;
        ContinuousSources = 1;
        target.AddEffect(this);
        PlayApplySound();
    }

    // OnEnterQueue가 부여한 magnitude만큼(grantedMagnitude) continuous 풀(IsInfinite인 인스턴스)에서
    // 되돌린다. 카드가 큐를 떠날 때(재생되어 CardInstance.ExitQueue가 호출될 때) 실행된다. 기여하던
    // 카드가 전부 떠나면 효과 자체를 제거한다. 풀이 이미 사라졌으면(Overcharge처럼 스스로 소멸하는
    // 효과) 아무것도 하지 않는다.
    public virtual void OnExitQueue(CharacterManager subject, CardInstance self, CardEffect cardEffect, int grantedMagnitude)
    {
        if (IsCardMarker) return;
        CharacterManager target = cardEffect.GetTarget(subject);

        foreach (Effect existing in target.GetEffects())
        {
            if (existing.GetEffectType() != GetEffectType() || !existing.IsInfinite) continue;
            existing.AddMagnitude(-grantedMagnitude);
            existing.ContinuousSources--;
            if (existing.ContinuousSources <= 0)
                target.RemoveEffectInstance(existing);
            return;
        }
    }

    // 이 Effect가 실제로 획득/발동될 때 SoundManager로 재생할 EffectSound.
    // 기본은 TargetPolarity(Positive→Buff, 그 외→Debuff)를 따르되, 고유한 사운드가 있는 타입은
    // 예외로 지정한다(Burning→Burn, Defend→ShieldGet, Quicker→TimeSkip).
    // 공격 계열(Attack, VenomousAttack)은 CardInstance.Play/CharacterManager.Attacked의 전용 사운드로
    // 따로 처리하므로 이 메서드를 호출하지 않는다.
    protected void PlayApplySound()
    {
        EffectSound sound;
        switch (_effectType)
        {
            case EffectType.Burning:
                sound = EffectSound.Burn;
                break;
            case EffectType.Defend:
                sound = EffectSound.ShieldGet;
                break;
            case EffectType.Quicker:
                sound = EffectSound.TimeSkip;
                break;
            default:
                sound = TargetPolarity == EffectTargetPolarity.Positive ? EffectSound.Buff : EffectSound.Debuff;
                break;
        }
        SoundManager.Instance?.Play(sound);
    }
}
