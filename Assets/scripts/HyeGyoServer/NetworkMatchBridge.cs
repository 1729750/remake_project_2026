using UnityEngine;
using Unity.Netcode;

public class NetworkMatchBridge : NetworkBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private PlayerInputManager playerInputManager;
}