using Unity.Netcode;
using UnityEngine;

public sealed class MultiPlayGamePresenter : MonoBehaviour
{
    [SerializeField]
    private GameNetworkState gameNetworkState;

    public bool IsReady { get; private set; }

    public ulong MyClientId { get; private set; }
    public ulong EnemyClientId { get; private set; }

    public bool LocalIsPlayer0 { get; private set; }

    public int MyHealth { get; private set; }
    public int EnemyHealth { get; private set; }

    private void OnEnable()
    {
        if (gameNetworkState != null)
        {
            gameNetworkState.StateChanged += Refresh;
        }
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (gameNetworkState != null)
        {
            gameNetworkState.StateChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        if (gameNetworkState == null)
            return;

        if (!gameNetworkState.IsSpawned)
            return;

        if (!gameNetworkState.PlayersAssigned)
            return;

        if (NetworkManager.Singleton == null)
            return;

        ulong localId =
            NetworkManager.Singleton.LocalClientId;

        ulong player0Id =
            gameNetworkState.Player0ClientId.Value;

        ulong player1Id =
            gameNetworkState.Player1ClientId.Value;

        MyClientId = localId;

        if (localId == player0Id)
        {
            LocalIsPlayer0 = true;

            EnemyClientId = player1Id;

            MyHealth =
                gameNetworkState.Player0Health.Value;

            EnemyHealth =
                gameNetworkState.Player1Health.Value;
        }
        else if (localId == player1Id)
        {
            LocalIsPlayer0 = false;

            EnemyClientId = player0Id;

            MyHealth =
                gameNetworkState.Player1Health.Value;

            EnemyHealth =
                gameNetworkState.Player0Health.Value;
        }
        else
        {
            Debug.LogError(
                $"[MultiPlayGamePresenter] " +
                $"LocalClientId {localId}가 Player0/Player1에 없습니다."
            );

            IsReady = false;
            return;
        }

        IsReady = true;

        Debug.Log(
            "[MultiPlayGamePresenter]\n" +
            $"MyClientId: {MyClientId}\n" +
            $"EnemyClientId: {EnemyClientId}\n" +
            $"Role: {(LocalIsPlayer0 ? "Player0" : "Player1")}\n" +
            $"MyHealth: {MyHealth}\n" +
            $"EnemyHealth: {EnemyHealth}"
        );
    }
}