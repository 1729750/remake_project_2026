using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MultiPlayMode_Oracle 씬의 진행 담당.
/// MultiplayLobby_Oracle 에서 방을 잡고 넘어온 뒤, 오라클 서버가 보내는 state에 따라
/// SinglePlayMode와 같은 화면을 켜고 끈다.
///   picking   → RewardPanel(OracleCardPickView)에서 카드 3장 중 1장 선택, TurnText에 남은 초
///   countdown → TurnText 카운트다운 + 턴 타이머 (싱글 전투 시작과 같음)
///   battle    → Player/Enemy(OracleCharacterView)를 서버 값으로 그림, 싱글과 같은 키(Q W E R / D / F)
///   finished  → TurnText에 승리/패배/무승부 → GameEndManager(OracleGameEndView) 다시하기/나가기
/// 게임 판정은 전부 서버가 하고, 여기서는 입력만 보낸다.
/// </summary>
public sealed class OracleMatchView : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private GameObject battleRoot;
    [SerializeField] private TextMeshPro turnText;
    [SerializeField] private OracleCharacterView me;
    [SerializeField] private OracleCharacterView opponent;
    [SerializeField] private OracleCardPickView pickView;
    [SerializeField] private OracleGameEndView gameEndView;

    [Header("Cards")]
    [Tooltip("서버 카드 이름 → CardDefinition 에셋 (그림/카드 종류 배경)")]
    [SerializeField] private CardDefinition[] cardLibrary;

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

    private void Awake()
    {
        library = new OracleCardLibrary(cardLibrary);
        CreateTurnTimer();

        if (pickView != null) pickView.Picked += index => client?.Send(ClientMessage.Pick(index));
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
                SetTurnText(Mathf.CeilToInt(timeLeft).ToString());
                turnTimer?.SetFill(0f);
                break;

            case "countdown":
                SetTurnText(Mathf.Max(1, Mathf.CeilToInt(timeLeft)).ToString());
                turnTimer?.SetFill(1f - timeLeft / 3f);
                break;

            case "battle":
                turnTimer?.SetFill(state.turnDuration > 0 ? 1f - timeLeft / state.turnDuration : 0f);
                break;
        }
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
                stateReceivedAt = Time.unscaledTime;
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
                    SetTurnText("");
                }
                break;

            case "battle":
                if (phaseChanged) EnterBattle();
                RenderCombatants();
                break;

            case "finished":
                if (phaseChanged) EnterFinished();
                break;
        }
    }

    private void EnterPicking()
    {
        StopResultRoutine();
        gameEndView?.Hide();
        UnloadBattleInput();
        if (battleRoot != null) battleRoot.SetActive(true);
        me?.ClearCards();
        opponent?.ClearCards();
    }

    private void EnterBattle()
    {
        pickView?.Hide();
        SetTurnText("");
        SoundManager.Instance?.Play(EffectSound.BattleStart);
        SoundManager.Instance?.Play(BgmName.BattleBGM);
        LoadBattleInput();
    }

    private void EnterFinished()
    {
        UnloadBattleInput();
        pickView?.Hide();
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
        SoundManager.Instance?.Play(EffectSound.UseCard);
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
