using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 고정 위치 팝업 표시자. 예전엔 Card/RewardDisplay/EnemyDisplay 프리팹마다 PopupDisplay + 고정
// 3슬롯(PopUpComponent)이 각각 붙어 있어서, 그 오브젝트가 select될 때마다 필요한 슬롯만 켜고
// 나머지는 꺼서 보여줬다. 그 다음엔 씬 전역에서 단 하나의 PopupManager.Instance를 CardVisual/
// RewardDisplay/MapVisual이 전부 공유하는 방식을 거쳤는데, Hand(전투 손패)/Reward(보상 화면)/
// Map(적 선택 화면)처럼 동시에 별도로 켜져 있을 수 있는 화면들이 팝업 하나를 두고 서로 덮어써버리는
// 문제가 있었다. 지금은 화면(매니저)마다 자신만의 PopupManager 인스턴스를 하나씩 소유하고,
// CardVisual/RewardDisplay/MapVisual/DeckDisplay는 그 인스턴스를 생성 시점에 넘겨받아(SetPopupManager)
// 쓴다 — static Instance는 없다.
// 넘겨받은 EffectType 전부를 Show()로 받으면 (아이콘, 텍스트) 목록으로 변환해 들고 있다가
// PopUpComponent 하나만 재사용해 한 번에 하나씩 보여준다 — 나머지는 전용 입력(Popup 액션맵의
// NextPage/PrevPage)으로 페이지 넘기듯 넘겨본다.
public class PopupManager : MonoBehaviour
{
    [SerializeField] private PopupComponent popupComponent;
    // Select/Battle 등 화면별 입력 context(PlayerInputManager의 스택)와 별개로 항상 켜져 있어야
    // 페이지 넘기기가 그 화면의 커서 이동과 충돌하지 않는다 — 그래서 스택을 타는
    // PlayerInputManager.Load를 쓰지 않고, 이 컴포넌트가 직접 Popup 액션맵을 찾아 계속 Enable해둔다.
    [SerializeField] private InputActionAsset inputActions;

    private InputAction _nextPageAction;
    private InputAction _prevPageAction;
    

    private readonly List<(Sprite icon, string text)> _entries = new List<(Sprite, string)>();
    private int _pageIndex;

    private void Awake()
    {
        if (popupComponent != null) popupComponent.gameObject.SetActive(false);

        if (inputActions == null) return;
        InputActionMap map = inputActions.FindActionMap("Popup", throwIfNotFound: false);
        if (map == null) return;

        _nextPageAction = map.FindAction("NextPage");
        _prevPageAction = map.FindAction("PrevPage");
        map.Enable();
    }

    private void Update()
    {
        if (_entries.Count <= 1) return;

        if (_nextPageAction != null && _nextPageAction.triggered) MovePage(1);
        else if (_prevPageAction != null && _prevPageAction.triggered) MovePage(-1);
    }

    private void MovePage(int delta)
    {
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
