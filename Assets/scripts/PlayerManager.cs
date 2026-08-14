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

        // 인스펙터에서 받은 deck은 원본 CardDefinition 에셋을 그대로 참조하고 있어서,
        // UpgradeEffect 같은 런타임 수정이 에셋 자체를 오염시킨다. Instantiate로 각 카드를
        // 복제해 이 플레이어 세션 전용 사본으로 교체한다(내부 CardEffect/Effect까지 포함해
        // Unity 직렬화 그래프 전체가 깊은 복사된다).
        for (int i = 0; i < deck.Count; i++)
            deck[i] = Instantiate(deck[i]);
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
