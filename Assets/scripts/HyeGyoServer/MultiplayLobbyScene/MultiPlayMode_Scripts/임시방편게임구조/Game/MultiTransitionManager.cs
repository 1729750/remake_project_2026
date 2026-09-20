using UnityEngine;

public sealed class MultiTransitionManager : MonoBehaviour
{
    [Header("Network")]
    [SerializeField]
    private GameNetworkState gameNetworkState;

    [Header("View Roots")]
    [SerializeField]
    private GameObject battleRoot;

    [SerializeField]
    private GameObject resultRoot;

    private void OnEnable()
    {
        if (gameNetworkState != null)
        {
            gameNetworkState.StateChanged +=
                Refresh;
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
            gameNetworkState.StateChanged -=
                Refresh;
        }
    }

    private void Refresh()
    {
        if (gameNetworkState == null)
            return;

        if (!gameNetworkState.IsSpawned)
            return;

        MultiMatchState state =
            gameNetworkState.MatchState.Value;

        switch (state)
        {
            case MultiMatchState.WaitingForPlayers:

                SetActiveSafe(
                    battleRoot,
                    false
                );

                SetActiveSafe(
                    resultRoot,
                    false
                );

                break;

            case MultiMatchState.BattleStarting:
            case MultiMatchState.Battle:

                SetActiveSafe(
                    battleRoot,
                    true
                );

                SetActiveSafe(
                    resultRoot,
                    false
                );

                break;

            case MultiMatchState.BattleFinished:

                SetActiveSafe(
                    battleRoot,
                    true
                );

                SetActiveSafe(
                    resultRoot,
                    true
                );

                break;
        }
    }

    private static void SetActiveSafe(
        GameObject target,
        bool active)
    {
        if (target != null &&
            target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}