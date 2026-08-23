using System.Collections;
using UnityEngine;

// GameManager.GameState가 바뀔 때의 화면 전환 연출을 전담한다. BattleManager/RewardManager
// (RewardPanel)/MapManager는 서로의 활성화·연출을 직접 건드리지 않고, GameManager.SetGameState가
// 상태를 바꿀 때마다 HandleStateChange를 호출해 여기로 위임한다.
//
// 다루는 연출은 다섯 가지다:
// 1) ... → BattleEnd: rewardManager(RewardPanel)가 화면 위에서 아래(원래 자리)로 내려온다.
//    battleManager는 계속 켜진 채로 뒤에 그대로 비친다.
// 2) BattleEnd → SelectEnemy: battleManager와 rewardManager가 동시에 아래로 내려가 화면 밖으로
//    사라지고, 그와 동시에 mapManager가 위에서 내려와 자리를 채운다. 셋 다 같은 방향·속도로
//    내려갈 뿐이지만, 마치 카메라가 위로 올라가면서 mapManager의 배경이 battleManager 위에
//    쌓여 있던 것처럼 보이는 착시를 만든다.
// 3) SelectEnemy → Battle: 2번의 반대 방향. mapManager가 위로 올라가 화면 밖으로 사라지고,
//    그와 동시에 battleManager가 아래에서 위로 올라와 자리를 채운다 — 카메라가 다시 아래로
//    내려가는 듯한 착시.
// 4) ... → GameOver: 2번(PlayMapReveal)과 같은 패턴. battleManager가 아래로 내려가 화면 밖으로
//    사라지고, 그와 동시에 gameEndManager가 위에서 내려와 자리를 채운다.
// 5) GameOver → (StartScreen/SelectEnemy 등): 화면 암전. 실제 페이드 구현은 아직 없고
//    PlayBlackout에 자리만 마련해뒀다(지금은 즉시 SnapToState).
// 그 외의 전환은 연출 없이 즉시 SetActive로 전환한다.
public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    [SerializeField] private BattleManager battleManager;
    [SerializeField] private RewardManager rewardManager;
    [SerializeField] private MapManager mapManager;
    [SerializeField] private GameEndManager gameEndManager;
    [SerializeField] private float rewardDropDuration = 0.5f;
    [SerializeField] private float mapRevealDuration = 0.6f;
    [SerializeField] private float battleRevealDuration = 0.6f;
    [SerializeField] private float gameEndDropDuration = 0.5f;

    private Transform _battleTransform;
    private Transform _rewardTransform;
    private Transform _mapTransform;
    private Transform _gameEndTransform;

    private Vector3 _battleRestPosition;
    private Vector3 _rewardRestPosition;
    private Vector3 _mapRestPosition;
    private Vector3 _gameEndRestPosition;

    // 한 화면 높이(월드 단위). 이만큼 위/아래로 옮기면 무엇이든 화면 밖으로 완전히 벗어난다.
    private float _travelDistance;

    private Coroutine _routine;
    // _routine이 현재 진행 중이라면, 그 애니메이션이 끝났을 때 도달했어야 할 GameState.
    // 애니메이션 도중 새 전환이 끼어들어 _routine을 StopCoroutine으로 끊으면, 코루틴 끝에서
    // 하려던 enable/disable(SetActive)이 실행되지 못하고 그대로 유실된다. 그래서 끊기 직전에
    // 이 값으로 SnapToState를 한 번 호출해, 유실될 뻔한 enable/disable을 먼저 확정 반영한다.
    private GameState _pendingState;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (battleManager == null || rewardManager == null || mapManager == null || gameEndManager == null)
        {
            Debug.LogWarning("[TransitionManager] battleManager/rewardManager/mapManager/gameEndManager 중 비어있는 참조가 있습니다.");
            return;
        }

        _battleTransform = battleManager.transform;
        _rewardTransform = rewardManager.transform;
        _mapTransform = mapManager.transform;
        _gameEndTransform = gameEndManager.transform;

        _battleRestPosition = _battleTransform.position;
        _rewardRestPosition = _rewardTransform.position;
        _mapRestPosition = _mapTransform.position;
        _gameEndRestPosition = _gameEndTransform.position;

        Camera cam = Camera.main;
        _travelDistance = cam != null && cam.orthographic ? cam.orthographicSize * 2f : 10f;
    }

    // GameManager.GameStart가 새 런을 시작할 때 호출한다. 진행 중이던 연출을 멈추고, 모든 매니저를
    // 각자의 rest position으로 되돌려(현재 GameState 기준으로 켜고 끄기까지 포함) 다음 상태 전환이
    // 항상 정상적인 자리에서 시작하도록 보장한다.
    public void Init()
    {
        if (_battleTransform == null) return;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        GameState state = GameManager.Instance.GetGameState();
        SnapToState(state);
        _pendingState = state;
    }

    // GameManager.SetGameState(previous → next)가 바뀔 때마다 호출된다.
    public void HandleStateChange(GameState previous, GameState next)
    {
        if (_battleTransform == null) return;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
            // 방금 끊은 애니메이션이 자연스럽게 끝났다면 반영했을 enable/disable을 먼저 즉시
            // 확정한다 — 이 다음에 이어지는 next용 enable/disable에 덮어씌워지기 전에 처리해야 한다.
            SnapToState(_pendingState);
        }

        _pendingState = next;

        if (next == GameState.BattleEnd)
        {
            _routine = StartCoroutine(PlayRewardDropIn());
        }
        else if (previous == GameState.BattleEnd && next == GameState.SelectEnemy)
        {
            _routine = StartCoroutine(PlayMapReveal());
        }
        else if (previous == GameState.SelectEnemy && next == GameState.Battle)
        {
            _routine = StartCoroutine(PlayBattleReveal());
        }
        else if (next == GameState.GameOver)
        {
            _routine = StartCoroutine(PlayGameEndDropIn());
        }
        else if (previous == GameState.GameOver)
        {
            _routine = StartCoroutine(PlayBlackout(next));
        }
        else
        {
            SnapToState(next);
        }
    }

    // 별도 연출이 없는 전환(예: SelectEnemy → Battle)의 즉시 처리. 애니메이션 없이 켜고 끄며,
    // 넷 다 각자의 rest position으로 되돌려 다음 연출이 항상 같은 자리에서 시작하게 한다.
    private void SnapToState(GameState state)
    {
        bool battleActive = state == GameState.Battle || state == GameState.BattleEnd;
        bool mapActive = state == GameState.SelectEnemy;
        bool rewardActive = state == GameState.BattleEnd;
        bool gameEndActive = state == GameState.GameOver;

        _battleTransform.position = _battleRestPosition;
        _mapTransform.position = _mapRestPosition;
        _rewardTransform.position = _rewardRestPosition;
        _gameEndTransform.position = _gameEndRestPosition;

        battleManager.gameObject.SetActive(battleActive);
        mapManager.gameObject.SetActive(mapActive);
        rewardManager.gameObject.SetActive(rewardActive);
        gameEndManager.gameObject.SetActive(gameEndActive);
    }

    private IEnumerator PlayRewardDropIn()
    {
        Vector3 start = _rewardRestPosition + Vector3.up * _travelDistance;
        _rewardTransform.position = start;
        rewardManager.gameObject.SetActive(true);

        yield return MoveOverTime(_rewardTransform, start, _rewardRestPosition, rewardDropDuration);
        _routine = null;
    }

    private IEnumerator PlayMapReveal()
    {
        Vector3 battleStart = _battleTransform.position;
        Vector3 battleEnd = _battleRestPosition - Vector3.up * _travelDistance;
        Vector3 rewardStart = _rewardTransform.position;
        Vector3 rewardEnd = _rewardRestPosition - Vector3.up * _travelDistance;

        Vector3 mapStart = _mapRestPosition + Vector3.up * _travelDistance;
        _mapTransform.position = mapStart;
        mapManager.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < mapRevealDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / mapRevealDuration));
            _battleTransform.position = Vector3.Lerp(battleStart, battleEnd, t);
            _rewardTransform.position = Vector3.Lerp(rewardStart, rewardEnd, t);
            _mapTransform.position = Vector3.Lerp(mapStart, _mapRestPosition, t);
            yield return null;
        }

        battleManager.gameObject.SetActive(false);
        rewardManager.gameObject.SetActive(false);
        _mapTransform.position = _mapRestPosition;

        // 꺼진 뒤에는 보이지 않으므로, 다음에 다시 켜질 때를 위해 rest position으로 되돌려둔다.
        _battleTransform.position = _battleRestPosition;
        _rewardTransform.position = _rewardRestPosition;

        _routine = null;
    }

    // PlayMapReveal의 반대 방향: mapManager는 위로 올라가 화면 밖으로 사라지고(꺼진 뒤 rest position
    // 으로 되돌려둔다), battleManager는 아래에서 위로 올라와 자리를 채운다. rewardManager는 이 시점에
    // 이미 꺼져 있으므로 건드리지 않는다.
    private IEnumerator PlayBattleReveal()
    {
        Vector3 mapStart = _mapTransform.position;
        Vector3 mapEnd = _mapRestPosition + Vector3.up * _travelDistance;

        Vector3 battleStart = _battleRestPosition - Vector3.up * _travelDistance;
        _battleTransform.position = battleStart;
        battleManager.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < battleRevealDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / battleRevealDuration));
            _mapTransform.position = Vector3.Lerp(mapStart, mapEnd, t);
            _battleTransform.position = Vector3.Lerp(battleStart, _battleRestPosition, t);
            yield return null;
        }

        mapManager.gameObject.SetActive(false);
        _mapTransform.position = _mapRestPosition;
        _battleTransform.position = _battleRestPosition;

        _routine = null;
    }

    // PlayMapReveal과 같은 패턴: battleManager가 아래로 내려가 화면 밖으로 사라지는 동안,
    // gameEndManager가 위에서 rest position으로 내려와 자리를 채운다.
    private IEnumerator PlayGameEndDropIn()
    {
        Vector3 battleStart = _battleTransform.position;
        Vector3 battleEnd = _battleRestPosition - Vector3.up * _travelDistance;

        Vector3 gameEndStart = _gameEndRestPosition + Vector3.up * _travelDistance;
        _gameEndTransform.position = gameEndStart;
        gameEndManager.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < gameEndDropDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / gameEndDropDuration));
            _battleTransform.position = Vector3.Lerp(battleStart, battleEnd, t);
            _gameEndTransform.position = Vector3.Lerp(gameEndStart, _gameEndRestPosition, t);
            yield return null;
        }

        battleManager.gameObject.SetActive(false);
        _battleTransform.position = _battleRestPosition;
        _gameEndTransform.position = _gameEndRestPosition;

        _routine = null;
    }

    // 타이틀 복귀/게임 재시작(=GameOver를 벗어나는 모든 전환)에 쓸 암전 연출.
    // TODO: 실제 페이드 아웃 → 대기 → 페이드 인 구현. 지금은 연출 없이 즉시 상태만 맞춘다.
    private IEnumerator PlayBlackout(GameState next)
    {
        SnapToState(next);
        yield return null;
        _routine = null;
    }

    private static IEnumerator MoveOverTime(Transform target, Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            target.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        target.position = to;
    }
}
