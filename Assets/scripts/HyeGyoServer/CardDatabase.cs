using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CardDatabase",
    menuName = "Game/Card Database"
)]
public sealed class CardDatabase :
    ScriptableObject
{
    [SerializeField]
    private List<CardDefinition> cards =
        new List<CardDefinition>();

    public bool TryGetCard(
        int cardId,
        out CardDefinition card)
    {
        for (int i = 0;
             i < cards.Count;
             i++)
        {
            if (cards[i].Id == cardId)
            {
                card = cards[i];
                return true;
            }
        }

        card = null;
        return false;
    }
}


[Serializable]
public sealed class CardDefinition
{
    [SerializeField] private int id;
    [SerializeField] private string cardName;
    [SerializeField] private int value;

    public int Id => id;
    public string CardName => cardName;
    public int Value => value;
}