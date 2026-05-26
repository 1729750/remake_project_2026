public class CardEffect
{
    private Effect _effect;
    private BattleManager _user;
    private BattleManager _target;

    public CardEffect(Effect effect, BattleManager user, BattleManager target)
    {
        _effect = effect;
        _user = user;
        _target = target;
    }

    public Effect GetEffect() => _effect;
    public BattleManager GetUser() => _user;
    public BattleManager GetTarget() => _target;
}
