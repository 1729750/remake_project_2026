namespace CardGameServer.Game;

/// <summary>
/// Unity CardEffect: 카드 위의 효과 하나 + 이번 적용에서 쌓인 adder/multiplier.
/// Unity에서는 같은 CardDefinition을 쓰는 카드끼리 이 객체를 공유했지만, 여기서는 카드마다 따로 만든다.
/// </summary>
public sealed class CardEffect
{
    private float multiplier = 1f;
    private int adder;

    public CardEffectDef Def { get; }
    public EffectType Type => Def.Type;

    public CardEffect(CardEffectDef def)
    {
        Def = def;
    }

    public void Reset()
    {
        multiplier = 1f;
        adder = 0;
    }

    public void Add(int amount) => adder += amount;
    public void Multiply(float amount) => multiplier *= amount;

    public int GetMagnitude() => (int)Math.Floor(Def.Magnitude * multiplier) + adder;

    public EffectCategory GetAppliedCategory()
    {
        if (Def.AppliedCategory != 0) return Def.AppliedCategory;
        EffectCategory supported = Effect.SupportedCategoriesOf(Def.Type);
        return supported.HasFlag(EffectCategory.Instant) ? EffectCategory.Instant : supported;
    }

    public Combatant GetTarget(Combatant user) => Def.Target == EffectTarget.User ? user : user.Opponent;

    /// <summary>OnUse/OnUsingOther처럼 서브클래스 동작이 필요한 훅은 매번 새 런타임 Effect로 호출한다.</summary>
    public Effect CreateRuntime() => Effect.Create(Def.Type, Def.Magnitude);
}

/// <summary>Unity CardInstance에서 화면 관련 코드를 뺀 것.</summary>
public sealed class CardInstance
{
    private static int nextId;

    private float costMultiplier = 1f;
    private int costAdder;
    private readonly Dictionary<CardEffect, int> queuedMagnitudes = new();

    /// <summary>클라이언트가 같은 카드를 손패→큐로 따라가며 움직일 수 있게 주는 고유 번호.</summary>
    public int Id { get; } = Interlocked.Increment(ref nextId);
    public CardDef Def { get; }
    public Combatant Owner { get; }
    public List<CardEffect> Effects { get; private set; }
    public bool IsPlayed { get; private set; }
    public int Cooldown { get; private set; }
    public int CooldownLeft { get; private set; }
    private int cost;

    public bool IsContinuousCard => Def.CardType == CardType.Continuous;
    public bool IsReady => CooldownLeft <= 0;

    public CardInstance(CardDef def, Combatant owner)
    {
        Def = def;
        Owner = owner;
        Cooldown = def.Cooldown;
        CooldownLeft = def.Cooldown;
        cost = def.Cost;
        Effects = def.Effects.Select(e => new CardEffect(e)).ToList();
    }

    public int GetCost() => (int)Math.Floor(cost * costMultiplier) + costAdder;
    public void AddCost(int amount) => costAdder += amount;

    public void ChangeCooldownLeft(int amount)
    {
        CooldownLeft += amount;
        if (CooldownLeft <= 0) CooldownLeft = IsPlayed ? 0 : 1;
    }

    public void SetCooldownLeft(int value) => CooldownLeft = value;
    public void Use() => IsPlayed = true;
    public void TickCooldown(int tick) => CooldownLeft -= tick;

    public void Play(Combatant owner)
    {
        CooldownLeft = Cooldown;
        ExitQueue(owner);

        foreach (CardEffect cardEffect in Effects)
        {
            if (!cardEffect.GetAppliedCategory().HasFlag(EffectCategory.Instant)) continue;
            cardEffect.Reset();
            Effect[] ownerEffects = owner.GetEffectsByPriority();
            for (int i = ownerEffects.Length - 1; i >= 0; i--)
                ownerEffects[i].OnApplyingOther(owner, cardEffect, true);
            cardEffect.GetTarget(owner).ApplyEffect(cardEffect);
        }
    }

    public int GetQueuedMagnitude(CardEffect cardEffect) =>
        queuedMagnitudes.TryGetValue(cardEffect, out int value) ? value : 0;

    public int ConsumeQueuedMagnitude(CardEffect cardEffect, int amount)
    {
        int available = GetQueuedMagnitude(cardEffect);
        int consumed = Math.Min(available, amount);
        if (consumed > 0) queuedMagnitudes[cardEffect] = available - consumed;
        return consumed;
    }

    public void EnterQueue(Combatant owner)
    {
        foreach (CardEffect cardEffect in Effects)
        {
            if (!cardEffect.GetAppliedCategory().HasFlag(EffectCategory.Continuous)) continue;
            cardEffect.Reset();
            Effect[] ownerEffects = owner.GetEffectsByPriority();
            for (int i = ownerEffects.Length - 1; i >= 0; i--)
                ownerEffects[i].OnApplyingOther(owner, cardEffect, true);
            Effect runtime = Effect.Create(cardEffect.Type, cardEffect.GetMagnitude());
            runtime.OnEnterQueue(owner, this, cardEffect);
            queuedMagnitudes[cardEffect] = runtime.Magnitude;
        }
    }

    public void ExitQueue(Combatant owner)
    {
        foreach (var (cardEffect, granted) in queuedMagnitudes.ToList())
            Effect.Create(cardEffect.Type, granted).OnExitQueue(owner, this, cardEffect, granted);
        queuedMagnitudes.Clear();
    }

    public void ResolveUse(Combatant subject, IEnumerable<CardInstance?> queuedCards, bool actualUse)
    {
        costMultiplier = 1f;
        costAdder = 0;

        if (actualUse)
        {
            foreach (CardEffect cardEffect in Effects)
                cardEffect.CreateRuntime().OnUse(subject, this, actualUse);
        }

        foreach (CardInstance? queued in queuedCards)
        {
            if (queued == null) continue;
            foreach (CardEffect cardEffect in queued.Effects)
                cardEffect.CreateRuntime().OnUsingOther(subject, this, actualUse);
        }
    }

    /// <summary>미리보기 계산용 복제본. 원본 상태를 건드리지 않는다.</summary>
    public CardInstance Clone()
    {
        var clone = new CardInstance(Def, Owner)
        {
            IsPlayed = IsPlayed,
            CooldownLeft = CooldownLeft,
            Cooldown = Cooldown,
        };
        clone.cost = cost;
        return clone;
    }
}
