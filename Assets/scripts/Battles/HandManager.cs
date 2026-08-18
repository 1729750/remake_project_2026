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
        _hand[i].SetVisual(visual);
        _hand[i].SetFace(_isHandVisualized);
        _hand[i].RefreshDisplay(_characterManager, _characterManager.GetQueue());
        _cardObjects[i] = obj;
    }

    // 매 턴 종료마다 CharacterManager가 호출해, 손패 카드들의 비용/효과 표시를 현재 버프 상태에 맞게 갱신한다.
    public void RefreshHandDisplay()
    {
        CardInstance[] queue = _characterManager.GetQueue();
        for (int i = 0; i < HandSize; i++)
            _hand[i]?.RefreshDisplay(_characterManager, queue);
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

    // 전투가 끝났을 때 손패를 덱으로 되돌리지 않고 그대로 비운다(다음 전투는 CharacterInit이 덱을 새로 만든다).
    public void ClearHand()
    {
        for (int i = 0; i < HandSize; i++)
        {
            _hand[i] = null;
            if (_cardObjects[i] != null)
            {
                Object.Destroy(_cardObjects[i]);
                _cardObjects[i] = null;
            }
        }
        ClearSelection();
    }

    // 카드를 선택하면(선택 대상이 바뀌는 경우 포함) Select, 선택된 카드를 다시 눌러 해제하면 Unselect.
    // MoveSelect1/2는 여기서 재생하지 않는다 — RewardManager/DeckDisplay의 WASD 커서 이동 전용.
    public void SelectCard(int index)
    {
        int previous = _selectedIndex;
        if (_selectedIndex == index) _selectedIndex = -1;
        else _selectedIndex = index;

        if (_selectedIndex != -1)
            SoundManager.Instance?.Play(EffectSound.Select);
        else if (previous != -1)
            SoundManager.Instance?.Play(EffectSound.Unselect);

        RefreshSelection();
    }

    public void UnselectCard()
    {
        bool wasSelected = _selectedIndex != -1;
        ClearSelection();
        if (wasSelected)
            SoundManager.Instance?.Play(EffectSound.Unselect);
    }

    // UseCard()가 확정 직후 손패에서 카드를 비울 때 쓰는, 소리 없는 선택 해제.
    // (UseCard 사운드와 겹쳐 Unselect까지 같이 울리는 것을 막는다.)
    private void ClearSelection()
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
            SoundManager.Instance?.Play(EffectSound.UseCard);
            card.SetSelected(false);
            _cardObjects[_selectedIndex] = null;
            _hand[_selectedIndex] = null;
            ClearSelection();
            return true;
        }

        return false;
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < HandSize; i++)
            _hand[i]?.SetSelected(i == _selectedIndex);
    }
}
