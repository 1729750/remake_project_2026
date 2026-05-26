public class QueueManager
{
    private const int QueueSize = 3;
    private CardInstance[] _queue = new CardInstance[QueueSize];

    public void AddCard(CardInstance card)
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] == null)
            {
                _queue[i] = card;
                return;
            }
        }
    }

    public CardInstance[] GetQueue() => _queue;

    public void TickQueueCards()
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (_queue[i] == null) continue;

            _queue[i].TickCooldown();
            if (_queue[i].IsReady())
            {
                _queue[i].Play();
                _queue[i] = null;
            }
        }
    }
}
