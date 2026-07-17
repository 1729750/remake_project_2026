using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    
    [SerializeField] protected CharacterManager playerCharacterManager;
    [SerializeField] protected InputActionAsset inputActions;

    public virtual void LoadBattleInputActions()
    {
    }

    public virtual void LoadSelectInputActions()
    {
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        switch (GameManager.Instance.GetGameState())
        {
            case GameState.StartScreen:
                break;
            case GameState.Battle:
                HandleBattleInput();
                break;
            case GameState.BattleEnd:
                HandleSelectInput();
                break;
        }
    }

    protected virtual void HandleBattleInput()
    {
    }

    protected virtual void HandleSelectInput()
    {
        
    }
}
