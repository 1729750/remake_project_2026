using System.Collections.Generic;
using UnityEngine;

public class HandManager_Multi
{
    private const int HandSize = 4;

    private readonly CardInstance_Multi[] _hand =
        new CardInstance_Multi[HandSize];

    private int _selectedIndex = -1;

    private readonly GameObject[] _cardObjects =
        new GameObject[HandSize];

    private readonly CharacterManager_Multi _characterManager;
    private readonly Transform[] _slots;
    private readonly GameObject _cardPrefab;
    private readonly bool _isHandVisualized;

    public HandManager_Multi(
        CharacterManager_Multi characterManager,
        GameObject slotsRoot,
        bool isHandVisualized)
    {
        _characterManager = characterManager;
        _slots = BuildSlots(slotsRoot);
        _cardPrefab =
            Resources.Load<GameObject>("Prefabs/Card");
        _isHandVisualized = isHandVisualized;
    }

    private static Transform[] BuildSlots(GameObject root)
    {
        if (root == null)
            return null;

        var slots = new List<Transform>();

        foreach (Transform child in root.transform)
            slots.Add(child);

        return slots.ToArray();
    }

    public void FillHand()
    {
        for (int i = 0; i < HandSize; i++)
        {
            if (_hand[i] != null)
                continue;

            _hand[i] =
                _characterManager.DrawCard();

            if (_hand[i] != null)
                CreateCardVisual(i);
        }

        RefreshSelection();
    }

    private void CreateCardVisual(int i)
    {
        if (_slots == null ||
            i < 0 ||
            i >= _slots.Length ||
            _slots[i] == null ||
            _cardPrefab == null)
        {
            return;
        }

        GameObject obj =
            Object.Instantiate(
                _cardPrefab,
                _slots[i]);

        obj.transform.localPosition =
            Vector3.zero;

        CardVisual visual =
            obj.GetComponent<CardVisual>();

        if (visual == null)
            visual = obj.AddComponent<CardVisual>();

        _hand[i].SetVisual(visual);
        _hand[i].SetFace(_isHandVisualized);

        _hand[i].RefreshDisplay(
            _characterManager,
            _characterManager.GetQueue());

        _cardObjects[i] = obj;
    }

    public void RefreshHandDisplay()
    {
        CardInstance_Multi[] queue =
            _characterManager.GetQueue();

        for (int i = 0; i < HandSize; i++)
        {
            _hand[i]?.RefreshDisplay(
                _characterManager,
                queue);
        }
    }

    public void ReturnHandToDeck()
    {
        for (int i = 0; i < HandSize; i++)
        {
            if (_hand[i] == null)
                continue;

            bool trigger = false;

            foreach (
                CardEffect_Multi cardEffect
                in _hand[i].GetEffects())
            {
                if (cardEffect.GetEffect().GetEffectType() ==
                    EffectType.Preserve)
                {
                    trigger = true;
                }
            }

            if (trigger)
                continue;

            _characterManager.ReturnToDeck(
                _hand[i]);

            _hand[i] = null;

            if (_cardObjects[i] != null)
            {
                Object.Destroy(
                    _cardObjects[i]);

                _cardObjects[i] = null;
            }
        }

        UnselectCard();
    }

    public void ClearHand()
    {
        for (int i = 0; i < HandSize; i++)
        {
            _hand[i] = null;

            if (_cardObjects[i] != null)
            {
                Object.Destroy(
                    _cardObjects[i]);

                _cardObjects[i] = null;
            }
        }

        ClearSelection();
    }

    public void SelectCard(int index)
    {
        int previous = _selectedIndex;

        if (_selectedIndex == index)
            _selectedIndex = -1;
        else
            _selectedIndex = index;

        if (_selectedIndex != -1)
            SoundManager.Instance?.Play(EffectSound.Select);
        else if (previous != -1)
            SoundManager.Instance?.Play(EffectSound.Unselect);

        RefreshSelection();
    }

    public void UnselectCard()
    {
        bool wasSelected =
            _selectedIndex != -1;

        ClearSelection();

        if (wasSelected)
            SoundManager.Instance?.Play(EffectSound.Unselect);
    }

    private void ClearSelection()
    {
        _selectedIndex = -1;
        RefreshSelection();
    }

    public CardInstance_Multi[] GetHand()
    {
        return _hand;
    }

    public CardInstance_Multi GetSelectedCard()
    {
        return _selectedIndex >= 0 &&
               _selectedIndex < HandSize
            ? _hand[_selectedIndex]
            : null;
    }

    public bool UseCard()
    {
        return UseCardImmediately(
            _selectedIndex);
    }

    public bool UseCardImmediately(int index)
    {
        if (index < 0 ||
            index >= HandSize ||
            _hand[index] == null)
        {
            return false;
        }

        CardInstance_Multi card =
            _hand[index];

        GameObject cardObject =
            _cardObjects[index];

        if (_characterManager.QueueCard(
                card,
                cardObject))
        {
            SoundManager.Instance?.Play(
                EffectSound.UseCard);

            card.SetSelected(false);

            _cardObjects[index] = null;
            _hand[index] = null;

            if (_selectedIndex == index)
                ClearSelection();

            return true;
        }

        SoundManager.Instance?.Play(
            EffectSound.PlayDenied);

        return false;
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < HandSize; i++)
        {
            _hand[i]?.SetSelected(
                i == _selectedIndex);
        }
    }
}
