using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance {get; private set;}
    [SerializeField] private CharacterManager characterManager;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private List<CardDefinition> deck;

    public void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    // 현재 플레이어 상태를 CharacterData로 박싱해서 반환한다
    public CharacterData GetCharacterData()
    {
        return CharacterData.Create(maxHealth, CardCollection.Create(deck));
    }

    public List<CardDefinition> GetDeck() => deck;
    public int GetMaxHealth() => maxHealth;
    public CharacterManager GetCharacterManager() => characterManager;

    public CardDefinition AddCard(CardDefinition card)
    {
       deck.Add(card);
       return deck[deck.Count - 1];
    }

    public CardDefinition DiscardCard(int index)
    {
        if (index < 0 || index >= deck.Count)
            return null;
        CardDefinition removed = deck[index];
        deck.RemoveAt(index);
        return removed;
    }
}
