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
                {
                    var visual = cardObject.GetComponent<CardVisual>();
                    if (visual != null)
                        visual.SetFace(true);
                }

                for (int j = i; j > 0; j--)
                {
                    if (_queue[j].GetCooldownLeft() < _queue[j - 1].GetCooldownLeft())
                    {
                        (_queue[j], _queue[j - 1]) = (_queue[j-1], _queue[j]);
                    }
                }
                return true;
            }
        }

        return false;
    }

    public CardInstance[] GetQueue() => _queue;
    public Transform GetSlot(int index) => (_slots != null && index >= 0 && index < _slots.Length) ? _slots[index] : null;

    public void TickQueueCards(CharacterManager characterManager)
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] == null) continue;

            _queue[i].TickCooldown();
            if (_queue[i].IsReady())
            {
                characterManager.PlayCard(_queue[i]);
                _queue[i] = null;
                DestroyCardVisual(i);
            }
        }
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
