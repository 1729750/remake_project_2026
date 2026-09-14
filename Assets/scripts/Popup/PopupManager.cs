using System;
using System.Collections.Generic;
using UnityEngine;

// 고정 위치 팝업 표시자. 예전엔 Card/RewardDisplay/EnemyDisplay 프리팹마다 PopupDisplay + 고정
// 3슬롯(PopUpComponent)이 각각 붙어 있어서, 그 오브젝트가 select될 때마다 필요한 슬롯만 켜고
// 나머지는 꺼서 보여줬다. 그 다음엔 씬 전역에서 단 하나의 PopupManager.Instance를 CardVisual/
// RewardDisplay/MapVisual이 전부 공유하는 방식을 거쳤는데, Hand(전투 손패)/Reward(보상 화면)/
// Map(적 선택 화면)처럼 동시에 별도로 켜져 있을 수 있는 화면들이 팝업 하나를 두고 서로 덮어써버리는
// 문제가 있었다. 지금은 화면(매니저)마다 자신만의 PopupManager 인스턴스를 하나씩 소유한다 —
// static Instance는 없다. CardVisual/RewardDisplay/DeckDisplay는 그 인스턴스를 생성 시점에
// 넘겨받아(SetPopupManager) 자기 select 상태가 바뀔 때마다 스스로 Show를 부르지만, Map(적 선택
// 화면)은 후보 카드 여럿이 동시에 존재해 그 방식대로 두면 서로 Show를 덮어써버리므로 MapManager가
// (지금 선택된 후보인지, 열어본 deck에서 select된 카드인지 판단해서) 직접 Show를 호출한다.
// 넘겨받은 EffectType 전부를 Show()로 받으면 (아이콘, 텍스트) 목록으로 변환해 들고 있다가
// PopUpComponent 하나만 재사용해 한 번에 하나씩 보여준다 — 나머지는 전용 입력(Popup 액션맵의
// NextPage/PrevPage)으로 페이지 넘기듯 넘겨본다.
public class PopupManager : MonoBehaviour
{
    // 인스펙터에서 직접 드래그해 연결하던 걸 GetComponentInChildren로 바꿨다 — 프리팹 변형이 여럿
    // 생기면서 다른 인스턴스의 PopUpComponent를 잘못 연결해두는 실수가 실제로 있었다. 자식에서
    // 스스로 찾으면 애초에 잘못 연결할 수가 없다.
    private PopupComponent popupComponent;

    private readonly List<(Sprite icon, string text)> _entries = new List<(Sprite, string)>();
    private int _pageIndex;

    private void Awake()
    {
        popupComponent = GetComponentInChildren<PopupComponent>(true);
        if (popupComponent != null) popupComponent.gameObject.SetActive(false);
    }

    // Select/Battle 등 화면별 입력 context(PlayerInputManager의 메인 스택)와 별개로 항상 폴링되어야
    // 페이지 넘기기가 그 화면의 커서 이동과 충돌하지 않는다 — 그래서 메인 Load/Unload 대신
    // LoadOverlay/UnloadOverlay를 쓴다. 이 PopupManager가 속한 화면이 켜져 있는 동안만(=이 컴포넌트가
    // Enable인 동안만) NextPage/PrevPage를 받는다.
    private void OnEnable()
    {
        PlayerInputManager.Instance?.LoadOverlay("Popup", new Dictionary<string, Action>
        {
            ["NextPage"] = () => MovePage(1),
            ["PrevPage"] = () => MovePage(-1),
        });
    }

    private void OnDisable()
    {
        PlayerInputManager.Instance?.UnloadOverlay("Popup");
    }

    private void MovePage(int delta)
    {
        if (_entries.Count <= 1) return;

        _pageIndex = ((_pageIndex + delta) % _entries.Count + _entries.Count) % _entries.Count;
        RefreshCurrentPage();
    }

    // 호출부가 넘겨준 effectTypes 전부(개수 제한 없음)를 아이콘/텍스트로 변환해 저장하고 0페이지부터
    // 보여준다. null이거나 비어 있으면 팝업을 끈다.
    public void Show(IReadOnlyList<EffectType> effectTypes)
    {
        _entries.Clear();
        if (effectTypes != null)
            foreach (EffectType effectType in effectTypes)
                _entries.Add((BattleManager.GetEmoji(effectType), GameManager.GetEffectSummary(effectType)));
        _pageIndex = 0;

        if (_entries.Count == 0)
        {
            if (popupComponent != null) popupComponent.gameObject.SetActive(false);
            return;
        }

        if (popupComponent != null) popupComponent.gameObject.SetActive(true);
        RefreshCurrentPage();
    }

    public void Hide() => Show(null);

    private void RefreshCurrentPage()
    {
        if (popupComponent == null || _entries.Count == 0) return;
        (Sprite icon, string text) = _entries[_pageIndex];
        popupComponent.SetEffect(icon, text);
    }
}
