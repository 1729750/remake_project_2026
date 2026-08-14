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

    private Vector2 _containerSize;
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

    // 레이아웃 계산에 쓰일 컨테이너 크기(월드 단위). SetDeck 전에 호출해야 한다.
    public void SetSize(Vector2 size) => _containerSize = size;

    // cardSize: cardPrefab 원본 크기 대비 배율(1이 원본 크기). 이 값으로 카드의 실제 표시 크기를
    // 정하고, 카드 사이 padding은 Columns장이 컨테이너 가로 폭을 정확히 채우도록 역산한다.
    // 예: 가로 폭 100, 카드 가로 길이 10이면 100 - 10*4 = 60을 카드 사이 3칸에 나눠 padding 20.
    // filter: cards 중 표시할 카드만 골라낸다. 기본값은 전부 표시(항상 true).
    public void SetDeck(List<CardDefinition> cards, Func<CardDefinition, bool> filter, float cardSize = 1f)
    {
        filter ??= _ => true;

        ClearCards();
        if (cards == null || cards.Count == 0) return;

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

        _selectedIndex = 0;
        _topRow = 0;
        LayoutCards();
        _cardInstances[0].SetSelected(true);
    }

    // 표시된(필터를 통과한) 카드 기준 선택 인덱스가 아니라, SetDeck에 넘겼던 원본 리스트 인덱스를 반환한다.
    public int GetSelectedIndex() => _cardIndexMap[_selectedIndex];

    public void MoveSelectionHorizontal(int delta)
    {
        if (_cardInstances.Count == 0) return;

        int newIndex = _selectedIndex + delta;
        if (newIndex < 0 || newIndex >= _cardInstances.Count) return;
        if (newIndex / Columns != _selectedIndex / Columns) return;

        SetSelectedIndex(newIndex);
    }

    public void MoveSelectionVertical(int delta)
    {
        if (_cardInstances.Count == 0) return;

        int col = _selectedIndex % Columns;
        int row = _selectedIndex / Columns;
        int totalRows = Mathf.CeilToInt((float)_cardInstances.Count / Columns);
        int newRow = Mathf.Clamp(row + delta, 0, totalRows - 1);
        if (newRow == row) return;

        SetSelectedIndex(Mathf.Min(newRow * Columns + col, _cardInstances.Count - 1));

        _topRow = Mathf.Clamp(_topRow, newRow - _visibleRows + 1, newRow);
        LayoutCards();
    }

    private void SetSelectedIndex(int index)
    {
        _cardInstances[_selectedIndex].SetSelected(false);
        _selectedIndex = index;
        _cardInstances[_selectedIndex].SetSelected(true);
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
