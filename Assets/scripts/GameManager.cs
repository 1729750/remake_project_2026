using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private CharacterManager playerCharacterManager;
    [SerializeField] private CharacterManager enemyCharacterManager;
    [SerializeField] private TextMeshPro turnText;
    [SerializeField] private float turnDuration = 1f;
    [SerializeField] private float startDelay = 3f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        var overlayGO = new GameObject("TurnTimerOverlay");
        overlayGO.transform.SetParent(turnText.transform.parent);
        overlayGO.transform.localPosition = Vector3.zero;
        overlayGO.transform.localScale = Vector3.one;
        overlayGO.AddComponent<MeshFilter>();
        overlayGO.AddComponent<MeshRenderer>();
        var turnTimerOverlay = overlayGO.AddComponent<TurnTimerOverlay>();

        var turnManager = new TurnManager(turnDuration, turnTimerOverlay, turnText);
        new BattleManager(playerCharacterManager, enemyCharacterManager, turnManager, turnTimerOverlay, startDelay).StartBattle();
    }

    void Update()
    {
        BattleManager.Instance.Tick(Time.deltaTime);
    }
}
