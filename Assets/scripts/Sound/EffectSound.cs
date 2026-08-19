// SoundManager.Play(EffectSound)가 Resources/Sound/{카테고리}/{이 enum 이름}에서 AudioClip을 찾는다.
// 카테고리(하위 폴더)는 SoundManager.EffectSoundCategory가 정한다.
// 새 효과음을 추가하려면 여기에 항목을 추가하고, Resources/Sound/{카테고리}/에 동일한 이름의 클립을 넣고,
// SoundManager.EffectSoundCategory에도 카테고리를 등록해야 한다.
public enum EffectSound
{
    // Sound/Attack — 공격 카드 발동 및 피격 반응
    Attack,
    UpcomingAttack,
    DamageWeak,
    Damage,
    DamageBig,
    DamageGuarded,
    DamageShielded,

    // Sound/Battle — 전투 진행(턴/전투 시작·종료)
    BattleStart,
    TurnEnd1,
    TurnEnd2,
    PlayerWin,

    // Sound/Card — 손패 카드 조작
    Select,
    Unselect,
    MoveSelect1,
    MoveSelect2,
    UseCard,
    PlayCard,

    // Sound/Effect — 버프/디버프 획득
    Buff,
    Debuff,
    Burn,
    ShieldGet,
    TimeSkip,
}
