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
    private readonly List<CardVisual> _cardVisuals = new List<CardVisual>();

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
    public void SetDeck(List<CardDefinition> cards, float cardSize = 1f)
    {
        ClearCards();
        if (cards == null || cards.Count == 0) return;

        if (_cardPrefab == null)
            _cardPrefab = Resources.Load<GameObject>("Prefabs/Card");
        if (_cardPrefab == null || _containerSize.x == 0f || _containerSize.y == 0f) return;

        foreach (CardDefinition def in cards)
            _cardVisuals.Add(SpawnCard(def));

        // SetSize로 스케일하기 전, 카드 프리팹 원본(배경 SpriteRenderer) 크기를 기준으로 삼는다.
        Vector2 nativeCardSize = _cardVisuals[0].GetBackgroundSize();
        _cardWidth = nativeCardSize.x * cardSize;
        _cardHeight = nativeCardSize.y * cardSize;

        float padding = (_containerSize.x - _cardWidth * Columns) / (Columns - 1);
        _cellWidth = _cardWidth + padding;
        _cellHeight = _cardHeight + padding;
        _visibleRows = Mathf.Max(1, Mathf.FloorToInt((_containerSize.y + padding) / _cellHeight));

        Vector2 cardTargetSize = new Vector2(_cardWidth, _cardHeight);
        foreach (CardVisual visual in _cardVisuals)
            visual.SetSize(cardTargetSize);

        _selectedIndex = 0;
        _topRow = 0;
        LayoutCards();
        _cardVisuals[0].SetSelected(true);
    }

    public int GetSelectedIndex() => _selectedIndex;

    public void MoveSelectionHorizontal(int delta)
    {
        if (_cardVisuals.Count == 0) return;

        int newIndex = _selectedIndex + delta;
        if (newIndex < 0 || newIndex >= _cardVisuals.Count) return;
        if (newIndex / Columns != _selectedIndex / Columns) return;

        SetSelectedIndex(newIndex);
    }

    public void MoveSelectionVertical(int delta)
    {
        if (_cardVisuals.Count == 0) return;

        int col = _selectedIndex % Columns;
        int row = _selectedIndex / Columns;
        int totalRows = Mathf.CeilToInt((float)_cardVisuals.Count / Columns);
        int newRow = Mathf.Clamp(row + delta, 0, totalRows - 1);
        if (newRow == row) return;

        SetSelectedIndex(Mathf.Min(newRow * Columns + col, _cardVisuals.Count - 1));

        _topRow = Mathf.Clamp(_topRow, newRow - _visibleRows + 1, newRow);
        LayoutCards();
    }

    private void SetSelectedIndex(int index)
    {
        _cardVisuals[_selectedIndex].SetSelected(false);
        _selectedIndex = index;
        _cardVisuals[_selectedIndex].SetSelected(true);
    }

    private void LayoutCards()
    {
        Vector3 origin = transform.position;
        float left = origin.x - _containerSize.x / 2f;
        float top = origin.y + _containerSize.y / 2f;

        for (int i = 0; i < _cardVisuals.Count; i++)
        {
            int row = i / Columns;
            int col = i % Columns;
            int visibleRow = row - _topRow;
            bool visible = visibleRow >= 0 && visibleRow < _visibleRows;

            _cardVisuals[i].gameObject.SetActive(visible);
            if (!visible) continue;

            float x = left + col * _cellWidth + _cardWidth / 2f;
            float y = top - visibleRow * _cellHeight - _cardHeight / 2f;
            _cardVisuals[i].transform.position = new Vector3(x, y, origin.z);
        }
    }

    private CardVisual SpawnCard(CardDefinition def)
    {
        GameObject obj = Instantiate(_cardPrefab, transform);
        CardVisual visual = obj.GetComponent<CardVisual>();
        visual.SetCard(new CardInstance(def, null), true);
        visual.SetLayer("UI");
        return visual;
    }

    private void ClearCards()
    {
        foreach (CardVisual visual in _cardVisuals)
            if (visual != null) Destroy(visual.gameObject);
        _cardVisuals.Clear();
    }
}
