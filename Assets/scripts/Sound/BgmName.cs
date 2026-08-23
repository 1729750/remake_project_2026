// SoundManager.Play(BgmName)가 재생할 AudioClip을 찾는 두 가지 경로:
// - Title/Battle처럼 SoundManager.BgmResourcePathOverride에 없는 항목은 기존 방식대로
//   Resources/Bgm/{이 enum 이름}/ 폴더에서 찾는다(LoadAll로 첫 클립 사용).
// - BattleBGM/Menu처럼 Sound/{카테고리}/ 밑에 이미 있는 클립을 그대로 쓰고 싶으면
//   SoundManager.BgmResourcePathOverride에 전체 경로를 등록한다.
public enum BgmName
{
    Title,
    Battle,
    BattleBGM,
    Menu,
    GameWinBGM,
    GameLoseBGM,
}
