using System.Collections.Generic;
using UnityEngine;

public class QueueManager_Multi
{
    private const int QueueSize = 3;

    private CardInstance_Multi[] _queue =
        new CardInstance_Multi[QueueSize];

    private GameObject[] _cardObjects =
        new GameObject[QueueSize];

    private Transform[] _slots;

    public QueueManager_Multi(GameObject slotsRoot)
    {
        _slots = BuildSlots(slotsRoot);
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

    public bool AddCard(
        CardInstance_Multi card,
        GameObject cardObject)
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] != null)
                continue;

            _queue[i] = card;
            _cardObjects[i] = cardObject;

            if (cardObject != null)
                card.SetFace(true);

            for (int j = i; j > 0; j--)
            {
                if (_queue[j].GetCooldownLeft() <
                    _queue[j - 1].GetCooldownLeft())
                {
                    (_queue[j], _queue[j - 1]) =
                        (_queue[j - 1], _queue[j]);

                    (_cardObjects[j], _cardObjects[j - 1]) =
                        (_cardObjects[j - 1], _cardObjects[j]);
                }
                else
                {
                    break;
                }
            }

            MoveVisualsToSlots();
            return true;
        }

        return false;
    }

    private void MoveVisualsToSlots()
    {
        if (_slots == null)
            return;

        for (int i = 0;
             i < QueueSize && i < _slots.Length;
             i++)
        {
            if (_cardObjects[i] == null ||
                _slots[i] == null)
            {
                continue;
            }

            _cardObjects[i].transform.SetParent(
                _slots[i],
                true);

            if (_queue[i] != null)
                _queue[i].MoveTo(_slots[i].position);
            else
                _cardObjects[i].transform.position =
                    _slots[i].position;
        }
    }

    public void ClearQueue()
    {
        for (int i = 0; i < QueueSize; i++)
        {
            _queue[i] = null;
            DestroyCardVisual(i);
        }
    }

    public CardInstance_Multi[] GetQueue()
    {
        return _queue;
    }

    public Transform GetSlot(int index)
    {
        return _slots != null &&
               index >= 0 &&
               index < _slots.Length
            ? _slots[index]
            : null;
    }

    public bool HasFreeSlot()
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] == null)
                return true;
        }

        return false;
    }

    public void TickQueueCards(
        CharacterManager_Multi characterManager,
        int tick = 1)
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] == null)
                continue;

            _queue[i].TickCooldown(tick);

            foreach (
                CardEffect_Multi cardEffect
                in _queue[i].GetEffects())
            {
                cardEffect.GetEffect().OnTick(
                    characterManager);
            }
        }

        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] == null)
                continue;

            if (_queue[i].IsReady())
            {
                characterManager.PlayCard(
                    _queue[i]);

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

        for (int read = 0;
             read < QueueSize;
             read++)
        {
            if (_queue[read] == null)
                continue;

            if (write != read)
            {
                _queue[write] =
                    _queue[read];

                _cardObjects[write] =
                    _cardObjects[read];

                _queue[read] = null;
                _cardObjects[read] = null;
            }

            write++;
        }

        MoveVisualsToSlots();
    }

    private void DestroyCardVisual(int index)
    {
        if (_cardObjects[index] == null)
            return;

        Object.Destroy(
            _cardObjects[index]);

        _cardObjects[index] = null;
    }
}
