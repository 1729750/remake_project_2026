using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class CharacterManager_Multi : MonoBehaviour
{
    public const int RedrawAction = -2;
    public const int DefenseAction = -1;

    [Header("Network")]
    [SerializeField]
    private GameNetworkState gameNetworkState;

    private ulong _representedClientId =
        GameNetworkState.UnassignedClientId;

    private bool _networkSubscribed;

    private int _maxHealth = 100;
    private int _specialAction;
    private List<CardInstance_Multi> _deck;

    private int _health;
    private int _defense;
    private int _cost;

    private const int MaxCost = 15;

    private List<Effect_Multi> _effects;

    [SerializeField]
    private CardDefinition[] startDeck;

    [SerializeField]
    private GameObject queueRoots;

    [SerializeField]
    private bool isHandVisualized;

    [SerializeField]
    private GameObject handsRoot;

    [SerializeField]
    private Transform hpBar;

    [SerializeField]
    private bool shrinkRight = true;

    [SerializeField]
    private bool playerControlled = true;

    [SerializeField]
    private TextMeshPro costText;

    [SerializeField]
    private RectTransform effectList;

    [SerializeField]
    private TextMeshPro defenseText;

    [SerializeField]
    private GameObject defenseIndicator;

    [SerializeField]
    private Transform[] defenseIndicatorRestPosition;

    [SerializeField]
    private float defenseIndicatorMoveDuration = 0.3f;

    private readonly List<EffectDisplay> _effectDisplays =
        new List<EffectDisplay>();

    private HandManager_Multi _handManager;
    private QueueManager_Multi _queueManager;

    private Coroutine _defenseIndicatorCoroutine;

    private bool _isGuard;

    public bool GetIsGuard() => _isGuard;

    private int tickSpeed = 1;

    public ulong RepresentedClientId =>
        _representedClientId;

    public bool IsLocalView =>
        NetworkManager.Singleton != null &&
        _representedClientId ==
        NetworkManager.Singleton.LocalClientId;

    private bool IsServerAuthority =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsServer;

    public void BindClientId(
        ulong clientId,
        bool isLocalView)
    {
        _representedClientId = clientId;

        // Single의 playerControlled 의미를 그대로 살리되,
        // Multi에서는 "이 PC가 직접 조작하는 Local View인가"로 사용한다.
        playerControlled = isLocalView;

        SubscribeNetworkState();
        RefreshFromNetworkState();
    }

    private void SubscribeNetworkState()
    {
        if (_networkSubscribed ||
            gameNetworkState == null)
        {
            return;
        }

        gameNetworkState.StateChanged +=
            RefreshFromNetworkState;

        _networkSubscribed = true;
    }

    private void OnDestroy()
    {
        if (_networkSubscribed &&
            gameNetworkState != null)
        {
            gameNetworkState.StateChanged -=
                RefreshFromNetworkState;
        }
    }

    public void ChangeTickSpeed(int delta)
    {
        if (!IsServerAuthority)
            return;

        tickSpeed += delta;
    }

    public void CharacterInit(
        CardDefinition[] deck,
        int maxHealth)
    {
        if (!IsServerAuthority)
            return;

        _maxHealth = maxHealth;
        startDeck = deck;
        _isGuard = false;
        tickSpeed = 1;

        Clear();

        if (startDeck != null)
        {
            foreach (CardDefinition def in startDeck)
            {
                if (def != null)
                {
                    _deck.Add(
                        new CardInstance_Multi(
                            def,
                            this));
                }
            }
        }

        ShuffleDeck();
        _handManager?.FillHand();

        SyncPublicStateServer();
    }

    private void ShuffleDeck()
    {
        if (!IsServerAuthority ||
            _deck == null)
        {
            return;
        }

        for (int i = _deck.Count - 1;
             i > 0;
             i--)
        {
            int j = UnityEngine.Random.Range(
                0,
                i + 1);

            (_deck[i], _deck[j]) =
                (_deck[j], _deck[i]);
        }
    }

    private void Update()
    {
        // Multi에서 AI는 존재하지 않는다.
        // Player/Enemy는 Local/Remote View 구분이다.
        UpdateCostDisplay();
    }

    public void OnTurnStart()
    {
        if (!IsServerAuthority)
            return;

        if (_handManager == null)
            return;

        for (int i = _effects.Count - 1;
             i >= 0;
             i--)
        {
            _effects[i].OnTurnStarted(this);
        }

        int healEnergyAmount = 2;

        if (_isGuard)
            healEnergyAmount--;

        if (_cost >= 10)
            healEnergyAmount--;

        EnergyHeal(healEnergyAmount);

        UpdateEffectList();

        _handManager.UseCard();
        _handManager.FillHand();
        _handManager.RefreshHandDisplay();

        SyncPublicStateServer();
    }

    public void OnTurnEnd()
    {
        if (!IsServerAuthority)
            return;

        _queueManager?.TickQueueCards(
            this,
            tickSpeed);

        for (int i = _effects.Count - 1;
             i >= 0;
             i--)
        {
            _effects[i].OnTurnEnded(this);
        }

        _specialAction = 0;

        _handManager?.RefreshHandDisplay();

        SyncPublicStateServer();
    }

    private void Redraw()
    {
        if (!IsServerAuthority)
            return;

        if (_cost == 0)
            return;

        _cost--;

        _handManager.ReturnHandToDeck();

        ShuffleDeck();

        _handManager.FillHand();

        SyncPublicStateServer();

        Debug.Log(
            $"[{gameObject.name}] Redrew hand");
    }

    private void SetDefenseIndicatorActive(
        bool active)
    {
        if (defenseIndicator == null ||
            defenseIndicatorRestPosition == null)
        {
            return;
        }

        int targetIndex =
            active ? 1 : 0;

        if (targetIndex >=
                defenseIndicatorRestPosition.Length ||
            defenseIndicatorRestPosition[targetIndex] == null)
        {
            return;
        }

        if (!active &&
            !defenseIndicator.activeSelf)
        {
            return;
        }

        if (_defenseIndicatorCoroutine != null)
        {
            StopCoroutine(
                _defenseIndicatorCoroutine);
        }

        if (active)
            defenseIndicator.SetActive(true);

        _defenseIndicatorCoroutine =
            StartCoroutine(
                MoveDefenseIndicator(
                    defenseIndicatorRestPosition[
                        targetIndex].position,
                    !active));
    }

    private IEnumerator MoveDefenseIndicator(
        Vector3 targetPosition,
        bool deactivateOnComplete)
    {
        Transform indicatorTransform =
            defenseIndicator.transform;

        Vector3 startPosition =
            indicatorTransform.position;

        float elapsed = 0f;

        while (elapsed <
               defenseIndicatorMoveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(
                    elapsed /
                    defenseIndicatorMoveDuration));

            indicatorTransform.position =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    t);

            yield return null;
        }

        indicatorTransform.position =
            targetPosition;

        if (deactivateOnComplete)
            defenseIndicator.SetActive(false);

        _defenseIndicatorCoroutine = null;
    }

    // Local 입력 진입점.
    // 직접 상태를 바꾸지 않고 BattleManager_Multi를 통해 Server에 요청한다.
    public void SelectCard(int index)
    {
        if (!playerControlled ||
            BattleManager_Multi.Instance == null)
        {
            return;
        }

        BattleManager_Multi.Instance
            .RequestSelectCard(index);
    }

    // Server만 호출.
    public void SelectCardServer(int index)
    {
        if (!IsServerAuthority)
            return;

        if (BattleManager_Multi.Instance == null ||
            BattleManager_Multi.Instance.CurrentState !=
                BattleState.Turn)
        {
            return;
        }

        if (index == RedrawAction ||
            index == DefenseAction)
        {
            _specialAction = index;

            _handManager.UnselectCard();

            if (index == RedrawAction)
            {
                Redraw();
            }
            else
            {
                _isGuard = !_isGuard;
                SyncPublicStateServer();
            }

            SetDefenseIndicatorActive(
                _isGuard);

            return;
        }

        _isGuard = false;

        CardInstance_Multi[] hand =
            _handManager.GetHand();

        if (index < 0 ||
            index >= hand.Length ||
            hand[index] == null)
        {
            return;
        }

        _specialAction = 0;

        SetDefenseIndicatorActive(false);

        string cardName =
            hand[index].GetDefinition().name;

        if (_handManager.UseCardImmediately(index))
        {
            Debug.Log(
                $"[{gameObject.name}] Used card: {cardName}");
        }
        else
        {
            _handManager.SelectCard(index);

            Debug.Log(
                $"[{gameObject.name}] Selected card: {cardName}");
        }

        SyncPublicStateServer();
    }

    public void ClearHandAndQueue()
    {
        _handManager?.ClearHand();
        _queueManager?.ClearQueue();
    }

    public int getmaxHealth() => _maxHealth;
    public int GetHealth() => _health;
    public int GetDefense() => _defense;
    public int GetCost() => _cost;

    public Effect_Multi[] GetEffects()
    {
        return _effects != null
            ? _effects.ToArray()
            : Array.Empty<Effect_Multi>();
    }

    public Effect_Multi[] GetEffectPrioritize()
    {
        return _effects != null
            ? _effects
                .OrderByDescending(
                    e => e.GetEffectPriority())
                .ToArray()
            : Array.Empty<Effect_Multi>();
    }

    public void Init()
    {
        _effects =
            new List<Effect_Multi>();

        _deck =
            new List<CardInstance_Multi>();

        _queueManager =
            new QueueManager_Multi(
                queueRoots);

        _handManager =
            new HandManager_Multi(
                this,
                handsRoot,
                isHandVisualized);

        SubscribeNetworkState();

        RefreshFromNetworkState();
    }

    public void Clear()
    {
        if (_effects == null)
            _effects = new List<Effect_Multi>();
        else
            _effects.Clear();

        _deck =
            new List<CardInstance_Multi>();

        _health = _maxHealth;
        _defense = 0;
        _cost = 0;
        _isGuard = false;

        UpdateHPBar();
        UpdateDefenseDisplay();
        UpdateCostDisplay();
        UpdateEffectList();
        ResetDefenseIndicator();

        if (IsServerAuthority)
            SyncPublicStateServer();
    }

    private void ResetDefenseIndicator()
    {
        if (defenseIndicator == null ||
            defenseIndicatorRestPosition == null ||
            defenseIndicatorRestPosition.Length == 0 ||
            defenseIndicatorRestPosition[0] == null)
        {
            return;
        }

        if (_defenseIndicatorCoroutine != null)
        {
            StopCoroutine(
                _defenseIndicatorCoroutine);

            _defenseIndicatorCoroutine = null;
        }

        defenseIndicator.transform.position =
            defenseIndicatorRestPosition[0]
                .position;

        defenseIndicator.SetActive(false);
    }

    private void UpdateHPBar()
    {
        if (hpBar == null)
            return;

        float ratio =
            _maxHealth > 0
                ? (float)_health / _maxHealth
                : 0f;

        ratio =
            Mathf.Clamp01(ratio);

        float offset =
            shrinkRight
                ? (ratio - 1f) * 0.5f
                : (1f - ratio) * 0.5f;

        hpBar.localScale =
            new Vector3(
                ratio,
                1f,
                1f);

        hpBar.localPosition =
            new Vector3(
                offset,
                0f,
                0f);
    }

    public void Attacked(int damage)
    {
        if (!IsServerAuthority)
            return;

        if (_isGuard)
            damage /= 2;

        TakeDamage(damage);
    }

    public void TakeDamage(int amount)
    {
        if (!IsServerAuthority ||
            amount <= 0)
        {
            return;
        }

        _defense -= amount;

        if (_defense < 0)
        {
            _health += _defense;
            _defense = 0;
        }

        UpdateHPBar();
        UpdateDefenseDisplay();

        SyncPublicStateServer();

        Debug.Log(
            $"[{gameObject.name}] Took {amount} damage " +
            $"(HP: {_health}, DEF: {_defense})");

        if (_health <= 0 &&
            BattleManager_Multi.Instance != null)
        {
            BattleManager_Multi.Instance
                .NotifyDefeat(this);
        }
    }

    public void Heal(int amount)
    {
        if (!IsServerAuthority)
            return;

        _health += amount;

        UpdateHPBar();
        SyncPublicStateServer();
    }

    public void EnergyHeal(int amount)
    {
        if (!IsServerAuthority)
            return;

        _cost =
            Mathf.Clamp(
                _cost + amount,
                0,
                MaxCost);

        UpdateCostDisplay();
        SyncPublicStateServer();
    }

    public void AddDefense(int amount)
    {
        if (!IsServerAuthority)
            return;

        _defense += amount;

        UpdateDefenseDisplay();
        SyncPublicStateServer();
    }

    public void PlayCard(
        CardInstance_Multi card)
    {
        if (!IsServerAuthority ||
            card == null)
        {
            return;
        }

        card.Play(this);

        foreach (
            CardEffect_Multi cardEffect
            in card.GetEffects())
        {
            if (cardEffect.GetEffect()
                    .GetEffectType() ==
                EffectType.Disposable)
            {
                return;
            }
        }

        ReturnToDeck(card);
    }

    public bool QueueCard(
        CardInstance_Multi card,
        GameObject cardObject)
    {
        if (!IsServerAuthority ||
            card == null)
        {
            return false;
        }

        CardInstance_Multi preview =
            card.Clone();

        preview.ResolveUse(
            this,
            _queueManager.GetQueue(),
            false);

        if (_cost < preview.GetCost())
            return false;

        card.ResolveUse(
            this,
            _queueManager.GetQueue(),
            true);

        card.Use();

        if (!_queueManager.AddCard(
                card,
                cardObject))
        {
            return false;
        }

        _cost -= card.GetCost();

        UpdateCostDisplay();
        SyncPublicStateServer();

        return true;
    }

    public CardInstance_Multi[] GetQueue()
    {
        return _queueManager != null
            ? _queueManager.GetQueue()
            : Array.Empty<CardInstance_Multi>();
    }

    public CardInstance_Multi DrawCard()
    {
        if (!IsServerAuthority ||
            _deck == null ||
            _deck.Count == 0)
        {
            return null;
        }

        CardInstance_Multi card =
            _deck[0];

        _deck.RemoveAt(0);

        return card;
    }

    public void ReturnToDeck(
        CardInstance_Multi card)
    {
        if (!IsServerAuthority ||
            card == null)
        {
            return;
        }

        _deck.Add(card);
    }

    public void AddEffect(
        Effect_Multi effect)
    {
        if (!IsServerAuthority ||
            effect == null)
        {
            return;
        }

        _effects.Add(effect);
        UpdateEffectList();
    }

    public void RemoveEffect<T>()
        where T : Effect_Multi
    {
        if (!IsServerAuthority)
            return;

        for (int i = 0;
             i < _effects.Count;
             i++)
        {
            if (!(_effects[i] is T))
                continue;

            Debug.Log(
                $"[{gameObject.name}] Lost effect: " +
                $"{_effects[i].GetEffectType()}");

            _effects[i].OnExpired(this);

            _effects.RemoveAt(i);

            UpdateEffectList();
            SyncPublicStateServer();

            return;
        }
    }

    public void ApplyEffect(
        CardEffect_Multi cardEffect)
    {
        if (!IsServerAuthority ||
            cardEffect == null ||
            cardEffect.GetEffect() == null)
        {
            return;
        }

        for (int i = _effects.Count - 1;
             i >= 0;
             i--)
        {
            _effects[i].OnAppliedOther(
                this,
                cardEffect,
                true);
        }

        Effect_Multi source =
            cardEffect.GetEffect();

        Effect_Multi runtimeEffect =
            Effect_Multi.Create(
                source.GetEffectType(),
                cardEffect.GetMagnitude());

        runtimeEffect.OnApply(this);

        UpdateEffectList();
        SyncPublicStateServer();
    }

    private void UpdateCostDisplay()
    {
        if (costText == null)
            return;

        costText.text =
            $"{_cost}/{MaxCost}";
    }

    private void UpdateDefenseDisplay()
    {
        if (defenseText == null)
            return;

        bool show =
            _defense >= 1;

        if (defenseText.transform.parent != null)
        {
            defenseText.transform.parent
                .gameObject
                .SetActive(show);
        }

        defenseText.text =
            _defense.ToString();
    }

    private void UpdateEffectList()
    {
        if (effectList == null)
            return;

        foreach (
            EffectDisplay display
            in _effectDisplays)
        {
            if (display != null)
                Destroy(display.gameObject);
        }

        _effectDisplays.Clear();

        if (_effects == null)
            return;

        float containerHeight =
            effectList.sizeDelta.y;

        float edge =
            (shrinkRight ? 1f : -1f) *
            effectList.sizeDelta.x /
            2f;

        float direction =
            shrinkRight ? -1f : 1f;

        float cursor = edge;

        foreach (
            Effect_Multi effect
            in _effects)
        {
            if (effect == null)
                continue;

            EffectDisplay display =
                EffectDisplay.Spawn(
                    effectList);

            if (display == null)
                return;

            _effectDisplays.Add(display);

            // Effect_Multi는 Effect를 상속하므로
            // 기존 EffectDisplay를 그대로 재사용한다.
            display.SetEffect(
                effect,
                effect.GetMagnitude().ToString());

            Vector2 spriteSize =
                display.GetSpriteSize();

            float nativeHeight =
                spriteSize.y > 0f
                    ? spriteSize.y
                    : containerHeight;

            float scale =
                nativeHeight > 0f
                    ? containerHeight /
                      nativeHeight
                    : 1f;

            display.SetScale(scale);

            float displayWidth =
                (spriteSize.x > 0f
                    ? spriteSize.x
                    : containerHeight) *
                scale;

            float centerX =
                shrinkRight
                    ? cursor -
                      displayWidth / 2f
                    : cursor +
                      displayWidth / 2f;

            display.SetLocalPosition(
                new Vector3(
                    centerX,
                    0f,
                    0f));

            cursor +=
                direction *
                displayWidth;
        }
    }

    public void SyncPublicStateServer()
    {
        if (!IsServerAuthority ||
            gameNetworkState == null ||
            _representedClientId ==
                GameNetworkState.UnassignedClientId)
        {
            return;
        }

        gameNetworkState
            .SetCharacterPublicStateServer(
                _representedClientId,
                _maxHealth,
                _health,
                _defense,
                _cost,
                _isGuard);
    }

    private void RefreshFromNetworkState()
    {
        if (gameNetworkState == null ||
            _representedClientId ==
                GameNetworkState.UnassignedClientId)
        {
            return;
        }

        if (!gameNetworkState.TryGetCharacterPublicState(
                _representedClientId,
                out int maxHealth,
                out int health,
                out int defense,
                out int cost,
                out bool guard))
        {
            return;
        }

        _maxHealth = Mathf.Max(1, maxHealth);
        _health = health;
        _defense = defense;
        _cost = cost;
        _isGuard = guard;

        UpdateHPBar();
        UpdateDefenseDisplay();
        UpdateCostDisplay();
        SetDefenseIndicatorActive(_isGuard);
    }
}
