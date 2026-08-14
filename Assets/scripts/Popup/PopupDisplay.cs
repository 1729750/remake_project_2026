using System.Collections.Generic;
using UnityEngine;

// Card/EnemyDisplay/RewardDisplay 프리팹 밑에 미리 붙어 있는 고정 팝업 슬롯 묶음.
// 슬롯(PopupComponent)은 프리팹 제작 시 이미 정해진 개수(현재 3개)만큼 자식으로 배치돼 있고,
// SetEffects가 필요한 개수만큼만 켜서 아이콘/텍스트를 채우고 나머지는 꺼둔다 — 슬롯 자체의
// 위치는 더 이상 코드가 계산하지 않는다(프리팹에서 이미 배치되어 있음).
public class PopupDisplay : MonoBehaviour
{
    private readonly List<PopupComponent> _slots = new List<PopupComponent>();
    private bool _slotsFound;
    private bool _clipChecked;

    // effectTypes 개수만큼만 슬롯을 켜서 아이콘(BattleManager.GetEmoji)/텍스트(GameManager.GetSummary)를
    // 채우고, 남는 슬롯은 끈다. 슬롯보다 개수가 많으면 넘치는 만큼은 무시한다.
    public void SetEffects(IReadOnlyList<EffectType> effectTypes)
    {
        EnsureSlots();

        // 화면 클리핑 검사는 실제로 보여줄 내용이 처음 생기는 시점에만, 딱 한 번 한다.
        // SetLayer("UI")만으로 먼저 호출될 때는(아직 select되지 않아 effectTypes가 null) 카드가
        // DeckDisplay/RewardManager의 레이아웃 배치를 거치기 전이라 위치가 확정되지 않은 상태라서,
        // 여기서 검사해버리면 엉뚱한 위치 기준으로 판정이 나고 그 결과가 영구히 캐싱돼버린다.
        bool hasContent = effectTypes != null && effectTypes.Count > 0;
        if (hasContent && !_clipChecked)
        {
            _clipChecked = true;
            CheckClipAndFlip();
        }

        gameObject.SetActive(true);
        for (int i = 0; i < _slots.Count; i++)
        {
            bool active = effectTypes != null && i < effectTypes.Count;
            _slots[i].gameObject.SetActive(active);
            if (active)
                _slots[i].SetEffect(BattleManager.GetEmoji(effectTypes[i]), GameManager.GetSummary(effectTypes[i]));
        }
    }

    private void EnsureSlots()
    {
        if (_slotsFound) return;
        _slots.AddRange(GetComponentsInChildren<PopupComponent>(true));
        _slotsFound = true;
    }

    // 슬롯 중 하나라도 화면 밖으로 잘리면 개별 슬롯이 아니라 popupDisplay 컨테이너 자체의 x좌표를
    // 반대편으로 뒤집는다 — 그러면 슬롯 전부가 한 번에 옮겨진다.
    private void CheckClipAndFlip()
    {
        Camera camera = Camera.main;
        bool anyClipped = false;
        foreach (PopupComponent slot in _slots)
        {
            if (slot.IsClipped(camera))
            {
                anyClipped = true;
                break;
            }
        }

        if (anyClipped)
        {
            Vector3 pos = transform.localPosition;
            pos.x = -pos.x;
            transform.localPosition = pos;
        }
    }
}
