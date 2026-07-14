using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private CharacterManager characterManager;
    [SerializeField] private CardDefinition[] deck;
    [SerializeField] private int maxHealth = 100;

    public void InitBattleCharacter()
    {
        characterManager.BattleCharacterInit(deck, maxHealth);
    }

    public CardDefinition[] GetDeck() => deck;
    public int GetMaxHealth() => maxHealth;
    public CharacterManager GetCharacterManager() => characterManager;
}
