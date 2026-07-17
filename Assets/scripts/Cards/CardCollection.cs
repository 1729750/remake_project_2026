using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CardCollection", menuName = "Scriptable Objects/CardCollection")]
public class CardCollection : ScriptableObject
{
    [SerializeField] private List<CardDefinition> cards;

    public CardDefinition[] GetCards() => cards != null ? cards.ToArray() : new CardDefinition[0];
    public int GetCount() => cards?.Count ?? 0;

    // 런타임 카드 리스트를 CardCollection으로 감싼다 (PlayerManager 덱 → CharacterData 박싱용)
    public static CardCollection Create(IEnumerable<CardDefinition> cards)
    {
        var collection = CreateInstance<CardCollection>();
        collection.cards = new List<CardDefinition>(cards);
        return collection;
    }
}
