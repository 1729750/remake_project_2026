using System.Collections.Generic;
using UnityEngine;

public class CardInstance_Multi
{
    private CardDefinition _definition;
    private bool _isPlayed;
    private int _cooldownLeft;
    private int _cooldown;
    private int _cost;
    private CardType cardType;
    private List<CardEffect_Multi> _effects;

    private CharacterManager_Multi _owner;
    private CardVisual _visual;

    private float _costMultiplier = 1f;
    private int _costAdder;

    public CardInstance_Multi(
        CardDefinition definition,
        CharacterManager_Multi owner)
    {
        _definition = definition;
        _owner = owner;
        RefreshInstance();
        _isPlayed = false;
    }

    public CardVisual GetVisual() => _visual;

    public void SetVisual(CardVisual visual)
    {
        _visual = visual;

        if (_visual == null)
            return;

        _visual.SetCardDefinition(_definition);
        RefreshCardVisual();
    }

    private void RefreshCardVisual()
    {
        if (_visual == null)
            return;

        foreach (CardEffect_Multi cardEffect in _effects)
            cardEffect.Reset();

        _visual.SetCostText(GetCost().ToString());
        _visual.SetCooldownText(_cooldownLeft.ToString());

        // CardVisual은 기존 Single CardEffect 타입을 받으므로
        // CardEffect_Multi가 CardEffect를 상속하도록 구성했다.
        _visual.RefreshEffectDisplays(
            new List<CardEffect>(_effects));
    }

    public void SetSelected(bool selected)
    {
        _visual?.SetSelected(selected);
    }

    public void SetFace(bool front)
    {
        _visual?.SetFace(front);
    }

    public void SetLayer(string sortingLayerName)
    {
        _visual?.SetLayer(sortingLayerName);
    }

    public void SetSize(Vector2 targetSize)
    {
        _visual?.SetSize(targetSize);
    }

    public void MoveTo(
        Vector3 targetPosition,
        float duration = 0.3f)
    {
        _visual?.MoveTo(targetPosition, duration);
    }

    public Vector2 GetBackgroundSize()
    {
        return _visual != null
            ? _visual.GetBackgroundSize()
            : Vector2.zero;
    }

    private void RefreshInstance()
    {
        _cooldown = _definition.GetCooldown();
        _cooldownLeft = _definition.GetCooldown();
        _cost = _definition.GetCost();

        _effects = new List<CardEffect_Multi>();

        foreach (CardEffect source in _definition.GetEffects())
        {
            if (source == null || source.GetEffect() == null)
                continue;

            Effect sourceEffect = source.GetEffect();

            _effects.Add(
                new CardEffect_Multi(
                    Effect_Multi.Create(
                        sourceEffect.GetEffectType(),
                        sourceEffect.GetMagnitude()),
                    source.GetEffectTarget()));
        }
    }

    public CardDefinition GetDefinition() => _definition;
    public List<CardEffect_Multi> GetEffects() => _effects;

    public int GetCost()
    {
        return Mathf.FloorToInt(
            _cost * _costMultiplier) + _costAdder;
    }

    public int GetCooldown() => _cooldown;
    public bool GetIsPlayed() => _isPlayed;
    public int GetCooldownLeft() => _cooldownLeft;
    public bool IsReady() => _cooldownLeft <= 0;

    public void AddCost(int amount)
    {
        _costAdder += amount;
    }

    public void ChangeCooldown(int amount)
    {
        int originalCooldown = _cooldown;

        _cooldown += amount;

        if (_cooldown <= 0)
            _cooldown = 1;

        amount = _cooldown - originalCooldown;

        if (!_isPlayed)
        {
            _cooldownLeft += amount;

            if (_cooldownLeft <= 0)
                _cooldownLeft = 1;
        }
    }

    public void ChangeCooldownLeft(int amount)
    {
        _cooldownLeft += amount;

        if (_cooldownLeft <= 0)
            _cooldownLeft = _isPlayed ? 0 : 1;

        RefreshCardVisual();
    }

    public void SetCooldownLeft(int value)
    {
        _cooldownLeft = value;
        RefreshCardVisual();
    }

    public void MultiplyCost(float amount)
    {
        _costMultiplier *= amount;
    }

    public void Use()
    {
        _isPlayed = true;
        RefreshCardVisual();
    }

    public void TickCooldown(int tick = 1)
    {
        _cooldownLeft -= tick;
        RefreshCardVisual();
    }

    public void Play(CharacterManager_Multi characterManager)
    {
        _cooldownLeft = GetCooldown();

        Debug.Log(
            $"[{characterManager.gameObject.name}] Activated card: {_definition.name}");

        SoundManager.Instance?.Play(EffectSound.PlayCard);

        if (_effects.Exists(
                e => e.GetEffect().GetEffectType() == EffectType.Attack))
        {
            SoundManager.Instance?.Play(EffectSound.Attack);
        }

        foreach (CardEffect_Multi cardEffect in _effects)
        {
            cardEffect.Reset();

            Effect_Multi[] ownerEffects =
                characterManager.GetEffectPrioritize();

            for (int i = ownerEffects.Length - 1; i >= 0; i--)
            {
                ownerEffects[i].OnApplyingOther(
                    characterManager,
                    cardEffect,
                    true);
            }

            CharacterManager_Multi resolved =
                cardEffect.GetTarget(characterManager);

            resolved?.ApplyEffect(cardEffect);
        }
    }

    public void ResolveUse(
        CharacterManager_Multi subject,
        IEnumerable<CardInstance_Multi> queuedCards,
        bool actualUse)
    {
        _costMultiplier = 1f;
        _costAdder = 0;

        if (actualUse)
        {
            foreach (CardEffect_Multi cardEffect in _effects)
            {
                ResolveEffect(cardEffect).OnUse(
                    subject,
                    this,
                    true);
            }
        }

        if (queuedCards == null)
            return;

        foreach (CardInstance_Multi queued in queuedCards)
        {
            if (queued == null)
                continue;

            foreach (CardEffect_Multi cardEffect in queued.GetEffects())
            {
                ResolveEffect(cardEffect).OnUsingOther(
                    subject,
                    this,
                    actualUse);
            }
        }
    }

    private static Effect_Multi ResolveEffect(
        CardEffect_Multi cardEffect)
    {
        Effect_Multi effect = cardEffect.GetEffect();

        return Effect_Multi.Create(
            effect.GetEffectType(),
            effect.GetMagnitude());
    }

    public void RefreshDisplay(
        CharacterManager_Multi subject,
        IEnumerable<CardInstance_Multi> queuedCards)
    {
        if (_visual == null || subject == null)
            return;

        CardInstance_Multi preview = Clone();

        preview.ResolveUse(
            subject,
            queuedCards,
            false);

        foreach (CardEffect_Multi cardEffect in preview._effects)
        {
            cardEffect.Reset();

            Effect_Multi[] ownerEffects =
                subject.GetEffectPrioritize();

            for (int i = ownerEffects.Length - 1; i >= 0; i--)
            {
                ownerEffects[i].OnApplyingOther(
                    subject,
                    cardEffect,
                    false);
            }

            CharacterManager_Multi target =
                cardEffect.GetTarget(subject);

            if (target == null)
                continue;

            Effect_Multi[] targetEffects =
                target.GetEffects();

            for (int i = targetEffects.Length - 1; i >= 0; i--)
            {
                targetEffects[i].OnAppliedOther(
                    target,
                    cardEffect,
                    false);
            }
        }

        _visual.SetCostText(
            preview.GetCost().ToString());

        _visual.SetCooldownText(
            _cooldownLeft.ToString());

        _visual.RefreshEffectDisplays(
            new List<CardEffect>(preview._effects));

        _visual.SetUnplayable(
            preview.GetCost() > subject.GetCost());
    }

    public CardInstance_Multi Clone()
    {
        CardInstance_Multi clone =
            new CardInstance_Multi(
                _definition,
                _owner)
            {
                _isPlayed = _isPlayed,
                _cooldownLeft = _cooldownLeft,
                _cost = _cost,
                _cooldown = _cooldown
            };

        clone._effects =
            new List<CardEffect_Multi>(_effects.Count);

        foreach (CardEffect_Multi cardEffect in _effects)
        {
            Effect_Multi effect = cardEffect.GetEffect();

            clone._effects.Add(
                new CardEffect_Multi(
                    Effect_Multi.Create(
                        effect.GetEffectType(),
                        effect.GetMagnitude()),
                    cardEffect.GetEffectTarget()));
        }

        return clone;
    }
}
