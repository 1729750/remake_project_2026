using System;
using Unity.Netcode;
using UnityEngine;

public sealed class MultiPlayGamePresenter : MonoBehaviour
{
    [SerializeField]
    private GameNetworkState gameNetworkState;

    public bool IsReady { get; private set; }

    public ulong MyClientId { get; private set; } =
        GameNetworkState.UnassignedClientId;

    public ulong EnemyClientId { get; private set; } =
        GameNetworkState.UnassignedClientId;

    public bool LocalIsPlayer0 { get; private set; }

    public int MyMaxHealth { get; private set; }
    public int MyHealth { get; private set; }
    public int MyDefense { get; private set; }
    public int MyCost { get; private set; }
    public bool MyGuard { get; private set; }

    public int EnemyMaxHealth { get; private set; }
    public int EnemyHealth { get; private set; }
    public int EnemyDefense { get; private set; }
    public int EnemyCost { get; private set; }
    public bool EnemyGuard { get; private set; }

    public event Action ViewStateChanged;

    private void OnEnable()
    {
        if (gameNetworkState != null)
            gameNetworkState.StateChanged += Refresh;
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (gameNetworkState != null)
            gameNetworkState.StateChanged -= Refresh;
    }

    public void Refresh()
    {
        IsReady = false;

        if (gameNetworkState == null ||
            !gameNetworkState.IsSpawned ||
            !gameNetworkState.PlayersAssigned ||
            NetworkManager.Singleton == null)
        {
            return;
        }

        ulong localId = NetworkManager.Singleton.LocalClientId;
        ulong player0Id = gameNetworkState.Player0ClientId.Value;
        ulong player1Id = gameNetworkState.Player1ClientId.Value;

        MyClientId = localId;

        if (localId == player0Id)
        {
            LocalIsPlayer0 = true;
            EnemyClientId = player1Id;
        }
        else if (localId == player1Id)
        {
            LocalIsPlayer0 = false;
            EnemyClientId = player0Id;
        }
        else
        {
            Debug.LogError(
                $"[MultiPlayGamePresenter] LocalClientId {localId}가 Player0/Player1에 없습니다.");
            return;
        }

        if (!gameNetworkState.TryGetCharacterPublicState(
                MyClientId,
                out int myMaxHealth,
                out int myHealth,
                out int myDefense,
                out int myCost,
                out bool myGuard))
        {
            return;
        }

        if (!gameNetworkState.TryGetCharacterPublicState(
                EnemyClientId,
                out int enemyMaxHealth,
                out int enemyHealth,
                out int enemyDefense,
                out int enemyCost,
                out bool enemyGuard))
        {
            return;
        }

        MyMaxHealth = myMaxHealth;
        MyHealth = myHealth;
        MyDefense = myDefense;
        MyCost = myCost;
        MyGuard = myGuard;

        EnemyMaxHealth = enemyMaxHealth;
        EnemyHealth = enemyHealth;
        EnemyDefense = enemyDefense;
        EnemyCost = enemyCost;
        EnemyGuard = enemyGuard;

        IsReady = true;
        ViewStateChanged?.Invoke();
    }
}
