namespace CardGameServer.Game;

/// <summary>
/// Unity CharacterManager + HandManager + QueueManager 에서 게임 규칙만 옮긴 것.
/// 한 플레이어의 HP / 코스트 / 효과 / 덱 / 손패(4) / 큐(3).
/// </summary>
public sealed class Combatant
{
    public const int RedrawAction = -2;
    public const int GuardAction = -1;
    public const int MaxCost = 15;
    public const int HandSize = 4;
    public const int QueueSize = 3;

    private readonly Random random;
    private readonly List<Effect> effects = new();
    private readonly List<CardInstance> deck = new();
    private int tickSpeed = 1;

    public string PlayerId { get; }
    public string Name { get; }
    public Combatant Opponent { get; set; } = null!;

    /// <summary>카드 발동/피해/효과 획득 같은 순간을 Match에 알린다. (클라이언트가 같은 순간에 싱글과 같은 소리를 내도록)</summary>
    public Action<MatchEvent>? Events { get; set; }

    public void Emit(string kind, string? card = null, int amount = 0, string? effect = null, bool guarded = false, bool ok = false) =>
        Events?.Invoke(new MatchEvent(kind, PlayerId, card, amount, effect, guarded, ok));

    public int MaxHealth { get; }
    public int Health { get; private set; }
    public int Cost { get; private set; }
    public bool IsGuard { get; private set; }
    public int SelectedIndex { get; private set; } = -1;

    public CardInstance?[] Hand { get; } = new CardInstance?[HandSize];
    public CardInstance?[] Queue { get; } = new CardInstance?[QueueSize];
    public IReadOnlyList<CardInstance> Deck => deck;
    public IReadOnlyList<Effect> Effects => effects;

    public bool IsDefeated => Health <= 0;

    public Combatant(string playerId, string name, int maxHealth, IEnumerable<CardDef> startDeck, Random random)
    {
        PlayerId = playerId;
        Name = name;
        MaxHealth = maxHealth;
        Health = maxHealth;
        this.random = random;

        foreach (CardDef def in startDeck)
            deck.Add(new CardInstance(def, this));
        ShuffleDeck();
        FillHand();
    }

    public void ChangeTickSpeed(int delta) => tickSpeed += delta;

    private void ShuffleDeck()
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }

    // =========================================================
    // Turn
    // =========================================================

    public void OnTurnStart()
    {
        for (int i = effects.Count - 1; i >= 0; i--)
            effects[i].OnTurnStarted(this);

        int healEnergyAmount = 2;
        if (IsGuard) healEnergyAmount--;
        if (Cost >= 10) healEnergyAmount--;
        EnergyHeal(healEnergyAmount);

        // 지난 턴에 코스트가 모자라 "선택"만 해 둔 카드를 이번 턴 시작에 다시 시도한다
        string? selected = SelectedIndex >= 0 ? Hand[SelectedIndex]?.Def.Name : null;
        if (UseCardImmediately(SelectedIndex))
            Emit("use", card: selected, ok: true);
        FillHand();
    }

    public void OnTurnEnd()
    {
        TickQueueCards();
        for (int i = effects.Count - 1; i >= 0; i--)
            effects[i].OnTurnEnded(this);
    }

    // Unity QueueManager.TickQueueCards. Unity에서도 tickSpeed는 넘기지 않는다(항상 1). 그대로 둔다.
    private void TickQueueCards(int tick = 1)
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (Queue[i] == null) continue;
            Queue[i]!.TickCooldown(Queue[i]!.IsContinuousCard ? 1 : tick);
        }

        for (int i = 0; i < QueueSize; i++)
        {
            if (Queue[i] == null) continue;
            if (Queue[i]!.IsReady)
            {
                PlayCard(Queue[i]!);
                Queue[i] = null;
                i = 0; // Unity 원본과 같은 동작 (다음 반복은 1번 칸부터)
            }
        }

        CompactQueue();
    }

    private void CompactQueue()
    {
        int write = 0;
        for (int read = 0; read < QueueSize; read++)
        {
            if (Queue[read] == null) continue;
            if (write != read)
            {
                Queue[write] = Queue[read];
                Queue[read] = null;
            }
            write++;
        }
    }

    // =========================================================
    // Player Input
    // =========================================================

    /// <summary>0~3: 손패 카드 사용, -2: 리드로우, -1: 가드 토글. Unity CharacterManager.SelectCard.</summary>
    public void SelectCard(int index)
    {
        if (index == RedrawAction || index == GuardAction)
        {
            SelectedIndex = -1;
            if (index == RedrawAction)
                Redraw();
            else
                IsGuard = !IsGuard;
            return;
        }

        IsGuard = false;
        if (index < 0 || index >= HandSize || Hand[index] == null) return;

        string cardName = Hand[index]!.Def.Name;
        bool used = UseCardImmediately(index);
        Emit("use", card: cardName, ok: used);
        if (!used)
        {
            // 지금은 못 쓰면 선택(토글)만 해 두고 다음 턴 시작에 다시 시도한다
            SelectedIndex = SelectedIndex == index ? -1 : index;
        }
    }

    private void Redraw()
    {
        if (Cost == 0) return;
        Cost--;

        for (int i = 0; i < HandSize; i++)
        {
            CardInstance? card = Hand[i];
            if (card == null || card.Effects.Any(e => e.Type == EffectType.Preserve)) continue;
            deck.Add(card);
            Hand[i] = null;
        }

        ShuffleDeck();
        FillHand();
    }

    private void FillHand()
    {
        for (int i = 0; i < HandSize; i++)
        {
            if (Hand[i] == null)
                Hand[i] = DrawCard();
        }
    }

    private bool UseCardImmediately(int index)
    {
        if (index < 0 || index >= HandSize || Hand[index] == null) return false;
        if (!QueueCard(Hand[index]!)) return false;

        Hand[index] = null;
        if (SelectedIndex == index) SelectedIndex = -1;
        return true;
    }

    private bool QueueCard(CardInstance card)
    {
        // Unity는 큐가 가득 차 있어도 ResolveUse/Use를 먼저 실행해 버리는데, 그러면 카드가 손에 남은 채
        // 상태만 바뀐다. 여기서는 빈 칸이 있을 때만 진행한다.
        if (!Queue.Any(q => q == null)) return false;

        CardInstance preview = card.Clone();
        preview.ResolveUse(this, Queue, false);
        if (Cost < preview.GetCost()) return false;

        card.ResolveUse(this, Queue, true);
        card.Use();
        AddToQueue(card);
        card.EnterQueue(this);
        Cost -= card.GetCost();
        return true;
    }

    private void AddToQueue(CardInstance card)
    {
        for (int i = 0; i < QueueSize; i++)
        {
            if (Queue[i] != null) continue;
            Queue[i] = card;
            for (int j = i; j > 0; j--)
            {
                if (Queue[j]!.CooldownLeft < Queue[j - 1]!.CooldownLeft)
                    (Queue[j], Queue[j - 1]) = (Queue[j - 1], Queue[j]);
                else
                    break;
            }
            return;
        }
    }

    // =========================================================
    // Cards / Deck
    // =========================================================

    private CardInstance? DrawCard()
    {
        if (deck.Count == 0) return null;
        CardInstance card = deck[0];
        deck.RemoveAt(0);
        return card;
    }

    public void ReturnToDeck(CardInstance card) => deck.Add(card);

    private void PlayCard(CardInstance card)
    {
        Emit("play", card: card.Def.Name, ok: card.Effects.Any(e => e.Type == EffectType.Attack));
        card.Play(this);
        if (card.Effects.Any(e => e.Type == EffectType.Disposable)) return;
        ReturnToDeck(card);
    }

    // =========================================================
    // HP / Cost / Defense
    // =========================================================

    public void Attacked(int damage)
    {
        if (IsGuard) damage /= 2;
        Emit("damage", amount: damage, guarded: IsGuard);
        TakeDamage(damage);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        Health -= amount;
    }

    public void EnergyHeal(int amount) => Cost = Math.Clamp(Cost + amount, 0, MaxCost);

    public int GetDefense()
    {
        int total = 0;
        foreach (CardInstance? queued in Queue)
        {
            if (queued == null) continue;
            foreach (CardEffect cardEffect in queued.Effects)
                if (cardEffect.Type == EffectType.Defend)
                    total += queued.GetQueuedMagnitude(cardEffect);
        }
        return total;
    }

    public void ConsumeAllDefense()
    {
        foreach (CardInstance? queued in Queue)
        {
            if (queued == null) continue;
            foreach (CardEffect cardEffect in queued.Effects)
            {
                if (cardEffect.Type != EffectType.Defend) continue;
                int remaining = queued.GetQueuedMagnitude(cardEffect);
                if (remaining > 0) queued.ConsumeQueuedMagnitude(cardEffect, remaining);
            }
        }
    }

    private int ConsumeQueuedDefense(int incomingDamage)
    {
        int before = incomingDamage;
        foreach (CardInstance? queued in Queue)
        {
            if (queued == null || incomingDamage <= 0) continue;
            foreach (CardEffect cardEffect in queued.Effects)
            {
                if (incomingDamage <= 0) break;
                if (cardEffect.Type != EffectType.Defend) continue;
                incomingDamage -= queued.ConsumeQueuedMagnitude(cardEffect, incomingDamage);
            }
        }
        if (before > incomingDamage) Emit("shield", amount: before - incomingDamage);
        return incomingDamage;
    }

    // =========================================================
    // Effects
    // =========================================================

    public Effect[] GetEffectsByPriority() => effects.OrderByDescending(e => e.Priority).ToArray();

    public void AddEffect(Effect effect) => effects.Add(effect);

    public void RemoveEffect<T>() where T : Effect
    {
        int index = effects.FindIndex(e => e is T);
        if (index < 0) return;
        Effect effect = effects[index];
        effects.RemoveAt(index);
        effect.OnExpired(this);
    }

    public void RemoveEffectInstance(Effect effect)
    {
        if (effects.Remove(effect))
            effect.OnExpired(this);
    }

    public void ApplyEffect(CardEffect cardEffect)
    {
        foreach (Effect targetEffect in GetEffectsByPriority())
            targetEffect.OnAppliedOther(this, cardEffect, true);

        int magnitude = cardEffect.GetMagnitude();
        if (cardEffect.Type == EffectType.Attack)
            magnitude = ConsumeQueuedDefense(magnitude);

        Effect.Create(cardEffect.Type, magnitude).OnApply(this);
    }

    // =========================================================
    // Preview (Unity CardInstance.RefreshDisplay)
    // =========================================================

    /// <summary>지금 버프/디버프를 반영했을 때 이 카드의 코스트와 효과 수치.</summary>
    public (int cost, int[] magnitudes) Preview(CardInstance card)
    {
        CardInstance preview = card.Clone();
        preview.ResolveUse(this, Queue, false);

        var magnitudes = new int[preview.Effects.Count];
        for (int k = 0; k < preview.Effects.Count; k++)
        {
            CardEffect cardEffect = preview.Effects[k];
            cardEffect.Reset();
            Effect[] ownerEffects = GetEffectsByPriority();
            for (int i = ownerEffects.Length - 1; i >= 0; i--)
                ownerEffects[i].OnApplyingOther(this, cardEffect, false);
            Combatant target = cardEffect.GetTarget(this);
            foreach (Effect targetEffect in target.GetEffectsByPriority())
                targetEffect.OnAppliedOther(target, cardEffect, false);
            magnitudes[k] = cardEffect.GetMagnitude();
        }

        return (preview.GetCost(), magnitudes);
    }
}
