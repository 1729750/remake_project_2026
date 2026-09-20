using System;
using System.Collections.Generic;
using UnityEngine;

// CharacterData에 인라인으로 직렬화되는 카드 목록. 예전엔 별도 ScriptableObject 에셋이었지만,
// 적 하나당 파일이 둘(Enemy/Deck)로 쪼개져 관리가 지저분해서 CharacterData 안에 파묻었다.
[Serializable]
public class CardCollection
{
    [SerializeField] private List<CardDefinition> cards;

    public CardDefinition[] GetCards() => cards != null ? cards.ToArray() : new CardDefinition[0];
    public int GetCount() => cards?.Count ?? 0;

    // 런타임 카드 리스트를 CardCollection으로 감싼다 (PlayerManager 덱 → CharacterData 박싱용)
    public static CardCollection Create(IEnumerable<CardDefinition> cards)
    {
        return new CardCollection { cards = new List<CardDefinition>(cards) };
    }
}
