using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Scriptable Objects/CharacterData")]
public class CharacterData : ScriptableObject
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private CardCollection deck;
    // 이후 스프라이트, AI 성향 등 캐릭터 단위 설정을 여기에 추가

    public int GetMaxHealth() => maxHealth;
    public CardCollection GetDeck() => deck;

    // 에셋이 아닌 런타임 상태(플레이어 등)를 CharacterData로 박싱할 때 사용
    public static CharacterData Create(int maxHealth, CardCollection deck)
    {
        var data = CreateInstance<CharacterData>();
        data.maxHealth = maxHealth;
        data.deck = deck;
        return data;
    }

    // deck까지 전부 새로 복제한 깊은 복사본을 만든다(CardDefinition.Clone 참고) — 원본 에셋이나
    // 다른 복사본과 강화(ApplyUpgrade) 상태를 공유하지 않게 하기 위함이다.
    public CharacterData Clone()
    {
        CardDefinition[] clonedCards = deck != null
            ? deck.GetCards().Select(card => card.Clone()).ToArray()
            : new CardDefinition[0];
        return Create(maxHealth, CardCollection.Create(clonedCards));
    }
}
