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
}
