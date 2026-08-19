using System.Collections.Generic;
using UnityEngine;

public class QueueManager
{
    private const int QueueSize = 3;
    private CardInstance[] _queue = new CardInstance[QueueSize];
    private GameObject[] _cardObjects = new GameObject[QueueSize];

    private Transform[] _slots;

    public QueueManager(GameObject slotsRoot)
    {
        _slots = BuildSlots(slotsRoot);
    }

    private static Transform[] BuildSlots(GameObject root)
    {
        if (root == null) return null;

        var slots = new List<Transform>();
        foreach (Transform child in root.transform)
            slots.Add(child);
        return slots.ToArray();
    }

    public bool AddCard(CardInstance card, GameObject cardObject)
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] == null)
            {
                _queue[i] = card;
                _cardObjects[i] = cardObject;
                if (cardObject != null)
                    card.SetFace(true);
                for (int j = i; j > 0; j--)
                {
                    if (_queue[j].GetCooldownLeft() < _queue[j - 1].GetCooldownLeft())
                    {
                        (_queue[j], _queue[j - 1]) = (_queue[j-1], _queue[j]);
                        (_cardObjects[j], _cardObjects[j - 1]) = (_cardObjects[j - 1], _cardObjects[j]);
                    }
                    else
                    {
                        break;
                    }
                }
                MoveVisualsToSlots();

                return true;
            }
        }

        return false;
    }

    private void MoveVisualsToSlots()
    {
        if (_slots == null) return;

        for (int i = 0; i < QueueSize && i < _slots.Length; i++)
        {
            if (_cardObjects[i] == null || _slots[i] == null) continue;

            _cardObjects[i].transform.SetParent(_slots[i], true);
            if (_queue[i] != null)
                _queue[i].MoveTo(_slots[i].position);
            else
                _cardObjects[i].transform.position = _slots[i].position;
        }
    }

    // 전투가 끝났을 때 큐를 소유자의 덱으로 되돌리지 않고 그대로 비운다.
    public void ClearQueue()
    {
        for (int i = 0; i < QueueSize; i++)
        {
            _queue[i] = null;
            DestroyCardVisual(i);
        }
    }

    public CardInstance[] GetQueue() => _queue;
    public Transform GetSlot(int index) => (_slots != null && index >= 0 && index < _slots.Length) ? _slots[index] : null;

    public void TickQueueCards(CharacterManager characterManager, int tick = 1)
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] == null) continue;

            _queue[i].TickCooldown(tick);
            foreach (CardEffect cardEffect in _queue[i].GetEffects())
                cardEffect.GetEffect().OnTick(characterManager);
        }
        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] == null) continue;

            if (_queue[i].IsReady())
            {
                characterManager.PlayCard(_queue[i]);
                _queue[i] = null;
                DestroyCardVisual(i);
                i = 0;
            }
        }
        CompactQueue();
    }

    private void CompactQueue()
    {
        int write = 0;
        for (int read = 0; read < QueueSize; read++)
        {
            if (_queue[read] == null) continue;

            if (write != read)
            {
                _queue[write] = _queue[read];
                _cardObjects[write] = _cardObjects[read];
                _queue[read] = null;
                _cardObjects[read] = null;
            }
            write++;
        }
        MoveVisualsToSlots();
    }

    private void DestroyCardVisual(int index)
    {
        if (_cardObjects[index] != null)
        {
            Object.Destroy(_cardObjects[index]);
            _cardObjects[index] = null;
        }
    }
}
