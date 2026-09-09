using System.Collections.Generic;
using UnityEngine;

public class CardInstance
{
    private CardDefinition _definition;
    private bool _isPlayed;
    private int _cooldownLeft; 
    private int _cooldown;
    private int _cost;
    private CardType cardType;
    private List<CardEffect> _effects;

    private CharacterManager _owner;
    private CardVisual _visual;
    // cost의 adder/multiplier — CardEffect의 adder/multiplier와 같은 역할이다.
    // OnUsingOther(actualUse가 false든 true든)가 큐에 있던 카드 쪽에서 "지금 사용되는 이 카드"의
    // 비용을 조정하고 싶을 때 이 값을 통해서 한다. ResolveUse가 매번 순회 전에 리셋한다.
    private float _costMultiplier = 1f;
    private int _costAdder;
    // effect will be added later

    // Continuous/Mix 효과가 큐에 머무는 동안 발휘하는 몫. CardEffect(정의 쪽 데이터, 같은
    // CardDefinition을 쓰는 다른 CardInstance와 공유됨)를 직접 깎으면 그 데이터가 오염되므로,
    // EnterQueue에서 카드별로 이 dictionary에 복사해두고 이후로는 이것만 읽고/깎는다.
    private readonly Dictionary<CardEffect, int> _queuedMagnitudes = new Dictionary<CardEffect, int>();

    public CardInstance(CardDefinition definition, CharacterManager owner)
    {
        _definition = definition;
        _owner = owner;
        RefreshInstance();
        _isPlayed = false;
    }

    // CardVisual은 자기 자신의 데이터를 갖지 않는다 — 이 카드가 화면에 어떻게 보일지는
    // 전부 CardInstance가 값을 넣어주는 방식으로 밀어넣는다(pull이 아니라 push).
    public CardVisual GetVisual() => _visual;

    // SetVisual 시점에는 아직 버프 상태를 반영한 미리보기를 계산할 필요가 없으므로(막 손에 들어온
    // 카드라 아직 아무 것도 반영할 게 없다) 정의된 그대로의 기본값만 밀어넣는다. 버프를 반영한
    // 값은 RefreshDisplay(매 턴 종료마다 호출)가 갱신한다.
    public void SetVisual(CardVisual visual)
    {
        _visual = visual;
        if (_visual == null) return;

        _visual.SetCardDefinition(_definition);
        RefreshCardVisual();
    }

    // CardVisual에 밀어넣는 값(비용/쿨다운/효과 표시)을 한 곳에서 갱신한다.
    // _effects는 같은 CardDefinition을 쓰는 다른 CardInstance와 CardEffect 객체를 공유하므로
    // (CardDefinition.GetEffects()가 매번 같은 CardEffect들을 담은 새 배열만 반환) 직전에 다른
    // 카드가 남겨둔 adder/multiplier가 남아있을 수 있다. 읽기 전에 항상 Reset()으로 지운다.
    private void RefreshCardVisual()
    {
        if (_visual == null) return;

        foreach (CardEffect cardEffect in _effects)
            cardEffect.Reset();

        _visual.SetCostText(GetCost().ToString());
        _visual.SetCooldownText(_cooldownLeft.ToString());
        _visual.RefreshEffectDisplays(_effects);
    }

    public void SetSelected(bool selected)
    {
        if (_visual != null) _visual.SetSelected(selected);
    }

    public void SetFace(bool front)
    {
        if (_visual != null) _visual.SetFace(front);
    }

    public void SetLayer(string sortingLayerName)
    {
        if (_visual != null) _visual.SetLayer(sortingLayerName);
    }

    public void SetSize(Vector2 targetSize)
    {
        if (_visual != null) _visual.SetSize(targetSize);
    }

    public void MoveTo(Vector3 targetPosition, float duration = 0.3f)
    {
        if (_visual != null) _visual.MoveTo(targetPosition, duration);
    }

    public Vector2 GetBackgroundSize() => _visual != null ? _visual.GetBackgroundSize() : Vector2.zero;

    private void RefreshInstance()
    {
        _cooldown =  _definition.GetCooldown();
        _cooldownLeft = _definition.GetCooldown();
        _cost  = _definition.GetCost();
        _effects = new List<CardEffect>();
        foreach (CardEffect cardEffect in _definition.GetEffects())
        {
            _effects.Add(cardEffect);
        }
    }
    public CardDefinition GetDefinition() => _definition;
    public List<CardEffect> GetEffects() => _effects;
    // CardEffect.GetMagnitude()와 같은 방식(adder/multiplier 적용)으로 계산한다.
    public int GetCost() => Mathf.FloorToInt(_cost * _costMultiplier) + _costAdder;
    public int GetCooldown() => _cooldown;
    public bool GetIsPlayed() => _isPlayed;
    public int GetCooldownLeft() => _cooldownLeft;
    public bool IsReady() => _cooldownLeft <= 0;

    public void AddCost(int amount) => _costAdder += amount;
    public  void ChangeCooldown(int amount)
    {
        int originalCooldown = _cooldown;
        _cooldown += amount;
        if (_cooldown <= 0) _cooldown = 1;
        amount = _cooldown - originalCooldown;
        if(!_isPlayed)
        {
            _cooldownLeft += amount;
            if (_cooldownLeft <= 0) _cooldownLeft = 1;
        }

    }

    public void ChangeCooldownLeft(int amount)
    {
        _cooldownLeft += amount;
        if (_cooldownLeft <= 0) _cooldownLeft = _isPlayed ? 0:1;
    }

    public void SetCooldownLeft(int value)
    {
        _cooldownLeft = value;
        RefreshCardVisual();
    }
    public void MultiplyCost(float amount) => _costMultiplier *= amount;

    public void Use()
    {
        _isPlayed = true;
        RefreshCardVisual();
    }

    public void TickCooldown(int tick=1)
    {
            _cooldownLeft-=tick;
            RefreshCardVisual();
    }

    public void Play(CharacterManager characterManager)
    {
        _cooldownLeft = GetCooldown();
        Debug.Log($"[{characterManager.gameObject.name}] Activated card: {_definition.name}");

        SoundManager.Instance?.Play(EffectSound.PlayCard);
        if (_effects.Exists(e => e.GetEffect().GetEffectType() == EffectType.Attack))
            SoundManager.Instance?.Play(EffectSound.Attack);

        // 큐를 떠나는 시점이므로 먼저 Continuous/Mix 효과가 큐에 머무는 동안 부여했던 몫을 되돌린다.
        ExitQueue(characterManager);

        foreach (CardEffect cardEffect in _effects)
        {
            // Instant 비트가 없는(순수 Continuous인) 효과는 큐에 머무는 동안 이미 발휘를 마쳤으므로
            // (ExitQueue) 여기서 다시 발동하지 않는다. Instant 비트가 있으면(Instant든, 두 비트를
            // 다 켠 카드별 Mix든) 카드가 다 됐을 때의 즉발 파이프라인을 탄다.
            if (!cardEffect.GetAppliedCategory().HasFlag(EffectCategory.Instant)) continue;

            cardEffect.Reset();
            Effect[] ownerEffects = characterManager.GetEffectPrioritize();
            for (int i = ownerEffects.Length - 1; i >= 0; i--)
                ownerEffects[i].OnApplyingOther(characterManager, cardEffect, true);
            CharacterManager resolved = cardEffect.GetTarget(characterManager);
            resolved.ApplyEffect(cardEffect);
        }
    }

    // 카드별 큐 잔여 magnitude 조회/소모. Defend처럼 큐에 머무는 동안 다른 시스템(공격 상쇄 등)이
    // magnitude를 직접 깎아써야 하는 Continuous 효과를 위한 것이다.
    public int GetQueuedMagnitude(CardEffect cardEffect) =>
        _queuedMagnitudes.TryGetValue(cardEffect, out int value) ? value : 0;

    public int ConsumeQueuedMagnitude(CardEffect cardEffect, int amount)
    {
        int available = GetQueuedMagnitude(cardEffect);
        int consumed = Mathf.Min(available, amount);
        if (consumed > 0) _queuedMagnitudes[cardEffect] = available - consumed;
        return consumed;
    }

    // 큐에 들어가는 순간(CharacterManager.QueueCard 성공 직후) 호출된다. Continuous 비트가 없는
    // 효과는 건너뛰고, 있는 효과만 그 시점의 버프(OnApplyingOther)를 반영한 magnitude로 OnEnterQueue를
    // 실행한 뒤, 실제로 적용된(예: Burning처럼 OnApply가 자체 조정한) magnitude를 _queuedMagnitudes에
    // 등록한다 — ExitQueue가 나중에 정확히 그만큼만 되돌릴 수 있어야 하기 때문이다.
    public void EnterQueue(CharacterManager owner)
    {
        foreach (CardEffect cardEffect in _effects)
        {
            if (!cardEffect.GetAppliedCategory().HasFlag(EffectCategory.Continuous)) continue;

            cardEffect.Reset();
            Effect[] ownerEffects = owner.GetEffectPrioritize();
            for (int i = ownerEffects.Length - 1; i >= 0; i--)
                ownerEffects[i].OnApplyingOther(owner, cardEffect, true);

            Effect definitionEffect = cardEffect.GetEffect();
            Effect runtimeEffect = Effect.Create(definitionEffect.GetEffectType(), cardEffect.GetMagnitude());
            runtimeEffect.OnEnterQueue(owner, this, cardEffect);

            _queuedMagnitudes[cardEffect] = runtimeEffect.GetMagnitude();
        }
    }

    // 큐를 떠날 때(Play() 참고) 호출되어 EnterQueue가 등록한 몫을 되돌린다. Defend처럼 OnExitQueue를
    // 비워둔 효과는 여기서 아무 것도 하지 않는다(방어 소모는 CharacterManager가 직접 처리).
    public void ExitQueue(CharacterManager owner)
    {
        foreach (var pair in _queuedMagnitudes)
        {
            Effect definitionEffect = pair.Key.GetEffect();
            Effect runtimeEffect = Effect.Create(definitionEffect.GetEffectType(), pair.Value);
            runtimeEffect.OnExitQueue(owner, this, pair.Key, pair.Value);
        }
        _queuedMagnitudes.Clear();
    }

    // subject: 이 카드를 쓰는 주체. queuedCards: 현재 큐에 있는 카드들(OnUsingOther가 반응할 대상).
    // actualUse가 true면 실제 사용 시점(카드가 손패에서 큐로 넘어가기 전, 비용 체크 직전)의 호출이고,
    // false면 시각화(비용 표시) 갱신용 미리보기 호출이다. cost adder/multiplier는 매 호출마다 리셋한다.
    public void ResolveUse(CharacterManager subject, IEnumerable<CardInstance> queuedCards, bool actualUse)
    {
        _costMultiplier = 1f;
        _costAdder = 0;
        if(actualUse)
        {
            foreach (CardEffect cardEffect in _effects)
                ResolveEffect(cardEffect).OnUse(subject, this, actualUse);
        }

        if (queuedCards == null) return;
        foreach (CardInstance queued in queuedCards)
        {
            if (queued == null) continue;
            foreach (CardEffect cardEffect in queued.GetEffects())
                ResolveEffect(cardEffect).OnUsingOther(subject, this, actualUse);
        }
    }

    // cardEffect.GetEffect()는 CardDefinition 애셋에 그대로 저장된 authoring용 Effect라
    // (SerializeReference가 아니라 평범한 SerializeField라) 서브클래스로 역직렬화되지 않고 항상
    // base Effect로 들어온다. OnUse/OnUsingOther처럼 서브클래스 오버라이드(DivideCooldown,
    // DefenseToCooldown, CostToCooldown, CooldownToCost 등)에 실제 동작이 있는 훅은 Effect.Create로
    // 매번 진짜 런타임 서브클래스를 새로 만들어 호출해야 한다.
    private static Effect ResolveEffect(CardEffect cardEffect) =>
        Effect.Create(cardEffect.GetEffect().GetEffectType(), cardEffect.GetEffect().GetMagnitude());

    // 매 턴 종료마다 호출되어, 현재 버프 상태(subject의 OnApplyingOther / 대상의 OnAppliedOther)를
    // 반영한 비용·효과 수치로 시각화를 다시 계산한다. 원본 데이터가 오염되지 않도록 복제본에서
    // 계산하고(ResolveUse도 false로 순회), 계산이 끝난 결과값만 실제 CardVisual에 밀어넣는다.
    public void RefreshDisplay(CharacterManager subject, IEnumerable<CardInstance> queuedCards)
    {
        if (_visual == null || subject == null) return;

        CardInstance preview = Clone();
        preview.ResolveUse(subject, queuedCards, false);

        foreach (CardEffect cardEffect in preview._effects)
        {
            cardEffect.Reset();
            Effect[] ownerEffects = subject.GetEffectPrioritize();
            for (int i = ownerEffects.Length - 1; i >= 0; i--)
                ownerEffects[i].OnApplyingOther(subject, cardEffect, false);

            CharacterManager target = cardEffect.GetTarget(subject);
            Effect[] targetEffects = target.GetEffectPrioritize();
            foreach (Effect targetEffect in targetEffects)
                targetEffect.OnAppliedOther(target, cardEffect, false);
        }

        _visual.SetCostText(preview.GetCost().ToString());
        _visual.SetCooldownText(_cooldownLeft.ToString());
        _visual.RefreshEffectDisplays(preview._effects);
        _visual.SetUnplayable(preview.GetCost() > subject.GetCost());
    }

    // OnUse/OnUsingOther/OnApplyingOther/OnAppliedOther를 actualUse=false로 순회할 때 쓰는 복제본.
    // 같은 CardDefinition을 쓰는 다른 CardInstance와 CardEffect 객체를 공유하지 않도록 새로 감싸서,
    // 이 복제본에 대한 Reset()/Add()/Multiply()가 원본이나 다른 카드에 영향을 주지 않게 한다.
    public CardInstance Clone()
    {
        CardInstance clone = new CardInstance(_definition, _owner)
        {
            _isPlayed = _isPlayed,
            _cooldownLeft = _cooldownLeft,
            _cost = _cost,
            _cooldown = _cooldown,
        };
        clone._effects = new List<CardEffect>(_effects.Count);
        foreach (CardEffect cardEffect in _effects)
            clone._effects.Add(new CardEffect(cardEffect.GetEffect(), cardEffect.GetEffectTarget()));
        return clone;
    }
}
