public class HandManager
{
    private const int HandSize = 4;
    private CardInstance[] _hand = new CardInstance[HandSize];
    private int _selectedIndex = -1;

    private BattleManager _battleManager;
    private QueueManager _queueManager;

    public HandManager(BattleManager battleManager, QueueManager queueManager)
    {
        _battleManager = battleManager;
        _queueManager = queueManager;
    }

    public void FillHand()
    {
        for (int i = 0; i < HandSize; i++)
        {
            if (_hand[i] == null)
                _hand[i] = _battleManager.DrawCard();
        }
    }

    public void SelectCard(int index) => _selectedIndex = index;

    public CardInstance[] GetHand() => _hand;

    public void UseCard()
    {
        if (_selectedIndex < 0 || _selectedIndex >= HandSize || _hand[_selectedIndex] == null)
            return;

        var card = _hand[_selectedIndex];
        card.Use();
        _hand[_selectedIndex] = null;
        _queueManager.AddCard(card);
        _selectedIndex = -1;
    }
}
