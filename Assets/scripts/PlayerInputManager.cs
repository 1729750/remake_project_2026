using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : InputManager
{
    
    public static PlayerInputManager Instance {get; private set;}
    
    private InputAction _playCard1;
    private InputAction _playCard2;
    private InputAction _playCard3;
    private InputAction _playCard4;

    private InputAction _selectLeft;
    private InputAction _selectRight;
    private InputAction _selectConfirm;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    public override void LoadBattleInputActions()
    {
        if (_playCard1 != null) return;
        if (inputActions == null) return;

        var map = inputActions.FindActionMap("Battle", throwIfNotFound: true);
        _playCard1 = map.FindAction("PlayCard1", throwIfNotFound: true);
        _playCard2 = map.FindAction("PlayCard2", throwIfNotFound: true);
        _playCard3 = map.FindAction("PlayCard3", throwIfNotFound: true);
        _playCard4 = map.FindAction("PlayCard4", throwIfNotFound: true);
        inputActions.Enable();
    }

    protected override void HandleBattleInput()
    {
        if (BattleManager.Instance == null || BattleManager.Instance.CurrentState != BattleState.Turn) return;

        if (_playCard1 == null) return;
        if (_playCard1.triggered)      playerCharacterManager.SelectCard(0);
        else if (_playCard2.triggered) playerCharacterManager.SelectCard(1);
        else if (_playCard3.triggered) playerCharacterManager.SelectCard(2);
        else if (_playCard4.triggered) playerCharacterManager.SelectCard(3);
    }

    public override void LoadSelectInputActions()
    {
        if (_selectLeft != null) return;
        if (inputActions == null) return;

        var map = inputActions.FindActionMap("Select", throwIfNotFound: true);
        _selectLeft = map.FindAction("Left", throwIfNotFound: true);
        _selectRight = map.FindAction("Right", throwIfNotFound: true);
        _selectConfirm = map.FindAction("Select", throwIfNotFound: true);
        inputActions.Enable();
    }

    protected override void HandleSelectInput()
    {
        if (BattleManager.Instance == null) return;

        if (_selectLeft == null) return;
        if (_selectLeft.triggered)         BattleManager.Instance.MoveRewardSelection(-1);
        else if (_selectRight.triggered)   BattleManager.Instance.MoveRewardSelection(1);
        else if (_selectConfirm.triggered) GameManager.Instance.ConfirmReward();
    }
}
