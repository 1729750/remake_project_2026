using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : InputManager
{
    public static PlayerInputManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
