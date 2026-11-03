using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MultiPlayMode_Oracle 씬 전체.
/// 로비(방 만들기/참가) → 카드 선택 → 카운트다운 → 전투 → 결과를 오라클 서버가 보내는 state대로 보여준다.
/// MultiplayLobby_Oracle 씬에서 방을 잡고 넘어오면 자체 로비는 건너뛰고, 나가면 그 로비 씬으로 돌아간다.
/// 이 씬을 바로 Play하면 아래의 간단한 자체 로비로 테스트할 수 있다.
/// 게임 판정은 전부 서버가 하고, 여기서는 입력(카드 선택, 카드 사용)만 서버로 보낸다.
///
/// 조작: 카드 클릭 또는 Q W E R (손패 1~4), D 리드로우, F 가드 (싱글 PlayerAction과 같은 키)
/// </summary>
public sealed class OracleMatchUI : MonoBehaviour
{
    [Header("Resources")]
    [Tooltip("한글이 들어있는 TMP 폰트 (NanumGothic SDF)")]
    [SerializeField]
    private TMP_FontAsset font;

    [Tooltip("카드 이름 → 그림을 찾을 CardDefinition 목록. 서버 카드 이름 = 에셋 이름")]
    [SerializeField]
    private CardDefinition[] cardLibrary;

    private OracleServerClient client;
    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    private MatchStateView state;
    private float stateReceivedAt;
    private bool inRoom;
    private bool requestInFlight;
    private bool cameFromLobby;

    // ---------------------------------------------------------
    // Lobby
    // ---------------------------------------------------------
    private GameObject lobbyPanel;
    private TMP_InputField nicknameInput;
    private TMP_InputField codeInput;
    private Button createButton;
    private Button joinButton;
    private Button leaveLobbyButton;
    private TextMeshProUGUI lobbyStatus;
    private TextMeshProUGUI roomText;

    // ---------------------------------------------------------
    // Picking / Countdown
    // ---------------------------------------------------------
    private GameObject pickPanel;
    private TextMeshProUGUI pickTitle;
    private TextMeshProUGUI pickTimer;
    private TextMeshProUGUI pickStatus;
    private TextMeshProUGUI deckText;
    private readonly OracleCardWidget[] offerCards = new OracleCardWidget[3];

    private GameObject countdownPanel;
    private TextMeshProUGUI countdownText;

    // ---------------------------------------------------------
    // Battle
    // ---------------------------------------------------------
    private GameObject battlePanel;
    private SideWidgets mySide;
    private SideWidgets opponentSide;
    private TextMeshProUGUI turnText;
    private Image turnBar;
    private readonly OracleCardWidget[] handCards = new OracleCardWidget[4];
    private Button redrawButton;
    private Button guardButton;

    private sealed class SideWidgets
    {
        public TextMeshProUGUI Name;
        public Image HpBar;
        public TextMeshProUGUI HpText;
        public TextMeshProUGUI Stats;
        public TextMeshProUGUI Effects;
        public readonly OracleCardWidget[] Queue = new OracleCardWidget[3];
    }

    // ---------------------------------------------------------
    // Result
    // ---------------------------------------------------------
    private GameObject resultPanel;
    private TextMeshProUGUI resultTitle;
    private TextMeshProUGUI resultReason;
    private Button rematchButton;

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        OracleUi.Font = font;

        if (cardLibrary != null)
        {
            foreach (CardDefinition card in cardLibrary)
            {
                if (card != null && card.GetSprite() != null)
                    sprites[card.name] = card.GetSprite();
            }
        }

        EnsureEventSystem();
        BuildUi();
        ShowLobby("닉네임을 입력하고 방을 만들거나 코드로 참가하세요.");
    }

    private void Start()
    {
        client = OracleServerClient.GetOrCreate();
        client.MessageReceived += HandleMessage;
        client.Disconnected += HandleDisconnected;

        // 로비 씬에서 방을 잡고 넘어온 경우: 씬 로드 중에 온 첫 state를 이어받는다
        if (client.InRoom)
        {
            cameFromLobby = true;
            inRoom = true;
            roomText.text = $"방 코드  <b>{client.RoomCode}</b>";

            if (client.LastState != null)
                HandleMessage(client.LastState);
            else
                ShowLobby("게임 준비 중...");
        }
    }

    private void ReturnToLobbyScene()
    {
        SceneManager.LoadScene(OracleScenes.Lobby);
    }

    private void OnDestroy()
    {
        if (client != null)
        {
            client.MessageReceived -= HandleMessage;
            client.Disconnected -= HandleDisconnected;
        }
    }

    private void Update()
    {
        if (state == null) return;

        float timeLeft = Mathf.Max(0f, state.timeLeft - (Time.unscaledTime - stateReceivedAt));

        switch (state.phase)
        {
            case "picking":
                pickTimer.text = Mathf.CeilToInt(timeLeft).ToString();
                break;
            case "countdown":
                countdownText.text = Mathf.Max(1, Mathf.CeilToInt(timeLeft)).ToString();
                break;
            case "battle":
                OracleUi.SetBar(turnBar, state.turnDuration > 0 ? 1f - timeLeft / state.turnDuration : 0f);
                ReadBattleKeys();
                break;
        }
    }

    private void ReadBattleKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.qKey.wasPressedThisFrame) Play(0);
        if (keyboard.wKey.wasPressedThisFrame) Play(1);
        if (keyboard.eKey.wasPressedThisFrame) Play(2);
        if (keyboard.rKey.wasPressedThisFrame) Play(3);
        if (keyboard.dKey.wasPressedThisFrame) Play(-2);
        if (keyboard.fKey.wasPressedThisFrame) Play(-1);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
        go.AddComponent<InputSystemUIInputModule>();
    }

    // =========================================================
    // Actions
    // =========================================================

    private async void CreateOrJoin(bool create)
    {
        if (requestInFlight || inRoom) return;

        string nickname = nicknameInput.text.Trim();
        if (nickname.Length == 0)
        {
            lobbyStatus.text = "닉네임을 입력하세요.";
            return;
        }

        string code = codeInput.text.Trim().ToUpperInvariant();
        if (!create && code.Length == 0)
        {
            lobbyStatus.text = "참가 코드를 입력하세요.";
            return;
        }

        requestInFlight = true;
        RefreshLobbyButtons();
        lobbyStatus.text = "서버 연결 중...";

        if (!await client.ConnectAsync())
        {
            requestInFlight = false;
            RefreshLobbyButtons();
            lobbyStatus.text = "서버에 연결할 수 없습니다.\n잠시 후 다시 시도하세요.";
            return;
        }

        lobbyStatus.text = create ? "방 만드는 중..." : "방 참가 중...";
        client.Send(ClientMessage.Hello(nickname));
        client.Send(create ? ClientMessage.CreateRoom() : ClientMessage.JoinRoom(code));
        // requestInFlight는 room_joined / error에서 풀린다
    }

    private void Leave()
    {
        if (client != null && client.IsConnected && inRoom)
            client.Send(ClientMessage.LeaveRoom());
        else if (cameFromLobby)
            ReturnToLobbyScene();
        else
            ShowLobby("");
    }

    private void Pick(int index)
    {
        if (state == null || state.phase != "picking" || state.picked) return;
        client.Send(ClientMessage.Pick(index));
    }

    private void Play(int index)
    {
        if (state == null || state.phase != "battle") return;
        client.Send(ClientMessage.Play(index));
    }

    private void Rematch()
    {
        if (state == null || state.phase != "finished") return;
        client.Send(ClientMessage.Rematch());
    }

    // =========================================================
    // Server Messages
    // =========================================================

    private void HandleMessage(ServerMessage message)
    {
        switch (message.type)
        {
            case "room_joined":
                requestInFlight = false;
                inRoom = true;
                codeInput.text = message.code;
                roomText.text = $"방 코드  <b>{message.code}</b>";
                lobbyStatus.text = message.players != null && message.players.Length >= 2
                    ? "상대와 연결되었습니다."
                    : "상대를 기다리는 중... 코드를 상대에게 알려주세요.";
                RefreshLobbyButtons();
                break;

            case "player_left":
                if (state != null && state.phase == "finished")
                {
                    // 결과 화면은 그대로 두고 다시 하기만 막는다
                    rematchButton.interactable = false;
                    OracleUi.SetLabel(rematchButton, "상대가 나갔습니다");
                }
                else if (cameFromLobby)
                {
                    // 게임 도중 상대가 나가면 방을 정리하고 로비 씬으로 (room_left에서 이동)
                    client.Send(ClientMessage.LeaveRoom());
                }
                else
                {
                    state = null;
                    ShowLobby("상대가 나갔습니다. 새 상대를 기다리는 중...");
                }
                break;

            case "room_left":
                if (cameFromLobby)
                {
                    ReturnToLobbyScene();
                    return;
                }

                inRoom = false;
                state = null;
                roomText.text = "";
                ShowLobby("방에서 나왔습니다.");
                break;

            case "error":
                requestInFlight = false;
                RefreshLobbyButtons();
                lobbyStatus.text = message.message;
                break;

            case "state":
                state = message.state;
                stateReceivedAt = Time.unscaledTime;
                RenderState();
                break;
        }
    }

    private void HandleDisconnected()
    {
        if (cameFromLobby)
        {
            ReturnToLobbyScene();
            return;
        }

        inRoom = false;
        requestInFlight = false;
        state = null;
        roomText.text = "";
        ShowLobby("서버와 연결이 끊어졌습니다.");
    }

    // =========================================================
    // Render
    // =========================================================

    private void ShowOnly(GameObject panel)
    {
        lobbyPanel.SetActive(panel == lobbyPanel);
        pickPanel.SetActive(panel == pickPanel);
        countdownPanel.SetActive(panel == countdownPanel);
        battlePanel.SetActive(panel == battlePanel);
        resultPanel.SetActive(false);
    }

    private void ShowLobby(string status)
    {
        ShowOnly(lobbyPanel);
        if (!string.IsNullOrEmpty(status)) lobbyStatus.text = status;
        RefreshLobbyButtons();
    }

    private void RefreshLobbyButtons()
    {
        bool idle = !requestInFlight && !inRoom;
        createButton.interactable = idle;
        joinButton.interactable = idle;
        nicknameInput.interactable = idle;
        codeInput.interactable = idle;
        leaveLobbyButton.gameObject.SetActive(inRoom);
    }

    private void RenderState()
    {
        switch (state.phase)
        {
            case "picking":
                ShowOnly(pickPanel);
                RenderPicking();
                break;

            case "countdown":
                ShowOnly(countdownPanel);
                break;

            case "battle":
                ShowOnly(battlePanel);
                RenderBattle();
                break;

            case "finished":
                // 전투가 시작된 뒤 끝났으면 마지막 전투 화면 위에 결과를 띄운다
                ShowOnly(state.me != null ? battlePanel : lobbyPanel);
                if (state.me != null) RenderBattle();
                RenderResult();
                break;
        }
    }

    private void RenderPicking()
    {
        pickTitle.text = $"카드 선택  {state.round} / {state.totalRounds}";
        pickStatus.text = state.picked
            ? (state.opponentPicked ? "" : $"{state.opponentName} 님이 고르는 중...")
            : "덱에 넣을 카드를 한 장 고르세요." + (state.opponentPicked ? $"  ({state.opponentName} 님은 골랐습니다)" : "");

        for (int i = 0; i < offerCards.Length; i++)
        {
            CardView card = state.offers != null && i < state.offers.Length ? state.offers[i] : null;
            offerCards[i].Set(card, SpriteFor(card), dimmed: state.picked);
        }

        deckText.text = $"내 덱 ({state.myDeck?.Length ?? 0}장)\n" +
            (state.myDeck != null ? string.Join("\n", state.myDeck) : "");
    }

    private void RenderBattle()
    {
        turnText.text = $"턴 {state.turn + 1}";

        RenderSide(mySide, state.me, isMe: true);
        RenderSide(opponentSide, state.opponent, isMe: false);

        bool battle = state.phase == "battle";
        for (int i = 0; i < handCards.Length; i++)
        {
            CardView card = state.me.hand != null && i < state.me.hand.Length ? state.me.hand[i] : null;
            handCards[i].Set(card, SpriteFor(card), selected: state.me.selectedIndex == i, dimmed: card != null && !card.playable);
        }

        redrawButton.interactable = battle && state.me.cost > 0;
        guardButton.interactable = battle;
        guardButton.targetGraphic.color = state.me.guard ? new Color(0.95f, 0.65f, 0.15f) : OracleUi.ButtonColor;
        OracleUi.SetLabel(guardButton, state.me.guard ? "가드 중 (F)" : "가드 (F)");
    }

    private void RenderSide(SideWidgets side, CombatantView view, bool isMe)
    {
        side.Name.text = isMe ? $"{view.name} (나)" : view.name;
        OracleUi.SetBar(side.HpBar, view.maxHp > 0 ? (float)view.hp / view.maxHp : 0f);
        side.HpText.text = $"{view.hp} / {view.maxHp}";

        string stats = $"코스트 {view.cost}/{view.maxCost}    방어 {view.defense}    덱 {view.deckCount}";
        if (!isMe) stats += $"    손패 {view.handCount}";
        if (view.guard) stats += "    <color=#fb4>가드</color>";
        side.Stats.text = stats;
        side.Effects.text = OracleUi.StatusEffects(view.effects);

        for (int i = 0; i < side.Queue.Length; i++)
        {
            CardView card = view.queue != null && i < view.queue.Length ? view.queue[i] : null;
            side.Queue[i].Set(card, SpriteFor(card));
        }
    }

    private void RenderResult()
    {
        resultPanel.SetActive(true);

        switch (state.result)
        {
            case "win":
                resultTitle.text = "승리!";
                resultTitle.color = new Color(1f, 0.85f, 0.3f);
                break;
            case "lose":
                resultTitle.text = "패배";
                resultTitle.color = new Color(0.9f, 0.4f, 0.4f);
                break;
            default:
                resultTitle.text = "무승부";
                resultTitle.color = Color.white;
                break;
        }

        resultReason.text = state.reason;

        if (state.rematchRequested)
        {
            rematchButton.interactable = false;
            OracleUi.SetLabel(rematchButton, "상대를 기다리는 중...");
        }
        else
        {
            rematchButton.interactable = true;
            OracleUi.SetLabel(rematchButton, state.opponentRematchRequested ? "다시 하기 (상대 대기 중)" : "다시 하기");
        }
    }

    private Sprite SpriteFor(CardView card) =>
        card != null && !card.IsEmpty && sprites.TryGetValue(card.name, out Sprite sprite) ? sprite : null;

    // =========================================================
    // Build
    // =========================================================

    private void BuildUi()
    {
        var canvasGo = new GameObject("OracleCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        Transform root = canvasGo.transform;
        Image background = OracleUi.Panel("Background", root, Vector2.zero, Vector2.zero, new Color(0.06f, 0.07f, 0.09f));
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;

        BuildLobby(root);
        BuildPicking(root);
        BuildCountdown(root);
        BuildBattle(root);
        BuildResult(root);
    }

    private void BuildLobby(Transform root)
    {
        lobbyPanel = OracleUi.Stretch("Lobby", root).gameObject;
        Transform p = OracleUi.Panel("Box", lobbyPanel.transform, Vector2.zero, new Vector2(820, 720), OracleUi.PanelColor).transform;

        OracleUi.Text("Title", p, new Vector2(0, 290), new Vector2(760, 80), "실시간 카드게임 · 온라인 대전", 48);

        OracleUi.Text("NickLabel", p, new Vector2(-250, 180), new Vector2(200, 60), "닉네임", 32, TextAlignmentOptions.Left);
        nicknameInput = OracleUi.Input("Nickname", p, new Vector2(90, 180), new Vector2(480, 70), "닉네임 (최대 12자)", 12);
        nicknameInput.text = "Player" + Random.Range(100, 1000);

        createButton = OracleUi.Button("Create", p, new Vector2(0, 80), new Vector2(680, 80), "방 만들기", () => CreateOrJoin(true));

        OracleUi.Text("CodeLabel", p, new Vector2(-250, -30), new Vector2(200, 60), "참가 코드", 32, TextAlignmentOptions.Left);
        codeInput = OracleUi.Input("Code", p, new Vector2(30, -30), new Vector2(360, 70), "ABC123", 6);
        joinButton = OracleUi.Button("Join", p, new Vector2(275, -30), new Vector2(130, 70), "참가", () => CreateOrJoin(false));

        roomText = OracleUi.Text("Room", p, new Vector2(0, -130), new Vector2(760, 60), "", 40);
        lobbyStatus = OracleUi.Text("Status", p, new Vector2(0, -210), new Vector2(760, 100), "", 28);
        lobbyStatus.color = new Color(0.8f, 0.85f, 0.95f);

        leaveLobbyButton = OracleUi.Button("Leave", p, new Vector2(0, -305), new Vector2(240, 60), "방 나가기", Leave, 28);
    }

    private void BuildPicking(Transform root)
    {
        pickPanel = OracleUi.Stretch("Picking", root).gameObject;
        Transform p = pickPanel.transform;

        pickTitle = OracleUi.Text("Title", p, new Vector2(-120, 440), new Vector2(900, 80), "", 52);
        pickTimer = OracleUi.Text("Timer", p, new Vector2(420, 440), new Vector2(160, 80), "", 52);
        pickTimer.color = new Color(1f, 0.85f, 0.4f);
        pickStatus = OracleUi.Text("Status", p, new Vector2(-120, 370), new Vector2(1100, 50), "", 30);

        for (int i = 0; i < offerCards.Length; i++)
        {
            int index = i;
            offerCards[i] = new OracleCardWidget(p, new Vector2(-480 + i * 360, -20), new Vector2(320, 460), () => Pick(index));
        }

        Image deckBox = OracleUi.Panel("DeckBox", p, new Vector2(770, -20), new Vector2(320, 760), OracleUi.PanelColor);
        deckText = OracleUi.Text("Deck", deckBox.transform, Vector2.zero, new Vector2(290, 730), "", 26, TextAlignmentOptions.Top);
        deckText.overflowMode = TextOverflowModes.Truncate;
    }

    private void BuildCountdown(Transform root)
    {
        countdownPanel = OracleUi.Stretch("Countdown", root).gameObject;
        OracleUi.Text("Label", countdownPanel.transform, new Vector2(0, 160), new Vector2(1000, 80), "전투 시작", 56);
        countdownText = OracleUi.Text("Number", countdownPanel.transform, new Vector2(0, -20), new Vector2(400, 260), "3", 220);
    }

    private void BuildBattle(Transform root)
    {
        battlePanel = OracleUi.Stretch("Battle", root).gameObject;
        Transform p = battlePanel.transform;

        opponentSide = BuildSide(p, infoY: 420, queueY: 250);
        mySide = BuildSide(p, infoY: -60, queueY: 90);

        turnText = OracleUi.Text("Turn", p, new Vector2(-760, 140), new Vector2(300, 60), "", 40);
        turnBar = OracleUi.Bar("TurnBar", p, new Vector2(-760, 95), new Vector2(300, 18), new Color(1f, 0.85f, 0.4f));

        string[] keys = { "Q", "W", "E", "R" };
        for (int i = 0; i < handCards.Length; i++)
        {
            int index = i;
            handCards[i] = new OracleCardWidget(p, new Vector2(-390 + i * 260, -330), new Vector2(240, 330), () => Play(index), keys[i]);
        }

        redrawButton = OracleUi.Button("Redraw", p, new Vector2(760, -270), new Vector2(260, 80), "리드로우 (D)", () => Play(-2), 30);
        guardButton = OracleUi.Button("Guard", p, new Vector2(760, -380), new Vector2(260, 80), "가드 (F)", () => Play(-1), 30);
        OracleUi.Text("Help", p, new Vector2(760, -470), new Vector2(320, 80),
            "리드로우: 코스트 1\n가드: 받는 피해 절반, 코스트 회복 -1", 20).color = new Color(1f, 1f, 1f, 0.55f);
    }

    private SideWidgets BuildSide(Transform p, float infoY, float queueY)
    {
        var side = new SideWidgets();
        Image box = OracleUi.Panel("Info", p, new Vector2(-560, infoY), new Vector2(700, 150), OracleUi.PanelColor);

        side.Name = OracleUi.Text("Name", box.transform, new Vector2(-150, 48), new Vector2(380, 44), "", 32, TextAlignmentOptions.Left);
        side.HpBar = OracleUi.Bar("Hp", box.transform, new Vector2(120, 48), new Vector2(380, 34), OracleUi.HpColor);
        side.HpText = OracleUi.Text("HpText", box.transform, new Vector2(120, 48), new Vector2(380, 34), "", 26);
        side.Stats = OracleUi.Text("Stats", box.transform, new Vector2(0, 2), new Vector2(660, 40), "", 26, TextAlignmentOptions.Left);
        side.Effects = OracleUi.Text("Effects", box.transform, new Vector2(0, -44), new Vector2(660, 40), "", 24, TextAlignmentOptions.Left);
        side.Effects.color = new Color(0.85f, 0.9f, 1f);

        OracleUi.Text("QueueLabel", p, new Vector2(100, queueY + 130), new Vector2(400, 30), "큐 (숫자 = 남은 쿨다운)", 20)
            .color = new Color(1f, 1f, 1f, 0.5f);
        for (int i = 0; i < side.Queue.Length; i++)
            side.Queue[i] = new OracleCardWidget(p, new Vector2(-70 + i * 180, queueY), new Vector2(160, 230), null);

        return side;
    }

    private void BuildResult(Transform root)
    {
        resultPanel = OracleUi.Stretch("Result", root).gameObject;
        Image dim = resultPanel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.65f);

        Transform p = OracleUi.Panel("Box", resultPanel.transform, Vector2.zero, new Vector2(760, 480), OracleUi.PanelColor).transform;
        resultTitle = OracleUi.Text("Title", p, new Vector2(0, 130), new Vector2(700, 140), "", 110);
        resultReason = OracleUi.Text("Reason", p, new Vector2(0, 10), new Vector2(700, 80), "", 30);
        rematchButton = OracleUi.Button("Rematch", p, new Vector2(-170, -150), new Vector2(320, 80), "다시 하기", Rematch, 30);
        OracleUi.Button("Exit", p, new Vector2(170, -150), new Vector2(320, 80), "나가기", Leave, 30);
    }
}
