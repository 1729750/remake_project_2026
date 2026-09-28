using UnityEngine;

public class PlayerInputManager_Multi : InputManager
{
    public static PlayerInputManager_Multi Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}
