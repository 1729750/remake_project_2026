using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CardDatabase",
    menuName = "Scriptable Objects/CardDatabase")]
public class CardDatabase : ScriptableObject
{
    [Serializable]
    public class CardEntry
    {
        public int cardId;
        public CardDefinition card;
    }

    [SerializeField]
    private List<CardEntry> cards = new List<CardEntry>();

    public bool TryGetCard(
        int cardId,
        out CardDefinition card)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            CardEntry entry = cards[i];

            if (entry == null)
                continue;

            if (entry.cardId != cardId)
                continue;

            if (entry.card == null)
                continue;

            card = entry.card;
            return true;
        }

        card = null;
        return false;
    }

    public CardDefinition GetCard(int cardId)
    {
        if (TryGetCard(
            cardId,
            out CardDefinition card))
        {
            return card;
        }

        return null;
    }
}