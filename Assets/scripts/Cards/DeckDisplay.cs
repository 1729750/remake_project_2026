using System;
using System.Collections.Generic;
using UnityEngine;

// CardDefinition 리스트를 Card 프리팹 4열 그리드로 배치한다. 컨테이너 크기는 SetSize로
// 외부에서 직접 주입받는다(배경 스프라이트가 없는 순수 레이아웃 컨테이너이기 때문).
// 카드 한 장의 크기는 cardPrefab의 원본(배경 SpriteRenderer) 크기 × cardSize로 정해지고,
// 카드 사이 padding은 "컨테이너 가로 폭에서 카드 4장 폭을 뺀 나머지를 3칸에 고르게 나눈 값"으로
// 자동 계산되기 때문에 cardPrefab 자체의 크기가 바뀌어도 SetDeck 호출부를 손댈 필요가 없다.
// 스크롤은 쓰지 않고, 세로 방향으로 화면에 다 들어가지 않는 행이 있으면 선택 이동에 맞춰
// 보이는 행 구간(_topRow)만 옮겨 페이징한다.
public class DeckDisplay : MonoBehaviour
{
    private const int Columns = 4;

    private RectTransform _rectTransform;
    private Vector2 _containerSize;
    private bool _sizeExplicitlySet;
    private GameObject _cardPrefab;
    private readonly List<CardInstance> _cardInstances = new List<CardInstance>();
    // _cardIndexMap[표시 인덱스] = filter를 통과해 표시된 카드의 원본 cards 리스트 인덱스.
    // 필터로 걸러진 카드가 있으면 표시 인덱스와 원본 인덱스가 어긋나므로 GetSelectedIndex가 이 맵을 거쳐 반환한다.
    private readonly List<int> _cardIndexMap = new List<int>();

    private float _cardWidth;
    private float _cardHeight;
    private float _cellWidth;
    private float _cellHeight;
    private int _visibleRows;
    private int _topRow;
    private int _selectedIndex;
    // WASD(Left/Right/Up/Down)로 선택을 옮길 때 MoveSelect1/2를 번갈아 재생하기 위한 토글.
    private bool _moveSelectToggle;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    // 레이아웃 계산에 쓰일 컨테이너 크기(월드 단위)를 명시적으로 지정한다. 이후 SetDeck을 몇 번을
    // 다시 불러도(재활용) 이 값이 계속 쓰이고, RectTransform 기반 기본값은 더 이상 참조하지 않는다.
    public void SetSize(Vector2 size)
    {
        _containerSize = size;
        _sizeExplicitlySet = true;
    }

    // SetSize로 크기를 명시적으로 받은 적이 없으면 자신의 RectTransform 크기를 그대로 컨테이너
    // 크기로 쓴다. DeckDisplay를 재활용(같은 인스턴스에 매번 다른 덱을 보여주는 용도)할 때, 매번
    // SetSize를 호출하지 않아도 프리팹/인스펙터에 잡아둔 RectTransform 크기가 기본값이 되게 한다.
    // rect.size는 이 RectTransform의 로컬(비스케일) 좌표계 값이라 lossyScale을 곱해 월드 단위로
    // 바꿔준다 — 카드 쪽 기준값(GetBackgroundSize, SpriteRenderer.bounds)이 월드 단위라서 맞춰야 한다.
    private void EnsureContainerSize()
    {
        if (_sizeExplicitlySet || _rectTransform == null) return;
        Vector2 localSize = _rectTransform.rect.size;
        Vector3 lossyScale = _rectTransform.lossyScale;
        _containerSize = new Vector2(localSize.x * lossyScale.x, localSize.y * lossyScale.y);
    }

    // cardSize: cardPrefab 원본 크기 대비 배율(1이 원본 크기). 이 값으로 카드의 실제 표시 크기를
    // 정하고, 카드 사이 padding은 Columns장이 컨테이너 가로 폭을 정확히 채우도록 역산한다.
    // 예: 가로 폭 100, 카드 가로 길이 10이면 100 - 10*4 = 60을 카드 사이 3칸에 나눠 padding 20.
    // filter: cards 중 표시할 카드만 골라낸다. 기본값은 전부 표시(항상 true).
    // 기존에 표시하고 있던 카드가 있으면 전부 정리(ClearCards)하고 새로 채우므로, DeckDisplay를
    // 파괴/재생성하지 않고 반복 호출해서 재활용하는 용도로도 그대로 쓸 수 있다.
    public void SetDeck(List<CardDefinition> cards, Func<CardDefinition, bool> filter, float cardSize = 1f)
    {
        filter ??= _ => true;

        ClearCards();
        if (cards == null || cards.Count == 0) return;

        EnsureContainerSize();

        if (_cardPrefab == null)
            _cardPrefab = Resources.Load<GameObject>("Prefabs/Card");
        if (_cardPrefab == null || _containerSize.x == 0f || _containerSize.y == 0f) return;

        for (int i = 0; i < cards.Count; i++)
        {
            if (!filter(cards[i])) continue;
            _cardInstances.Add(SpawnCard(cards[i]));
            _cardIndexMap.Add(i);
        }
        if (_cardInstances.Count == 0) return;

        // SetSize로 스케일하기 전, 카드 프리팹 원본(배경 SpriteRenderer) 크기를 기준으로 삼는다.
        Vector2 nativeCardSize = _cardInstances[0].GetBackgroundSize();
        _cardWidth = nativeCardSize.x * cardSize;
        _cardHeight = nativeCardSize.y * cardSize;

        float xpadding = (_containerSize.x - _cardWidth * Columns) / (Columns - 1);
        _cellWidth = _cardWidth + xpadding;

        // ypadding은 Columns(가로 4장 고정)를 참고할 수 없다 — 몇 줄이 보일지(visibleRows) 자체가
        // 미지수라서, ypadding으로 visibleRows를 구하려 하면 서로가 서로에 의존하는 순환이 생긴다.
        // 그래서 먼저 padding 없이(카드 높이만으로) 몇 줄이 들어가는지 늘려가며 귀납적으로 visibleRows를
        // 찾고, 그렇게 정해진 visibleRows로 컨테이너 세로 폭을 정확히 채우는 ypadding을 역산한다.
        _visibleRows = 1;
        while (_cardHeight * (_visibleRows + 1) <= _containerSize.y)
            _visibleRows++;

        float ypadding = _visibleRows > 1
            ? (_containerSize.y - _cardHeight * _visibleRows) / (_visibleRows - 1)
            : 0f;
        _cellHeight = _cardHeight + ypadding;

        Vector2 cardTargetSize = new Vector2(_cardWidth, _cardHeight);
        foreach (CardInstance instance in _cardInstances)
            instance.SetSize(cardTargetSize);

        // 생성 시점에는 아무 카드도 select되지 않은 상태(-1)로 둔다. select되려면 자신의 input이
        // load될 때 호출되는 SelectFirst를 거쳐야 한다.
        _selectedIndex = -1;
        _topRow = 0;
        LayoutCards();
    }

    // 이 DeckDisplay를 조작하는 input context가 load될 때 호출: 0번 카드를 select하고, 보이는
    // 행 구간도 맨 위(0행)로 되돌린다 — 이전에 스크롤해뒀던 상태로 재진입하지 않게 한다.
    public void SelectFirst()
    {
        if (_cardInstances.Count == 0) return;

        if (_selectedIndex >= 0)
            _cardInstances[_selectedIndex].SetSelected(false);
        _selectedIndex = 0;
        _cardInstances[0].SetSelected(true);

        _topRow = 0;
        LayoutCards();
    }

    // 이 DeckDisplay를 조작하던 input context가 unload될 때 호출: 다시 아무 것도 select되지 않은
    // 상태(-1)로 되돌린다.
    public void Deselect()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _cardInstances.Count)
            _cardInstances[_selectedIndex].SetSelected(false);
        _selectedIndex = -1;
    }

    // 표시된(필터를 통과한) 카드 기준 선택 인덱스가 아니라, SetDeck에 넘겼던 원본 리스트 인덱스를 반환한다.
    // 아직 아무 것도 select되지 않았다면(-1) -1을 그대로 반환한다.
    public int GetSelectedIndex() => _selectedIndex >= 0 ? _cardIndexMap[_selectedIndex] : -1;

    // 아직 아무 것도 select되지 않았다면(-1, input이 아직 load되지 않은 상태) 아무 일도 하지 않는다.
    public void MoveSelectionHorizontal(int delta)
    {
        if (_cardInstances.Count == 0 || _selectedIndex < 0) return;

        int newIndex = _selectedIndex + delta;
        if (newIndex < 0 || newIndex >= _cardInstances.Count) return;
        if (newIndex / Columns != _selectedIndex / Columns) return;

        SetSelectedIndex(newIndex);
        PlayMoveSelectSound();
    }

    public void MoveSelectionVertical(int delta)
    {
        if (_cardInstances.Count == 0 || _selectedIndex < 0) return;

        int col = _selectedIndex % Columns;
        int row = _selectedIndex / Columns;
        int totalRows = Mathf.CeilToInt((float)_cardInstances.Count / Columns);
        int newRow = Mathf.Clamp(row + delta, 0, totalRows - 1);
        if (newRow == row) return;

        SetSelectedIndex(Mathf.Min(newRow * Columns + col, _cardInstances.Count - 1));
        PlayMoveSelectSound();

        _topRow = Mathf.Clamp(_topRow, newRow - _visibleRows + 1, newRow);
        LayoutCards();
    }

    private void SetSelectedIndex(int index)
    {
        _cardInstances[_selectedIndex].SetSelected(false);
        _selectedIndex = index;
        _cardInstances[_selectedIndex].SetSelected(true);
    }

    private void PlayMoveSelectSound()
    {
        SoundManager.Instance?.Play(_moveSelectToggle ? EffectSound.MoveSelect1 : EffectSound.MoveSelect2);
        _moveSelectToggle = !_moveSelectToggle;
    }

    private void LayoutCards()
    {
        Vector3 origin = transform.position;
        float left = origin.x - _containerSize.x / 2f;
        float top = origin.y + _containerSize.y / 2f;

        for (int i = 0; i < _cardInstances.Count; i++)
        {
            int row = i / Columns;
            int col = i % Columns;
            int visibleRow = row - _topRow;
            bool visible = visibleRow >= 0 && visibleRow < _visibleRows;

            GameObject cardObject = _cardInstances[i].GetVisual().gameObject;
            cardObject.SetActive(visible);
            if (!visible) continue;

            float x = left + col * _cellWidth + _cardWidth / 2f;
            float y = top - visibleRow * _cellHeight - _cardHeight / 2f;
            cardObject.transform.position = new Vector3(x, y, origin.z);
        }
    }

    // 아직 소유자가 없는 카드라 owner 없이 표시 전용 CardInstance로 감싼다.
    private CardInstance SpawnCard(CardDefinition def)
    {
        GameObject obj = Instantiate(_cardPrefab, transform);
        CardVisual visual = obj.GetComponent<CardVisual>();
        var instance = new CardInstance(def, null);
        instance.SetVisual(visual);
        instance.SetFace(true);
        instance.SetLayer("UI");
        return instance;
    }

    private void ClearCards()
    {
        foreach (CardInstance instance in _cardInstances)
            if (instance != null && instance.GetVisual() != null) Destroy(instance.GetVisual().gameObject);
        _cardInstances.Clear();
        _cardIndexMap.Clear();
    }
}
