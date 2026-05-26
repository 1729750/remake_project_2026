public class CardInstance
{
    private CardDefinition _definition;
    private BattleManager _battleManager;
    private bool _isPlayed = false;
    private int _cooldownLeft;
    // effect will be added later

    public CardInstance(CardDefinition definition, BattleManager battleManager)
    {
        _definition = definition;
        _battleManager = battleManager;
        _isPlayed = false;
        _cooldownLeft = 0;
    }

    public CardDefinition GetDefinition() => _definition;
    public int GetCooldownLeft() => _cooldownLeft;
    public bool IsReady() => _cooldownLeft <= 0;

    public void Use()
    {
        _cooldownLeft = _definition.GetCooldown();
        _isPlayed = true;
    }

    public void TickCooldown()
    {
        if (_cooldownLeft > 0)
            _cooldownLeft--;
    }

    public void Play()
    {
        foreach (CardEffect cardEffect in _definition.GetEffects())
        {
            BattleManager resolved = cardEffect.GetTarget();
            resolved.ApplyEffect(cardEffect);
        }
        _battleManager.ReturnToDeck(this);
    }
}
