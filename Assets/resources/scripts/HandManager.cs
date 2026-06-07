using UnityEngine;

public class HandManager
{
    private const int HandSize = 4;
    private CardInstance[] _hand = new CardInstance[HandSize];
    private int _selectedIndex = -1;
    private GameObject[] _cardObjects = new GameObject[HandSize];

    private BattleManager _battleManager;
    private QueueManager _queueManager;
    private Transform[] _slots;
    private GameObject _cardPrefab;

    public HandManager(BattleManager battleManager, QueueManager queueManager, Transform[] slots, GameObject cardPrefab)
    {
        _battleManager = battleManager;
        _queueManager = queueManager;
        _slots = slots;
        _cardPrefab = cardPrefab;
    }

    public void FillHand()
    {
        for (int i = 0; i < HandSize; i++)
        {
            if (_hand[i] == null)
                _hand[i] = _battleManager.DrawCard();
        }
        RefreshVisuals();
    }

    public void SelectCard(int index)
    {
        _selectedIndex = index;
        RefreshSelection();
    }

    public void UnselectCard()
    {
        _selectedIndex = -1;
        RefreshSelection();
    }
    public CardInstance[] GetHand() => _hand;
    public CardInstance GetSelectedCard() => (_selectedIndex >= 0 && _selectedIndex < HandSize) ? _hand[_selectedIndex] : null;

    public bool UseCard()
    {
        if (_selectedIndex < 0 || _selectedIndex >= HandSize || _hand[_selectedIndex] == null)
            return false;

        var card = _hand[_selectedIndex];
        card.Use();
        if (_queueManager.AddCard(card))
            _hand[_selectedIndex] = null;
        UnselectCard();
        RefreshVisuals();
        return true;
    }

    private void RefreshVisuals()
    {
        if (_slots == null || _cardPrefab == null) return;

        for (int i = 0; i < HandSize; i++)
        {
            if (_cardObjects[i] != null)
            {
                Object.Destroy(_cardObjects[i]);
                _cardObjects[i] = null;
            }

            if (_hand[i] != null)
            {
                GameObject obj = Object.Instantiate(_cardPrefab, _slots[i]);
                obj.transform.localPosition = Vector3.zero;
                obj.AddComponent<CardVisual>().SetCard(_hand[i]);
                _cardObjects[i] = obj;
            }
        }
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < HandSize; i++)
        {
            if (_cardObjects[i] == null) continue;
            var visual = _cardObjects[i].GetComponent<CardVisual>();
            if (visual != null)
                visual.SetSelected(i == _selectedIndex);
        }
    }
}
