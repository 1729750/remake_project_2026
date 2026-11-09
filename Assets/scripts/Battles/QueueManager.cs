using System.Collections.Generic;
using UnityEngine;

public class QueueManager
{
    private const int QueueSize = 3;
    private CardInstance[] _queue = new CardInstance[QueueSize];
    private GameObject[] _cardObjects = new GameObject[QueueSize];

    private Transform[] _slots;
    private readonly GameObjectPool _cardPool;

    public QueueManager(GameObject slotsRoot, GameObjectPool cardPool)
    {
        _slots = BuildSlots(slotsRoot);
        _cardPool = cardPool;
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
            ReleaseCardVisual(i);
        }
    }

    public CardInstance[] GetQueue() => _queue;
    public Transform GetSlot(int index) => (_slots != null && index >= 0 && index < _slots.Length) ? _slots[index] : null;

    public bool HasFreeSlot()
    {
        for (int i = 0; i < QueueSize; i++)
            if (_queue[i] == null) return true;
        return false;
    }

    public void TickCooldowns(int tick = 1)
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] == null) continue;

            // Quicker 등으로 빨라진 틱 속도는 Continuous 카드에는 적용하지 않는다.
            _queue[i].TickCooldown(_queue[i].IsContinuousCard() ? 1 : tick);
        }
    }

    // 쿨다운이 다 된 카드 중 continuousCards에 해당하는 종류만 발동시킨다. BattleManager가 양쪽의
    // Instant 카드를 먼저, Continuous 카드를 나중에 발동시키므로, 같은 턴 종료에 끝나는 방어/버프는
    // 그 턴에 떨어지는 공격까지는 유지된다(양쪽 모두 동일하게).
    public void ResolveReadyCards(CharacterManager characterManager, bool continuousCards)
    {
        for (int i = 0; i < QueueSize; i++)
        {
            CardInstance card = _queue[i];
            if (card == null || !card.IsReady() || card.IsContinuousCard() != continuousCards) continue;

            _queue[i] = null;
            ReleaseCardVisual(i);
            characterManager.PlayCard(card);
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

    private void ReleaseCardVisual(int index)
    {
        if (_cardObjects[index] != null)
        {
            _cardPool?.Release(_cardObjects[index]);
            _cardObjects[index] = null;
        }
    }
}
