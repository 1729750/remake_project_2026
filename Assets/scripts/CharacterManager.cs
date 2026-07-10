using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class CharacterManager: MonoBehaviour
{
    private int _maxHealth = 100;
    private List<CardInstance> _deck;
    
    private int _health;
    private int _defense;
    private int _cost;
    private const int MaxCost = 10;
    private List<Effect> _effects;
    [SerializeField] private CardDefinition[] startDeck;
    [SerializeField] private GameObject queueRoots;
    [SerializeField] private bool isHandVisualized;
    [SerializeField] private GameObject handsRoot;
    [SerializeField] private Transform hpBar;
    [SerializeField] private bool shrinkRight = true;
    [SerializeField] private bool playerControlled = true;
    [SerializeField] private TextMeshPro costText;
    [SerializeField] private TextMeshPro effectListText;
    [SerializeField] private TextMeshPro defenseText;
    private HandManager _handManager;
    private QueueManager _queueManager;
    
    public CharacterManager()
    {
        _effects = new List<Effect>();
    }

    private void Update()
    {
        UpdateCostDisplay();
    }

    public void OnTurnStart()
    {
        if (_handManager == null) return;
        _cost = Mathf.Min(_cost + 1, MaxCost);
        UpdateCostDisplay();
        UpdateEffectList();
        _handManager.FillHand();
    }

    public void OnTurnEnd()
    {
        _queueManager.TickQueueCards(this);
        for (int i = _effects.Count - 1; i >= 0; i--)
            _effects[i].OnTurnEnded(this);
        if (!playerControlled)
            SelectRandomCard();
        var selected = _handManager?.GetSelectedCard();
        if (selected != null && _cost >= selected.GetCost())
        {
            if(_handManager.UseCard())
            {
                _cost -= selected.GetCost();
            }
            UpdateCostDisplay();
        }
    }

    public void SelectCard(int index)
    {
        if (BattleManager.Instance == null || BattleManager.Instance.CurrentState != BattleState.Turn) return;
        var hand = _handManager.GetHand();
        if (index < 0 || index >= hand.Length || hand[index] == null) return;
        _handManager.SelectCard(index);
        Debug.Log($"[{gameObject.name}] Selected card: {hand[index].GetDefinition().name}");
    }

    public void SelectRandomCard()
    {
        var hand = _handManager.GetHand();
        var valid = new List<int>();
        for (int i = 0; i < hand.Length; i++)
        {
            if (hand[i] != null && hand[i].GetCost() <= _cost) valid.Add(i);
        }
        if (valid.Count == 0) return;
        int idx = valid[UnityEngine.Random.Range(0, valid.Count)];
        _handManager.SelectCard(idx);
        int cardCost = hand[idx].GetCost();
        string cardName = hand[idx].GetDefinition().name;
        Debug.Log($"[{gameObject.name}] Selected card: {cardName} (cost: {cardCost}, cost left: {_cost})");
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
        
        //덱 생성
        if (startDeck != null)
            foreach (var def in startDeck)
                _deck.Add(new CardInstance(def,this));
        
        //덱 셔플
        for (int i = _deck.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
        }
        
        //손 채우기
        _handManager.FillHand();
    }

    public void Clear()
    {
        _effects.Clear();
        _deck = new List<CardInstance>();
        _health = _maxHealth;
        _defense = 0;
        _cost = 0;
        UpdateHPBar();
        UpdateDefenseDisplay();
    }

    private void UpdateHPBar()
    {
        if (hpBar == null) return;
        float ratio = (float)_health / _maxHealth;
        float offset = shrinkRight ? (ratio - 1f) * 0.5f : (1f - ratio) * 0.5f;
        hpBar.localScale = new Vector3(ratio, 1f, 1f);
        hpBar.localPosition = new Vector3(offset, 0f, 0f);
    }

    public void Attacked(int damage)
    {
        TakeDamage(damage);
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

    public void NotifyVictory()
    {
        Debug.Log($"[{gameObject.name}] Victory!");
    }

    public void Heal(int amount)
    {
        _health += amount;
        UpdateHPBar();
    }

    public void AddDefense(int amount)
    {
        _defense += Math.Max(amount,0);
        UpdateDefenseDisplay();
    }

    public void PlayCard(CardInstance card)
    {
        card.Play(this);
    }

    public bool QueueCard(CardInstance card, GameObject cardObject)
    {
        if (!_queueManager.AddCard(card, cardObject))
            return false;

        if (cardObject != null)
        {
            int index = Array.IndexOf(_queueManager.GetQueue(), card);
            Transform slot = _queueManager.GetSlot(index);
            cardObject.transform.SetParent(slot, false);
            cardObject.transform.localPosition = Vector3.zero;
        }

        return true;
    }

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
            _effects[i].OnApplied(this, cardEffect, true);
        }
        EffectType effectType = cardEffect.GetEffect().GetEffectType();
        switch (effectType)
        {
            case(EffectType.Attack):
                Attacked(cardEffect.GetMagnitude());
                break;
            case(EffectType.Defend):
                AddDefense(cardEffect.GetMagnitude());
                break;
            default:
                for (int i = 0; i < _effects.Count; i++){
                    if (_effects[i].GetEffectType() == effectType)
                    {
                        _effects[i].AddMagnitude(cardEffect.GetMagnitude());
                        return;
                    }
                }
                var newEffect = Effect.Create(effectType, cardEffect.GetMagnitude());
                _effects.Add(newEffect);
                Debug.Log($"[{gameObject.name}] Gained effect: {effectType} :{cardEffect.GetMagnitude()}");
                break;
        }
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

    private void UpdateEffectList()
    {
        if (effectListText == null) return;
        var parts = new List<string>();
        foreach (var effect in _effects)
        {
            if (effect == null) continue;
            parts.Add($"{BattleManager.Instance.GetEmoji(effect.GetEffectType())}:{effect.GetMagnitude()}");
        }
        effectListText.text = string.Join(" ", parts);
    }
}
