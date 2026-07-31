using System.Collections.Generic;
using UnityEngine;

// CardDefinition 리스트를 Card 프리팹 4열 그리드로 배치한다. 컨테이너 크기는 SetSize로
// 외부에서 직접 주입받는다(배경 스프라이트가 없는 순수 레이아웃 컨테이너이기 때문).
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

    public void SetDeck(List<CardDefinition> cards, float horizontalPadding, float? verticalPadding = null)
    {
        ClearCards();
        if (cards == null || cards.Count == 0) return;

        if (_cardPrefab == null)
            _cardPrefab = Resources.Load<GameObject>("Prefabs/Card");
        if (_cardPrefab == null || _containerSize.x == 0f || _containerSize.y == 0f) return;

        foreach (CardDefinition def in cards)
            _cardVisuals.Add(SpawnCard(def));

        Vector2 containerSize = _containerSize;
        Vector2 nativeCardSize = _cardVisuals[0].GetBackgroundSize();
        float vPadding = verticalPadding ?? horizontalPadding;

        _cardWidth = (containerSize.x - horizontalPadding * (Columns - 1)) / Columns;
        _cardHeight = _cardWidth * (nativeCardSize.y / nativeCardSize.x);
        _cellWidth = _cardWidth + horizontalPadding;
        _cellHeight = _cardHeight + vPadding;
        _visibleRows = Mathf.Max(1, Mathf.FloorToInt((containerSize.y + vPadding) / _cellHeight));

        foreach (CardVisual visual in _cardVisuals)
            visual.SetSize(new Vector2(_cardWidth, _cardHeight));

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
        return visual;
    }

    private void ClearCards()
    {
        foreach (CardVisual visual in _cardVisuals)
            if (visual != null) Destroy(visual.gameObject);
        _cardVisuals.Clear();
    }
}
