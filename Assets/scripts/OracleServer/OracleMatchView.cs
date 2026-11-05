using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// MultiPlayMode_Oracle 씬의 진행 담당.
/// MultiplayLobby_Oracle 에서 방을 잡고 넘어온 뒤, 오라클 서버가 보내는 state에 따라
/// SinglePlayMode와 같은 화면을 켜고 끈다.
///   picking   → 전투 HUD를 숨기고 RewardPanel(OracleCardPickView): 내 덱, 남은 횟수, 선택지 3장, 건너뛰기
///   countdown → TurnText 카운트다운 + 턴 타이머 (싱글 전투 시작과 같음)
///   battle    → Player/Enemy(OracleCharacterView)를 서버 값으로 그림, 싱글과 같은 키(Q W E R / D / F)
///               Tab: 현재 덱 보기 (DeckDisplay, 화면의 4/5 크기)
///   finished  → TurnText에 승리/패배/무승부 → GameEndManager(OracleGameEndView) 다시하기/나가기
/// 게임 판정은 전부 서버가 하고, 여기서는 입력만 보낸다.
/// </summary>
public sealed class OracleMatchView : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private GameObject battleRoot;
    [Tooltip("카드 선택 중에는 숨길 전투 화면 요소 (Player, Enemy, turnDisplay)")]
    [SerializeField] private GameObject[] battleHud;
    [SerializeField] private TextMeshPro turnText;
    [SerializeField] private OracleCharacterView me;
    [SerializeField] private OracleCharacterView opponent;
    [SerializeField] private OracleCardPickView pickView;
    [SerializeField] private OracleGameEndView gameEndView;

    [Header("Cards")]
    [Tooltip("서버 카드 이름 → CardDefinition 에셋 (그림/카드 종류 배경)")]
    [SerializeField] private CardDefinition[] cardLibrary;

    [Tooltip("TurnText에 승리/패배를 쓸 때 잠시 바꿔 쓸 한글 폰트 (TurnText 기본 폰트에는 한글이 없다)")]
    [SerializeField] private TMP_FontAsset resultFont;

    [Header("Deck View (Tab)")]
    [SerializeField, Range(0.3f, 1f)] private float deckViewScreenRatio = 0.8f;

    [Header("Timing")]
    [SerializeField] private float resultDelay = 2.5f;

    private OracleServerClient client;
    private OracleCardLibrary library;
    private TurnTimerOverlay turnTimer;

    private MatchStateView state;
    private float stateReceivedAt;
    private string shownPhase;
    private bool battleInputLoaded;
    private Coroutine resultRoutine;
    private DeckDisplay deckView;
    private TMP_FontAsset turnTextFont;
    private bool nextTurnEndIsFirst = true;
    private int countdownShown;
    private int warnedTurn = -1;

    private void Awake()
    {
        library = new OracleCardLibrary(cardLibrary);
        CreateTurnTimer();
        if (turnText != null) turnTextFont = turnText.font;

        if (pickView != null)
        {
            pickView.Picked += index => client?.Send(ClientMessage.Pick(index));
            pickView.Skipped += () => client?.Send(ClientMessage.Skip());
        }
        if (gameEndView != null)
        {
            gameEndView.RematchSelected += () => client?.Send(ClientMessage.Rematch());
            gameEndView.ExitSelected += Leave;
        }
    }

    private void Start()
    {
        client = OracleServerClient.GetOrCreate();

        // 로비를 거치지 않고 이 씬을 바로 실행했으면 로비로 보낸다
        if (!client.InRoom)
        {
            ReturnToLobby();
            return;
        }

        client.MessageReceived += HandleMessage;
        client.Disconnected += ReturnToLobby;

        // 로비 씬에서 넘어오는 사이에 도착한 첫 state를 이어받는다
        if (client.LastState != null)
            HandleMessage(client.LastState);
        else if (turnText != null)
            turnText.text = "";
    }

    private void OnDestroy()
    {
        UnloadBattleInput();
        if (client == null) return;
        client.MessageReceived -= HandleMessage;
        client.Disconnected -= ReturnToLobby;
    }

    private void Update()
    {
        if (state == null) return;

        float timeLeft = Mathf.Max(0f, state.timeLeft - (Time.unscaledTime - stateReceivedAt));

        switch (state.phase)
        {
            case "picking":
                pickView?.SetTimeLeft(timeLeft);
                break;

            case "countdown":
                int second = Mathf.Max(1, Mathf.CeilToInt(timeLeft));
                // 싱글 BattleManager처럼 카운트다운 1초마다 턴 종료음
                if (second != countdownShown)
                {
                    countdownShown = second;
                    PlayAlternatingTurnEnd();
                }
                SetTurnText(second.ToString());
                turnTimer?.SetFill(1f - timeLeft / 3f);
                break;

            case "battle":
                turnTimer?.SetFill(state.turnDuration > 0 ? 1f - timeLeft / state.turnDuration : 0f);
                if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                    ToggleDeckView();
                break;
        }
    }

    // =========================================================
    // Deck View (Tab)
    // =========================================================

    private void ToggleDeckView()
    {
        if (deckView != null)
        {
            CloseDeckView();
            return;
        }

        Camera cam = Camera.main;
        if (cam == null || state == null) return;

        float height = cam.orthographicSize * 2f * deckViewScreenRatio;
        float width = height * cam.aspect;
        var center = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
        deckView = library.ShowDeck(null, transform, center, new Vector2(width, height), state.myDeck);
    }

    private void CloseDeckView()
    {
        if (deckView != null) Destroy(deckView.gameObject);
        deckView = null;
    }

    private void SetBattleHudVisible(bool visible)
    {
        if (battleHud == null) return;
        foreach (GameObject hud in battleHud)
            if (hud != null) hud.SetActive(visible);
    }

    // BattleManager.Init이 만들던 것과 같은 턴 타이머(파이 모양)를 TurnText 자리에 만든다
    private void CreateTurnTimer()
    {
        if (turnText == null) return;
        var overlay = new GameObject("TurnTimerOverlay");
        overlay.transform.SetParent(turnText.transform.parent);
        overlay.transform.localPosition = Vector3.zero;
        overlay.transform.localScale = Vector3.one;
        overlay.AddComponent<MeshFilter>();
        overlay.AddComponent<MeshRenderer>();
        turnTimer = overlay.AddComponent<TurnTimerOverlay>();
        turnTimer.SetFill(0f);
    }

    // =========================================================
    // Server Messages
    // =========================================================

    private void HandleMessage(ServerMessage message)
    {
        switch (message.type)
        {
            case "state":
                state = message.state;
                // 서버가 이 state를 만든 건 왕복 시간의 절반쯤 전이다. 그만큼 타이머를 앞당겨 두 화면을 맞춘다
                stateReceivedAt = Time.unscaledTime - client.RoundTripTime / 2f;
                Render();
                break;

            case "player_left":
                // 결과 화면이면 그대로 두고(나가기만 가능), 게임 도중이면 방을 정리하고 로비로
                if (state == null || state.phase != "finished")
                    client.Send(ClientMessage.LeaveRoom());
                break;

            case "room_left":
                ReturnToLobby();
                break;
        }
    }

    private void Leave()
    {
        if (client != null && client.IsConnected && client.InRoom)
            client.Send(ClientMessage.LeaveRoom()); // room_left를 받으면 로비로
        else
            ReturnToLobby();
    }

    private void ReturnToLobby()
    {
        UnloadBattleInput();
        SoundManager.Instance?.StopBgm();
        SceneManager.LoadScene(OracleScenes.Lobby);
    }

    // =========================================================
    // Render
    // =========================================================

    private void Render()
    {
        bool phaseChanged = state.phase != shownPhase;
        shownPhase = state.phase;

        switch (state.phase)
        {
            case "picking":
                if (phaseChanged) EnterPicking();
                pickView?.Show(state, library);
                break;

            case "countdown":
                if (phaseChanged)
                {
                    pickView?.Hide();
                    SetBattleHudVisible(true);
                    SetTurnText("");
                }
                break;

            case "battle":
                if (phaseChanged) EnterBattle();
                RenderCombatants();
                WarnUpcomingAttack();
                break;

            case "finished":
                if (phaseChanged) EnterFinished();
                break;
        }

        PlayEventSounds(state.events);
    }

    // =========================================================
    // Sounds (싱글에서 같은 순간에 나던 소리를 서버 이벤트로 낸다)
    // =========================================================

    private void PlayEventSounds(EventView[] events)
    {
        SoundManager sound = SoundManager.Instance;
        if (events == null || sound == null) return;

        foreach (EventView e in events)
        {
            switch (e.kind)
            {
                case "use": // HandManager.UseCardImmediately
                    if (e.mine) sound.Play(e.ok ? EffectSound.UseCard : EffectSound.PlayDenied);
                    break;

                case "play": // CardInstance.Play
                    sound.Play(EffectSound.PlayCard);
                    if (e.ok) sound.Play(EffectSound.Attack);
                    break;

                case "damage": // CharacterManager.PlayDamageSound
                    sound.Play(e.guarded ? EffectSound.DamageGuarded
                        : e.amount <= 10 ? EffectSound.DamageWeak
                        : e.amount <= 30 ? EffectSound.Damage
                        : EffectSound.DamageBig);
                    break;

                case "shield": // CharacterManager.ConsumeQueuedDefense
                    sound.Play(EffectSound.DamageShielded);
                    break;

                case "effect": // Effect.PlayApplySound
                    if (Enum.TryParse(e.effect, out EffectType type))
                        sound.Play(ApplySound(type));
                    break;

                case "turnEnd": // BattleManager.PlayAlternatingTurnEnd
                    PlayAlternatingTurnEnd();
                    break;
            }
        }
    }

    private static EffectSound ApplySound(EffectType type)
    {
        switch (type)
        {
            case EffectType.Burning: return EffectSound.Burn;
            case EffectType.Defend: return EffectSound.ShieldGet;
            case EffectType.TimeSkip:
            case EffectType.Quicker: return EffectSound.TimeSkip;
            default:
                return Effect.Create(type, 0).TargetPolarity == EffectTargetPolarity.Positive ? EffectSound.Buff : EffectSound.Debuff;
        }
    }

    private void PlayAlternatingTurnEnd()
    {
        SoundManager.Instance?.Play(nextTurnEndIsFirst ? EffectSound.TurnEnd1 : EffectSound.TurnEnd2);
        nextTurnEndIsFirst = !nextTurnEndIsFirst;
    }

    // CharacterManager.PlayUpcomingAttackSoundIfNeeded: 턴 시작 때 상대 큐에 곧 나갈 공격 카드가 있으면 경고음
    private void WarnUpcomingAttack()
    {
        if (state.turn == warnedTurn || state.opponent?.queue == null) return;
        warnedTurn = state.turn;

        foreach (CardView queued in state.opponent.queue)
        {
            if (queued.cooldownLeft > 1 || queued.effects == null) continue;
            foreach (CardEffectView effect in queued.effects)
            {
                if (effect.type != "Attack") continue;
                SoundManager.Instance?.Play(EffectSound.UpcomingAttack);
                return;
            }
        }
    }

    private void EnterPicking()
    {
        countdownShown = 0;
        warnedTurn = -1;
        nextTurnEndIsFirst = true;
        StopResultRoutine();
        gameEndView?.Hide();
        UnloadBattleInput();
        CloseDeckView();
        RestoreTurnTextFont();
        SetTurnText("");
        turnTimer?.SetFill(0f);
        if (battleRoot != null) battleRoot.SetActive(true);
        SetBattleHudVisible(false);
        me?.ClearCards();
        opponent?.ClearCards();
    }

    private void EnterBattle()
    {
        pickView?.Hide();
        SetBattleHudVisible(true);
        SetTurnText("");
        SoundManager.Instance?.Play(EffectSound.BattleStart);
        SoundManager.Instance?.Play(BgmName.BattleBGM);
        LoadBattleInput();
    }

    private void EnterFinished()
    {
        UnloadBattleInput();
        CloseDeckView();
        pickView?.Hide();
        SetBattleHudVisible(true);
        if (turnText != null && resultFont != null) turnText.font = resultFont;
        if (state.me != null) RenderCombatants();
        turnTimer?.SetFill(0f);

        switch (state.result)
        {
            case "win":
                SetTurnText("승리");
                SoundManager.Instance?.StopBgm();
                SoundManager.Instance?.Play(EffectSound.PlayerWin);
                break;
            case "lose":
                SetTurnText("패배");
                SoundManager.Instance?.Play(BgmName.GameLoseBGM);
                break;
            default:
                SetTurnText("무승부");
                SoundManager.Instance?.StopBgm();
                break;
        }

        StopResultRoutine();
        resultRoutine = StartCoroutine(ShowResultScreen());
    }

    private IEnumerator ShowResultScreen()
    {
        yield return new WaitForSeconds(resultDelay);
        resultRoutine = null;

        // 싱글 GameOver처럼 전투 화면을 내리고 결과 화면을 띄운다
        me?.ClearCards();
        opponent?.ClearCards();
        if (battleRoot != null) battleRoot.SetActive(false);
        gameEndView?.Show();
    }

    private void StopResultRoutine()
    {
        if (resultRoutine == null) return;
        StopCoroutine(resultRoutine);
        resultRoutine = null;
    }

    private void RenderCombatants()
    {
        if (state.me != null) me?.Render(state.me, library, isMe: true);
        if (state.opponent != null) opponent?.Render(state.opponent, library, isMe: false);
    }

    private void RestoreTurnTextFont()
    {
        if (turnText != null && turnTextFont != null && turnText.font != turnTextFont)
            turnText.font = turnTextFont;
    }

    private void SetTurnText(string text)
    {
        if (turnText != null && turnText.text != text) turnText.text = text;
    }

    // =========================================================
    // Input (CharacterManager.CharacterInit 과 같은 "Battle" 맵)
    // =========================================================

    private void Play(int index)
    {
        if (state == null || state.phase != "battle") return;
        client.Send(ClientMessage.Play(index));
        // 성공/실패 소리는 서버의 use 이벤트를 받고 낸다
    }

    private void LoadBattleInput()
    {
        if (battleInputLoaded || PlayerInputManager.Instance == null) return;
        battleInputLoaded = true;
        PlayerInputManager.Instance.Load("Battle", new Dictionary<string, Action>
        {
            ["PlayCard1"] = () => Play(0),
            ["PlayCard2"] = () => Play(1),
            ["PlayCard3"] = () => Play(2),
            ["PlayCard4"] = () => Play(3),
            ["ReDraw"] = () => Play(-2),
            ["Defense"] = () => Play(-1),
        });
    }

    private void UnloadBattleInput()
    {
        if (!battleInputLoaded) return;
        battleInputLoaded = false;
        PlayerInputManager.Instance?.Unload();
    }
}
