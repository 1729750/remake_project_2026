using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance {get; private set;}
    [SerializeField] private CharacterManager characterManager;
    [SerializeField] private int maxHealth = 100;
    // 에디터에서 구성한 원본 덱. Init 이후로는 게임 내에서 절대 직접 수정하지 않는다 —
    // 실제 플레이 중 변화(GetDeck/AddCard/DiscardCard)는 전부 _deckInstance를 대상으로 이루어진다.
    [SerializeField] private List<CardDefinition> deck;

    private List<CardDefinition> _deckInstance;

    public void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Init();
    }

    // deck(직렬화된 원본, 읽기 전용)을 Instantiate로 복제해 _deckInstance를 새로 만든다
    // (내부 CardEffect/Effect까지 포함해 Unity 직렬화 그래프 전체가 깊은 복사된다).
    // GameManager.GameStart가 매 게임(새 런) 시작 시 이 함수를 다시 호출해, 플레이어 덱이
    // 항상 원본과 동일한 상태로 시작하도록 보장한다.
    public void Init()
    {
        _deckInstance = new List<CardDefinition>(deck.Count);
        foreach (CardDefinition card in deck)
            _deckInstance.Add(Instantiate(card));
    }

    // 현재 플레이어 상태를 CharacterData로 박싱해서 반환한다
    public CharacterData GetCharacterData()
    {
        return CharacterData.Create(maxHealth, CardCollection.Create(_deckInstance));
    }

    public List<CardDefinition> GetDeck() => _deckInstance;
    public int GetMaxHealth() => maxHealth;
    public CharacterManager GetCharacterManager() => characterManager;

    public CardDefinition AddCard(CardDefinition card)
    {
       _deckInstance.Add(card);
       return _deckInstance[_deckInstance.Count - 1];
    }

    public CardDefinition DiscardCard(int index)
    {
        if (index < 0 || index >= _deckInstance.Count)
            return null;
        CardDefinition removed = _deckInstance[index];
        _deckInstance.RemoveAt(index);
        return removed;
    }
}
