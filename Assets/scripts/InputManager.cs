using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    
    public static InputManager Instance {get; private set;}
    
    [SerializeField] private CharacterManager playerCharacterManager;
    [SerializeField] private InputActionAsset battleInputActions;

    private InputAction _playCard1;
    private InputAction _playCard2;
    private InputAction _playCard3;
    private InputAction _playCard4;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    public void LoadBattleInputActions()
    {
        if (_playCard1 != null) return;
        if (battleInputActions == null) return;

        var map = battleInputActions.FindActionMap("Battle", throwIfNotFound: true);
        _playCard1 = map.FindAction("PlayCard1", throwIfNotFound: true);
        _playCard2 = map.FindAction("PlayCard2", throwIfNotFound: true);
        _playCard3 = map.FindAction("PlayCard3", throwIfNotFound: true);
        _playCard4 = map.FindAction("PlayCard4", throwIfNotFound: true);
        battleInputActions.Enable();
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
        }
    }

    private void HandleBattleInput()
    {
        if (BattleManager.Instance == null || BattleManager.Instance.CurrentState != BattleState.Turn) return;

        if (_playCard1 == null) return;
        if (_playCard1.triggered)      playerCharacterManager.SelectCard(0);
        else if (_playCard2.triggered) playerCharacterManager.SelectCard(1);
        else if (_playCard3.triggered) playerCharacterManager.SelectCard(2);
        else if (_playCard4.triggered) playerCharacterManager.SelectCard(3);
    }
}
