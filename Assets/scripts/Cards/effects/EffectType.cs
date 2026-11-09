// CostToCooldown/CooldownToCost/DivideCooldown/TimeSkip/DefenseToCooldown는 싱글플레이 효과
// 시스템(Effect.Create, CardDefinition .asset 등)에서는 더 이상 쓰지 않지만, 이 enum은
// Assets/scripts/HyeGyoServer(멀티플레이, 이번 리워크 범위 밖)의 *_Multi 클래스들이 이름으로
// 직접 참조하는 하나뿐인 공유 enum이라(별도 어셈블리 분리가 없음) 멤버 자체는 지우지 않고
// 남겨둔다 — 지우면 멀티플레이 쪽 컴파일이 깨진다. 숫자값은 기존 .asset 직렬화와의 호환을 위해
// 전부 명시적으로 고정해뒀다.
public enum EffectType
{
    Attack = 0,
    Defend = 1,
    Weak = 2,
    Vulnerable = 3,
    Strength = 4,
    Harden = 5,
    Guard = 6,
    PoseBreak = 7,
    EnergyHeal = 8,
    AddDump = 9,
    Burning = 10,
    CostToCooldown = 11,
    CooldownToCost = 12,
    DivideCooldown = 13,
    TimeSkip = 14,
    Quicker = 15,
    Preserve = 16,
    Disposable = 17,
    DefenseToCooldown = 18,
    EnergyDrain = 19,
    Poison = 20,
    Overcharge = 21,
    LastStand = 22,
    VenomousAttack = 23,
}
