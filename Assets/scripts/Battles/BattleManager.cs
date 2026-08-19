using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BattleManager:MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [SerializeField] private CharacterManager playerCharacterManager;
    [SerializeField] private CharacterManager enemyCharacterManager;
    [SerializeField] private TextMeshPro turnText;
    [SerializeField] private float turnDuration = 1f;
    [SerializeField] private float startDelay = 3f;
    [SerializeField] private List<EffectEmoji> effectEmojis = new List<EffectEmoji>();

    private TurnManager _turnManager;
    private TurnTimerOverlay _turnTimerOverlay;
    private float _startElapsed;
    private static Dictionary<EffectType, Sprite> _emojiCache;

    // BattleStarting 카운트다운 중 1초마다, 그리고 실제 턴이 끝날 때마다 TurnEnd1/TurnEnd2를
    // 번갈아 재생하기 위한 상태. 전투가 끝나면(NotifyDefeat) 다음 전투를 위해 리셋된다.
    private int _startTickCount;
    private bool _nextTurnEndIsFirst = true;

    public BattleState CurrentState { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // GameManager.Start(최초 1회)와 GameManager.GameStart(매 런 재시작마다) 둘 다에서 호출된다.
    // 재호출 시 이전 TurnTimerOverlay가 남아있으면 먼저 파괴한다 — 그렇지 않으면 재시작할 때마다
    // 오버레이 오브젝트가 하나씩 쌓인다.
    public void Init()
    {
        playerCharacterManager.Init();
        enemyCharacterManager.Init();

        if (_turnTimerOverlay != null)
            Destroy(_turnTimerOverlay.gameObject);

        var overlayGO = new GameObject("TurnTimerOverlay");
        overlayGO.transform.SetParent(turnText.transform.parent);
        overlayGO.transform.localPosition = Vector3.zero;
        overlayGO.transform.localScale = Vector3.one;
        overlayGO.AddComponent<MeshFilter>();
        overlayGO.AddComponent<MeshRenderer>();
        _turnTimerOverlay = overlayGO.AddComponent<TurnTimerOverlay>();
        _turnManager = new TurnManager(turnDuration, _turnTimerOverlay, turnText);

        _turnManager.OnTurnStarted += OnTurnStarted;
        _turnManager.OnTurnEnded += OnTurnEnded;
    }

    public void StartBattle(CharacterData playerData, CharacterData enemyData)
    {
        playerCharacterManager.CharacterInit(UnpackCardCollection(playerData.GetDeck()).ToArray(), playerData.GetMaxHealth());
        enemyCharacterManager.CharacterInit(UnpackCardCollection(enemyData.GetDeck()).ToArray(), enemyData.GetMaxHealth());

        _turnManager.Reset();
        _startElapsed = 0f;
        _startTickCount = 0;
        _turnTimerOverlay?.SetFill(0f);
        SetState(BattleState.BattleStarting);
    }

    // CardCollection 에셋을 런타임 덱으로 풀어낸다. 원본 에셋이 오염되지 않도록
    // 각 CardDefinition을 Instantiate로 깊은 복사해서 반환한다.
    public static List<CardDefinition> UnpackCardCollection(CardCollection collection)
    {
        var result = new List<CardDefinition>();
        if (collection == null) return result;
        foreach (var def in collection.GetCards())
            if (def != null)
                result.Add(Instantiate(def));
        return result;
    }

    public void Tick(float deltaTime)
    {
        if (CurrentState == BattleState.BattleStarting)
        {
            _startElapsed += deltaTime;

            int wholeSecondsElapsed = Mathf.FloorToInt(Mathf.Min(_startElapsed, startDelay));
            while (_startTickCount < wholeSecondsElapsed)
            {
                _startTickCount++;
                PlayAlternatingTurnEnd();
            }

            if (_startElapsed >= startDelay)
            {
                _turnTimerOverlay?.SetFill(_startElapsed / startDelay);
                OnBattleStarted();
            }
        }
        else if (CurrentState == BattleState.Turn)
        {
            _turnManager.Tick(deltaTime);
        }
    }

    private void SetState(BattleState state)
    {
        CurrentState = state;
    }

    private void OnBattleStarted()
    {
        SoundManager.Instance?.Play(EffectSound.BattleStart);
        SoundManager.Instance?.Play(BgmName.BattleBGM);
        _turnManager.StartTurn();
    }

    private void OnTurnStarted()
    {
        SetState(BattleState.TurnStart);
        Debug.Log($"Turn {_turnManager.GetCurrentTurn()} Started");
        playerCharacterManager.OnTurnStart();
        enemyCharacterManager.OnTurnStart();
        SetState(BattleState.Turn);
    }

    private void OnTurnEnded()
    {
        SetState(BattleState.TurnEnd);
        Debug.Log($"Turn {_turnManager.GetCurrentTurn()} Ended");
        PlayAlternatingTurnEnd();
        playerCharacterManager.OnTurnEnd();
        enemyCharacterManager.OnTurnEnd();
        if (CurrentState != BattleState.BattleFinish)
        {
            _turnManager.StartTurn();
        }
    }

    // BattleStarting 카운트다운의 1초 틱과 실제 턴 종료가 같은 토글을 공유해 TurnEnd1/2를 번갈아 재생한다.
    private void PlayAlternatingTurnEnd()
    {
        SoundManager.Instance?.Play(_nextTurnEndIsFirst ? EffectSound.TurnEnd1 : EffectSound.TurnEnd2);
        _nextTurnEndIsFirst = !_nextTurnEndIsFirst;
    }

    public void NotifyDefeat(CharacterManager loser)
    {
        if (CurrentState != BattleState.BattleFinish)
        {
            SetState(BattleState.BattleFinish);
            _nextTurnEndIsFirst = true;
            playerCharacterManager.ClearHandAndQueue();
            enemyCharacterManager.ClearHandAndQueue();
            // battleBGM은 전투가 끝나는 이 시점부터 다음 BattleStart(OnBattleStarted가 다시
            // BgmName.BattleBGM을 틀 때)까지 나오지 않아야 하므로 여기서 완전히 멈춘다.
            SoundManager.Instance?.StopBgm();
            if (loser == playerCharacterManager)
            {
                GameManager.Instance.GameOver();
            }
            else
            {
                SoundManager.Instance?.Play(EffectSound.PlayerWin);
                // menu BGM은 playerWin 효과음이 끝난 뒤부터 틀어야 하므로 그 클립 길이만큼 예약한다.
                float delay = SoundManager.Instance != null
                    ? SoundManager.Instance.GetEffectClipLength(EffectSound.PlayerWin)
                    : 0f;
                SoundManager.Instance?.PlayBgmDelayed(BgmName.Menu, delay);
                GameManager.Instance.EndBattle();
            }
        }
    }

    public CharacterManager GetOpponent(CharacterManager user)
    {
        return user == playerCharacterManager ? enemyCharacterManager : playerCharacterManager;
    }

    public static Sprite GetEmoji(EffectType effectType)
    {
        if (_emojiCache == null)
        {
            _emojiCache = new Dictionary<EffectType, Sprite>();
            foreach (var entry in Instance.effectEmojis)
                _emojiCache[entry.effectType] = entry.sprite;
        }
        _emojiCache.TryGetValue(effectType, out var sprite);
        return sprite;
    }

    public void Update()
    {
        if(GameManager.Instance.GetGameState() == GameState.Battle)
            Tick(Time.deltaTime);
    }
}

public enum BattleState
{
    BattleStarting,
    TurnStart,
    Turn,
    TurnEnd,
    BattleFinish
}

[System.Serializable]
public class EffectEmoji
{
    public EffectType effectType;
    public Sprite sprite;
}
