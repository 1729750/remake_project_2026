using UnityEngine;

public class QueueManager : MonoBehaviour
{
    private const int QueueSize = 3;
    private CardInstance[] _queue = new CardInstance[QueueSize];
    private GameObject[] _cardObjects = new GameObject[QueueSize];

    [SerializeField] private GameObject cardPrefab;

    private Transform[] _slots;

    private void Awake()
    {
        _slots = new Transform[QueueSize];
        for (int i = 0; i < QueueSize; i++)
            _slots[i] = transform.GetChild(i);
    }

    public bool AddCard(CardInstance card)
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] == null)
            {
                _queue[i] = card;
                SpawnCardVisual(i);
                return true;
            }
        }

        return false;
    }

    public CardInstance[] GetQueue() => _queue;

    public void TickQueueCards(BattleManager battleManager)
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] == null) continue;

            _queue[i].TickCooldown();
            if (_queue[i].IsReady())
            {
                _queue[i].Play(battleManager);
                _queue[i] = null;
                DestroyCardVisual(i);
            }
        }
    }

    private void SpawnCardVisual(int index)
    {
        if (_cardObjects[index] != null)
            Destroy(_cardObjects[index]);

        GameObject obj = Instantiate(cardPrefab, _slots[index]);
        obj.transform.localPosition = Vector3.zero;
        CardVisual visual = obj.AddComponent<CardVisual>();
        visual.SetCard(_queue[index]);
        _cardObjects[index] = obj;
    }

    private void DestroyCardVisual(int index)
    {
        if (_cardObjects[index] != null)
        {
            Destroy(_cardObjects[index]);
            _cardObjects[index] = null;
        }
    }
}
