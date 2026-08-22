using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class CharacterManager: MonoBehaviour
{
    // SelectCard의 특수 행동 인덱스 (카드 선택과 상호배타 — 턴 종료 시 하나만 실행된다)
    public const int RedrawAction = -2;
    public const int DefenseAction = -1;
    private int _maxHealth = 100;
    private int _specialAction; // 0 = 없음, RedrawAction/DefenseAction
    private List<CardInstance> _deck;
    
    private int _health;
    private int _defense;
    private int _cost;
    private const int MaxCost = 15;
    private List<Effect> _effects;
    [SerializeField] private CardDefinition[] startDeck;
    [SerializeField] private GameObject queueRoots;
    [SerializeField] private bool isHandVisualized;
    [SerializeField] private GameObject handsRoot;
    [SerializeField] private Transform hpBar;
    [SerializeField] private bool shrinkRight = true;
    [SerializeField] private bool playerControlled = true;
    [SerializeField] private TextMeshPro costText;
    [SerializeField] private RectTransform effectList;
    [SerializeField] private TextMeshPro defenseText;
    [SerializeField] private GameObject defenseIndicator;
    [SerializeField] private Transform[] defenseIndicatorRestPosition;
    [SerializeField] private float defenseIndicatorMoveDuration = 0.3f;
    private readonly List<EffectDisplay> _effectDisplays = new List<EffectDisplay>();
    private HandManager _handManager;
    private QueueManager _queueManager;
    private Coroutine _defenseIndicatorCoroutine;

    private bool _isGuard = false;
    public bool GetIsGuard() => _isGuard;
    
    private int tickSpeed=1;

    public void ChangeTickSpeed(int delta)
    {
        tickSpeed += delta;
    }
    public void CharacterInit(CardDefinition[] deck, int maxHealth)
    {
        _maxHealth=maxHealth;
        startDeck = deck;
        _isGuard = false;
        tickSpeed=1;
        Clear();

        //덱 생성
        if (startDeck != null)
            foreach (var def in startDeck)
                _deck.Add(new CardInstance(def,this));
        
        //덱 셔플
        ShuffleDeck();

        //손 채우기
        _handManager.FillHand();
        
        //(만약 플레이어라면) 조작키 할당
        if (playerControlled)
        {
            
            PlayerInputManager.Instance.Load("Battle", new Dictionary<string, Action>
            {
                ["PlayCard1"] = () => SelectCard(0),
                ["PlayCard2"] = () => SelectCard(1),
                ["PlayCard3"] = () => SelectCard(2),
                ["PlayCard4"] = () => SelectCard(3),
                ["ReDraw"]    = () => SelectCard(CharacterManager.RedrawAction),
                ["Defense"]   = () => SelectCard(CharacterManager.DefenseAction),
            });
        }
    }

    private void ShuffleDeck()
    {
        for (int i = _deck.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
        }
    }
    
    private void Update()
    {
        UpdateCostDisplay();
        if (!playerControlled)
            UpdateAi();
    }

    public void OnTurnStart()
    {
        if (_handManager == null) return;
        for (int i = _effects.Count - 1; i >= 0; i--)
            _effects[i].OnTurnStarted(this);
        int healEnergyAmount = 2;
        if (_isGuard) healEnergyAmount--;
        if (_cost >= 10) healEnergyAmount--;
        EnergyHeal(healEnergyAmount);
        UpdateEffectList();
        // healEnergy/effect 처리가 끝난 직후, 지난 턴에 코스트/큐가 부족해 select된 채로 남아있던
        // 카드가 있으면 강제로 재시도한다 — 여전히 안 되면 HandManager.UseCard가 자연히 실패하고
        // select 상태만 남는다.
        _handManager.UseCard();
        _handManager.FillHand();
        PlayUpcomingAttackSoundIfNeeded();
        // 이번 턴 시작 로직이 전부 끝난 뒤(코스트 회복 등 반영 완료 시점) preview cost/cooldown을
        // 다시 계산해, 지금 코스트로 낼 수 없는 손패 카드에 Unplayable 오버레이를 켠다.
        _handManager.RefreshHandDisplay();
    }

    // 상대 큐에 이번 턴 종료 시 발동될(cooldownLeft가 1 이하인) Attack 효과 카드가 있으면 경고음을 재생한다.
    // 전투가 이미 끝난 상태(BattleFinish)라면 재생하지 않는다 — OnTurnStart는 보통 그 전에 걸러지지만,
    // BattleManager.Instance나 CurrentState를 통해 한 번 더 방어적으로 확인한다.
    private void PlayUpcomingAttackSoundIfNeeded()
    {
        if (!playerControlled) return;
        if (SoundManager.Instance == null || BattleManager.Instance == null) return;
        if (BattleManager.Instance.CurrentState == BattleState.BattleFinish) return;

        CharacterManager opponent = BattleManager.Instance.GetOpponent(this);
        if (opponent == null) return;

        foreach (CardInstance queued in opponent.GetQueue())
        {
            if (queued == null || queued.GetCooldownLeft() > 1) continue;

            foreach (CardEffect cardEffect in queued.GetEffects())
            {
                if (cardEffect.GetEffect().GetEffectType() == EffectType.Attack)
                {
                    SoundManager.Instance.Play(EffectSound.UpcomingAttack);
                    return;
                }
            }
        }
    }

    public void OnTurnEnd()
    {
        _queueManager.TickQueueCards(this);
        for (int i = _effects.Count - 1; i >= 0; i--)
            _effects[i].OnTurnEnded(this);
        // 플레이어는 select된 카드가 있어도 여기서 건드리지 않는다 — 다음 OnTurnStart의
        // 재시도로 넘긴다. AI는 이제 Update()가 턴 내내 매 프레임 판단해서 곧장 사용하므로
        // (UpdateAi 참고) 턴 종료 시점에 따로 뽑아 쓸 필요가 없다.
        _specialAction = 0;
        // 매 턴 종료마다 손패 카드들의 비용/효과 표시를 이번 턴에 바뀐 버프 상태에 맞게 다시 계산한다.
        _handManager.RefreshHandDisplay();
    }

    // ReDraw: 손패 전부를 덱에 되돌리고 셔플 후 다시 채운다
    private void Redraw()
    {
        if (_cost == 0) return;
        _cost--;
        _handManager.ReturnHandToDeck();
        ShuffleDeck();
        _handManager.FillHand();
        Debug.Log($"[{gameObject.name}] Redrew hand");
    }

    // defenseIndicator: Defense 액션이 선택되면 defenseIndicatorRestPosition[1]로,
    // 아니면(다른 선택/턴 종료) [0]으로 Lerp를 이용해 부드럽게 이동한 뒤, [0]에 도달하면 비활성화된다.
    private void SetDefenseIndicatorActive(bool active)
    {
        if (defenseIndicator == null || defenseIndicatorRestPosition == null) return;

        int targetIndex = active ? 1 : 0;
        if (targetIndex >= defenseIndicatorRestPosition.Length || defenseIndicatorRestPosition[targetIndex] == null) return;

        if (!active && !defenseIndicator.activeSelf) return;

        if (_defenseIndicatorCoroutine != null)
            StopCoroutine(_defenseIndicatorCoroutine);

        if (active)
            defenseIndicator.SetActive(true);

        _defenseIndicatorCoroutine = StartCoroutine(MoveDefenseIndicator(defenseIndicatorRestPosition[targetIndex].position, !active));
    }

    private IEnumerator MoveDefenseIndicator(Vector3 targetPosition, bool deactivateOnComplete)
    {
        Transform indicatorTransform = defenseIndicator.transform;
        Vector3 startPosition = indicatorTransform.position;
        float elapsed = 0f;

        while (elapsed < defenseIndicatorMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / defenseIndicatorMoveDuration));
            indicatorTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        indicatorTransform.position = targetPosition;
        if (deactivateOnComplete)
            defenseIndicator.SetActive(false);
        _defenseIndicatorCoroutine = null;
    }

    public void SelectCard(int index)
    {
        if (BattleManager.Instance == null || BattleManager.Instance.CurrentState != BattleState.Turn) return;
        if (index == RedrawAction || index == DefenseAction)
        {
            _specialAction = index;
            _handManager.UnselectCard();
            Debug.Log($"[{gameObject.name}] Selected action: {(index == RedrawAction ? "ReDraw" : "Defense")}");
            if (index == RedrawAction)
            {
                Redraw();
            }
            else if (index == DefenseAction)
            {
                _isGuard = !_isGuard;
            }
            SetDefenseIndicatorActive(_isGuard);
            return;
        }

        _isGuard = false;
        var hand = _handManager.GetHand();
        if (index < 0 || index >= hand.Length || hand[index] == null) return;
        _specialAction = 0;
        SetDefenseIndicatorActive(false);

        string cardName = hand[index].GetDefinition().name;
        if (_handManager.UseCardImmediately(index))
        {
            // 지금 바로 낼 수 있었으면(코스트/큐 여유 있음) select를 거치지 않고 곧장 사용된다.
            Debug.Log($"[{gameObject.name}] Used card: {cardName}");
        }
        else
        {
            // 지금은 낼 수 없으면(QueueCard가 실패) select만 해두고, 다음 OnTurnStart 재시도로 넘긴다.
            _handManager.SelectCard(index);
            Debug.Log($"[{gameObject.name}] Selected card: {cardName}");
        }
    }

    // AI(!playerControlled) 전용 카드 판단. 매 Update마다(턴 중이고 큐에 자리가 있을 때만) 자신의
    // 손패와 상대 큐 상태를 보고 지금 낼 가치가 있는 카드가 있으면 곧장 사용한다 — 상대가 턴
    // 도중에 카드를 새로 내면(예: 공격을 큐에 올리면) 그 다음 프레임부터 바로 반영해서 반응한다.
    // select를 거치지 않고 바로 UseCardImmediately를 쓰는 이유: 여기서 고르는 카드는 이미
    // "지금 코스트로 낼 수 있는지" preview까지 확인한 뒤라 실패할 일이 거의 없고, select/unselect
    // 사운드가 매 프레임 반복 재생되는 것도 피할 수 있다.
    private void UpdateAi()
    {
        if (BattleManager.Instance == null || BattleManager.Instance.CurrentState != BattleState.Turn) return;
        if (!_queueManager.HasFreeSlot()) return;

        int bestIndex = ChooseBestCardIndex();
        if (bestIndex < 0) return;

        CardInstance card = _handManager.GetHand()[bestIndex];
        string cardName = card.GetDefinition().name;
        if (_handManager.UseCardImmediately(bestIndex))
            Debug.Log($"[{gameObject.name}] AI used card: {cardName}");
    }

    // 지금 코스트로 낼 수 있는 손패 카드들 중 ScoreCard가 가장 높게 평가한 카드의 인덱스를 고른다.
    // 점수가 0 이하(도움이 안 되거나 자충수인 카드밖에 없음)면 아무것도 내지 않고 다음 기회로 미룬다.
    private int ChooseBestCardIndex()
    {
        CardInstance[] hand = _handManager.GetHand();
        int bestIndex = -1;
        float bestScore = 0f;

        for (int i = 0; i < hand.Length; i++)
        {
            CardInstance card = hand[i];
            if (card == null) continue;
            if (PreviewCost(card) > _cost) continue;

            float score = ScoreCard(card);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    // QueueCard가 실제로 쓰는 것과 같은 preview 경로(버프로 바뀐 실제 코스트)로 계산한다 —
    // 원본 cost만 보면 EnergyDrain/CostToCooldown류로 실제 코스트가 달라졌을 때 판단이 어긋난다.
    private int PreviewCost(CardInstance card)
    {
        CardInstance preview = card.Clone();
        preview.ResolveUse(this, _queueManager.GetQueue(), false);
        return preview.GetCost();
    }

    // 카드의 각 CardEffect를 CardInstance.RefreshDisplay와 동일한 경로로 미리 계산해(현재 버프
    // 반영한 최종 magnitude) 점수를 합산한다.
    private float ScoreCard(CardInstance card)
    {
        if (BattleManager.Instance == null) return 0f;
        CharacterManager opponent = BattleManager.Instance.GetOpponent(this);
        if (opponent == null) return 0f;

        CardInstance preview = card.Clone();
        preview.ResolveUse(this, _queueManager.GetQueue(), false);

        float total = 0f;
        foreach (CardEffect cardEffect in preview.GetEffects())
        {
            cardEffect.Reset();
            Effect[] ownerEffects = GetEffectPrioritize();
            for (int i = ownerEffects.Length - 1; i >= 0; i--)
                ownerEffects[i].OnApplyingOther(this, cardEffect, false);

            CharacterManager target = cardEffect.GetTarget(this);
            Effect[] targetEffects = target.GetEffects();
            for (int i = targetEffects.Length - 1; i >= 0; i--)
                targetEffects[i].OnAppliedOther(target, cardEffect, false);

            total += ScoreCardEffect(cardEffect, target, opponent);
        }
        return total;
    }

    // CardEffect 하나가 실제로 적용됐을 때 이 캐릭터(AI) 입장에서 얼마나 가치 있는지 점수를 매긴다.
    // 방향(자신에게 좋은지/나쁜지)은 Effect.TargetPolarity + 실제 target이 자신인지로 정하고,
    // 크기는 타입별 가중치(및 상황: 상대의 임박한 공격, 자신의 공격 수단 보유 여부)로 정한다.
    private float ScoreCardEffect(CardEffect cardEffect, CharacterManager target, CharacterManager opponent)
    {
        EffectType type = cardEffect.GetEffect().GetEffectType();
        int magnitude = cardEffect.GetMagnitude();
        bool targetsSelf = target == this;

        // Attack은 즉발 데미지라 처치 가능 여부까지 바로 계산할 수 있어 별도로 다룬다.
        if (type == EffectType.Attack)
        {
            float value = magnitude;
            if (magnitude >= target.GetDefense() + target.GetHealth())
                value += 40f; // 지금 내면 상대를 처치할 수 있으면 최우선으로 취급한다.
            return targetsSelf ? -value : value;
        }

        bool opponentAttackIncoming = OpponentHasImminentAttack(opponent);
        bool haveAttackPressure = HasAttackPressure();
        float importance = MagnitudeImportance(type, magnitude, opponentAttackIncoming, haveAttackPressure);

        Effect runtimeEffect = Effect.Create(type, magnitude);
        float sign = runtimeEffect.TargetPolarity switch
        {
            EffectTargetPolarity.Positive => targetsSelf ? 1f : -1f,
            EffectTargetPolarity.Negative => targetsSelf ? -1f : 1f,
            _ => 1f,
        };

        return importance * sign;
    }

    // 타입별 magnitude 1당 가중치. 상황(상대의 공격이 임박했는지/내가 공격 수단을 갖고 있는지)에
    // 따라 방어형·시너지형 효과의 가치를 올린다.
    private static float MagnitudeImportance(EffectType type, int magnitude, bool opponentAttackIncoming, bool haveAttackPressure)
    {
        switch (type)
        {
            case EffectType.Defend:        return magnitude * (opponentAttackIncoming ? 1.5f : 0.7f);
            case EffectType.Guard:         return magnitude * (opponentAttackIncoming ? 6f : 1.5f);
            case EffectType.Vulnerable:    return magnitude * (haveAttackPressure ? 8f : 2.5f);
            case EffectType.Weak:          return magnitude * (opponentAttackIncoming ? 8f : 2.5f);
            case EffectType.PoseBreak:     return magnitude * 6f;
            case EffectType.Strength:      return magnitude * (haveAttackPressure ? 6f : 2f);
            case EffectType.Harden:        return magnitude * 3f;
            case EffectType.EnergyHeal:    return magnitude * 3f;
            case EffectType.EnergyDrain:   return magnitude * 4f;
            case EffectType.Burning:       return magnitude * 3f;
            case EffectType.AddDump:       return magnitude * 2f;
            case EffectType.Quicker:       return magnitude * 4f;
            case EffectType.TimeSkip:      return magnitude * 3f;
            // Preserve/Disposable/CostToCooldown/CooldownToCost/DivideCooldown/DefenseToCooldown처럼
            // 세부 평가를 만들지 않은 나머지 타입 — 완전히 무시하지는 않도록 소폭의 기본 점수만 준다.
            default:                       return Mathf.Max(magnitude, 1) * 2f;
        }
    }

    // 상대 큐에 곧(2턴 이내) 발동될 Attack 카드가 있는지. PlayUpcomingAttackSoundIfNeeded와 같은
    // 패턴이지만, 방어 카드 가치 판단에 쓰기 위해 여유를 좀 더 둔다(cooldownLeft <= 2).
    private static bool OpponentHasImminentAttack(CharacterManager opponent)
    {
        foreach (CardInstance queued in opponent.GetQueue())
        {
            if (queued == null || queued.GetCooldownLeft() > 2) continue;
            foreach (CardEffect cardEffect in queued.GetEffects())
                if (cardEffect.GetEffect().GetEffectType() == EffectType.Attack)
                    return true;
        }
        return false;
    }

    // 자신의 큐/손패에 Attack 카드가 있는지 — Strength/Vulnerable처럼 공격력에 얹는 효과가
    // 지금 당장 써먹을 데가 있는지 판단하는 데 쓴다.
    private bool HasAttackPressure()
    {
        foreach (CardInstance queued in _queueManager.GetQueue())
        {
            if (queued == null) continue;
            foreach (CardEffect cardEffect in queued.GetEffects())
                if (cardEffect.GetEffect().GetEffectType() == EffectType.Attack)
                    return true;
        }
        foreach (CardInstance card in _handManager.GetHand())
        {
            if (card == null) continue;
            foreach (CardEffect cardEffect in card.GetEffects())
                if (cardEffect.GetEffect().GetEffectType() == EffectType.Attack)
                    return true;
        }
        return false;
    }

    // 전투 종료 시(BattleManager.NotifyDefeat) 손패/큐를 덱으로 되돌리지 않고 그대로 비운다
    // (다음 전투는 CharacterInit이 덱 자체를 새로 만들기 때문에 되돌릴 필요가 없다).
    public void ClearHandAndQueue()
    {
        _handManager?.ClearHand();
        _queueManager?.ClearQueue();
    }

    public int getmaxHealth() => _maxHealth;
    public int GetHealth() => _health;
    public int GetDefense() => _defense;
    public int GetCost() => _cost;
    public Effect[] GetEffects() => _effects.ToArray();
    public Effect[] GetEffectPrioritize() => _effects.OrderByDescending(e => e.GetEffectPriority()).ToArray();

    public void Init()
    {
        _effects = new List<Effect>();
        _deck = new List<CardInstance>();

        _queueManager = new QueueManager(queueRoots);
        _handManager = new HandManager(this, handsRoot, isHandVisualized);

        Clear();
    }

    // CharacterInit(전투 시작마다 호출되어 같은 CharacterManager를 재사용)에서도 불리므로,
    // 데이터(_effects/_health/_defense/_cost)뿐 아니라 그걸 반영하는 시각 요소(HP바, 방어도 표시,
    // effectList 아이콘, defenseIndicator)까지 전부 이전 전투의 흔적 없이 리셋해야 한다.
    public void Clear()
    {
        _effects.Clear();
        _deck = new List<CardInstance>();
        _health = _maxHealth;
        _defense = 0;
        _cost = 0;
        UpdateHPBar();
        UpdateDefenseDisplay();
        UpdateEffectList();
        ResetDefenseIndicator();
    }

    // defenseIndicator를 비활성 상태로, rest position[0]으로 되돌린다. 진행 중이던 이동 코루틴이
    // 있으면 중단한다(순간 리셋이라 Lerp로 움직일 필요가 없다).
    private void ResetDefenseIndicator()
    {
        if (defenseIndicator == null || defenseIndicatorRestPosition == null
            || defenseIndicatorRestPosition.Length == 0 || defenseIndicatorRestPosition[0] == null)
            return;

        if (_defenseIndicatorCoroutine != null)
        {
            StopCoroutine(_defenseIndicatorCoroutine);
            _defenseIndicatorCoroutine = null;
        }

        defenseIndicator.transform.position = defenseIndicatorRestPosition[0].position;
        defenseIndicator.SetActive(false);
    }

    private void UpdateHPBar()
    {
        if (hpBar == null) return;
        float ratio = (float)_health / _maxHealth;
        if (ratio < 0f) ratio = 0f;
        float offset = shrinkRight ? (ratio - 1f) * 0.5f : (1f - ratio) * 0.5f;
        hpBar.localScale = new Vector3(ratio, 1f, 1f);
        hpBar.localPosition = new Vector3(offset, 0f, 0f);
    }

    public void Attacked(int damage)
    {
        if (_isGuard) damage /= 2;
        PlayDamageSound(damage);
        TakeDamage(damage);
    }

    // isGuard면 Weak/Damage/Big 대신 DamageGuarded를 재생한다. 방어도가 흡수한 만큼(현재 _defense와
    // damage 중 작은 값)이 1 이상이면 위 사운드에 더해 DamageShielded도 재생한다.
    private void PlayDamageSound(int damage)
    {
        if (SoundManager.Instance == null) return;

        if (_isGuard)
        {
            SoundManager.Instance.Play(EffectSound.DamageGuarded);
        }
        else if (damage <= 10)
        {
            SoundManager.Instance.Play(EffectSound.DamageWeak);
        }
        else if (damage <= 30)
        {
            SoundManager.Instance.Play(EffectSound.Damage);
        }
        else
        {
            SoundManager.Instance.Play(EffectSound.DamageBig);
        }

        int shieldedAmount = Mathf.Clamp(Mathf.Min(damage, _defense), 0, damage);
        if (shieldedAmount >= 1)
            SoundManager.Instance.Play(EffectSound.DamageShielded);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
            return;
        _defense -= amount;
        if(_defense<0)
        {
            _health += _defense;
            _defense = 0;
        }
        UpdateHPBar();
        UpdateDefenseDisplay();
        Debug.Log($"[{gameObject.name}] Took {amount} damage (HP: {_health}, DEF: {_defense})");
        if (_health <= 0 && BattleManager.Instance != null)
        {
            Debug.Log($"[{gameObject.name}] Defeated!");
            BattleManager.Instance.NotifyDefeat(this);
        }
    }

    public void Heal(int amount)
    {
        _health += amount;
        UpdateHPBar();
    }

    public void EnergyHeal(int amount)
    {
        _cost = Mathf.Clamp(_cost + amount, 0, MaxCost);
        UpdateCostDisplay();
    }
    
    public void AddDefense(int amount)
    {
        _defense += amount;
        UpdateDefenseDisplay();
    }

    public void PlayCard(CardInstance card)
    {
        card.Play(this);

        List<CardEffect> cardEffects = card.GetEffects();
        foreach (CardEffect cardEffect in cardEffects)
        {
            if (cardEffect.GetEffect().GetEffectType() == EffectType.Disposable)
                return;
        }

        ReturnToDeck(card);
        return;
    }

    // 카드 하나를 실제로 큐에 편입시키는 유일한 통로. 코스트를 감당할 수 없으면(preview 기준)
    // 곧장 실패하고, 감당 가능하면 실제 적용(ResolveUse actualUse=true) 후 큐에 자리가 있는지
    // 시도한다 — 큐가 꽉 차 있으면 QueueManager.AddCard가 false를 돌려주고, 그러면 이 카드는
    // 손패에 그대로 남는다(호출부인 HandManager가 처리). 성공한 경우에만 코스트를 차감한다.
    public bool QueueCard(CardInstance card, GameObject cardObject)
    {
        CardInstance preview = card.Clone();
        preview.ResolveUse(this, _queueManager.GetQueue(), false);
        if (_cost < preview.GetCost()) return false;

        card.ResolveUse(this, _queueManager.GetQueue(), true);
        card.Use();
        if (!_queueManager.AddCard(card, cardObject)) return false;

        _cost -= card.GetCost();
        UpdateCostDisplay();
        return true;
    }

    public CardInstance[] GetQueue() => _queueManager.GetQueue();

    public CardInstance DrawCard()
    {
        if (_deck.Count == 0) return null;
        var card = _deck[0];
        _deck.RemoveAt(0);
        return card;
    }

    public void ReturnToDeck(CardInstance card)
    {
        _deck.Add(card);
    }

    public void AddEffect(Effect effect)
    {
        _effects.Add(effect);
    }

    public void RemoveEffect<T>() where T : Effect
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i] is T)
            {
                Debug.Log($"[{gameObject.name}] Lost effect: {_effects[i].GetEffectType()}");
                _effects[i].OnExpired(this);
                _effects.RemoveAt(i);
                return;
            }
        }
    }

    public void ApplyEffect(CardEffect cardEffect)
    {
        Debug.Log($"applying {cardEffect.GetEffect().GetEffectType()} effect");

        for(int i = _effects.Count - 1; i >= 0; i--)
        {
            _effects[i].OnAppliedOther(this, cardEffect, true);
        }

        EffectType effectType = cardEffect.GetEffect().GetEffectType();
        Effect effect = Effect.Create(effectType, cardEffect.GetMagnitude());
        effect.OnApply(this);
    }

    private void UpdateCostDisplay()
    {
        if (costText == null) return;
        costText.text = $"{_cost}/{MaxCost}";
    }

    private void UpdateDefenseDisplay()
    {
        if (defenseText == null) return;
        bool show = _defense >= 1;
        defenseText.transform.parent.gameObject.SetActive(show);
        defenseText.text = _defense.ToString();
    }

    // effectList 영역의 높이에 맞춰 effectDisplay(아이콘+수치)를 인스턴스화하고,
    // shrinkRight가 false면 왼쪽부터, true면 오른쪽부터 순서대로 채워나간다.
    private void UpdateEffectList()
    {
        if (effectList == null) return;

        foreach (EffectDisplay display in _effectDisplays)
            Destroy(display.gameObject);
        _effectDisplays.Clear();

        float containerHeight = effectList.sizeDelta.y;
        float edge = (shrinkRight ? 1f : -1f) * effectList.sizeDelta.x / 2f;
        float direction = shrinkRight ? -1f : 1f;
        float cursor = edge;

        foreach (var effect in _effects)
        {
            if (effect == null) continue;

            EffectDisplay display = EffectDisplay.Spawn(effectList);
            if (display == null) return;
            _effectDisplays.Add(display);

            display.SetEffect(effect, effect.GetMagnitude().ToString());

            Vector2 spriteSize = display.GetSpriteSize();
            float nativeHeight = spriteSize.y > 0f ? spriteSize.y : containerHeight;
            float scale = nativeHeight > 0f ? containerHeight / nativeHeight : 1f;
            display.SetScale(scale);

            float displayWidth = (spriteSize.x > 0f ? spriteSize.x : containerHeight) * scale;
            float centerX = shrinkRight ? cursor - displayWidth / 2f : cursor + displayWidth / 2f;
            display.SetLocalPosition(new Vector3(centerX, 0f, 0f));

            cursor += direction * displayWidth;
        }
    }
}
