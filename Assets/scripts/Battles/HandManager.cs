using System.Collections.Generic;
using UnityEngine;

public class HandManager
{
    private const int HandSize = 4;
    private readonly CardInstance[] _hand = new CardInstance[HandSize];
    private int _selectedIndex = -1;
    private readonly GameObject[] _cardObjects = new GameObject[HandSize];

    private readonly CharacterManager _characterManager;
    private readonly Transform[] _slots;
    private readonly GameObject _cardPrefab;
    private readonly bool _isHandVisualized;

    public HandManager(CharacterManager characterManager, GameObject slotsRoot, bool isHandVisualized)
    {
        _characterManager = characterManager;
        _slots = BuildSlots(slotsRoot);
        _cardPrefab = Resources.Load<GameObject>("Prefabs/Card");
        _isHandVisualized = isHandVisualized;
    }

    private static Transform[] BuildSlots(GameObject root)
    {
        if (root == null) return null;

        var slots = new List<Transform>();
        foreach (Transform child in root.transform)
                    slots.Add(child);
        return slots.ToArray();
    }

    public void FillHand()
    {
        for (int i = 0; i < HandSize; i++)
        {
            if (_hand[i] == null)
            {
                _hand[i] = _characterManager.DrawCard();
                if (_hand[i] != null)
                    CreateCardVisual(i);
            }
        }
        RefreshSelection();
    }

    private void CreateCardVisual(int i)
    {
        if (_slots == null || _cardPrefab == null) return;

        GameObject obj = Object.Instantiate(_cardPrefab, _slots[i]);
        obj.transform.localPosition = Vector3.zero;
        var visual = obj.GetComponent<CardVisual>();
        if (visual == null)
            visual = obj.AddComponent<CardVisual>();
        visual.SetCard(_hand[i], _isHandVisualized);
        _cardObjects[i] = obj;
    }

    // 손패 전체를 소유자의 덱으로 되돌린다 (ReDraw용)
    public void ReturnHandToDeck()
    {
        for (int i = 0; i < HandSize; i++)
        {
            if (_hand[i] == null) continue;
            bool trigger = false;
            foreach (CardEffect cardEffect in _hand[i].GetEffects())
            {
                if (cardEffect.GetEffect().GetEffectType() == EffectType.Preserve)
                {
                    trigger = true;
                }
            }

            if (trigger) continue;
            
            _characterManager.ReturnToDeck(_hand[i]);
            _hand[i] = null;
            if (_cardObjects[i] != null)
            {
                Object.Destroy(_cardObjects[i]);
                _cardObjects[i] = null;
            }
        }
        UnselectCard();
    }

    public void SelectCard(int index)
    {
        if (_selectedIndex == index) _selectedIndex = -1;
        else _selectedIndex = index;
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
        GameObject cardObject = _cardObjects[_selectedIndex];
        if (_characterManager.QueueCard(card, cardObject))
        {
            if (cardObject != null)
            {
                var visual = cardObject.GetComponent<CardVisual>();
                if (visual != null)
                    visual.SetSelected(false);
            }
            _cardObjects[_selectedIndex] = null;
            _hand[_selectedIndex] = null;
            UnselectCard();
            return true;
        }

        return false;
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
