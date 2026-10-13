# MultiPlayMode 카드 생성 및 상단 표시 구현 계획

## 조사 범위와 결론

이 계획은 `.agents/tasks/multiplaymode-scene-investigation.md`를 기준으로 작성했다. 지정된 스크립트와 씬 YAML을 다시 확인한 결과, SinglePlayMode의 `MapManager`는 카드 후보를 랜덤 생성하지 않는다. Single의 `MapManager`는 이미 전달된 `CharacterData[]`를 표시하고, 선택한 후보의 덱을 `DeckDisplay`에 채운다. Multi에서 실제 카드 후보를 랜덤 생성하는 현재 경로는 `CardOptionGenerator.cardPool`과 `GenerateOptions`이다. 따라서 구현은 이 서버 경로를 재사용하고, 결과를 Single의 `CardDefinition -> CardInstance -> CardVisual` 표시 경로로 넘기는 방식이어야 한다.

## (A) 재사용할 Single/Multi 카드 생성 및 표시 semantics

- 카드 원본 pool은 `Assets/scripts/HyeGyoServer/MultipLayMode/CardOptionGenerator.cs:6-9`의 `[SerializeField] private List<CardDefinition> cardPool`이다. 조사한 `Assets/Scenes/MultiPlayMode.unity`에는 `CardOptionGenerator` 컴포넌트의 pool 항목이 직렬화되어 보이지 않으므로 구현 전에 해당 컴포넌트의 실제 YAML 위치와 Inspector 값/프리팹 연결을 확인해야 한다. pool이 비어 있으면 현재 `CreateAddCardOption`이 Skip을 반환한다(`CardOptionGenerator.cs:50-83`).
- 후보 생성은 `CardOptionGenerator.GenerateOptions(PlayerPreparationData player, int round)`(`CardOptionGenerator.cs:11-48`)이다. 현재 3개 후보를 만들며, 0번은 강화 가능 카드가 있으면 `CreateEnhanceOption`, 없으면 AddCard, 1번은 AddCard, 2번은 50% 강화/AddCard다. `round`는 현재 전달만 되고 정책에는 사용되지 않는다. 구현은 이 메서드를 서버에서만 호출하고, 후보별 AddCard 원본은 전송 전에 pool index로 바꾼다(`ToNetData`, `CardOptionGenerator.cs:203-231` 부근).
- 후보가 선택되어 덱에 저장될 때 이미 `MultiPreparationManager.ApplyAddCard`(`MultiPreparationManager.cs:239-287`)가 `card.Clone()`을 호출한다. `CardDefinition.Clone()`(`Assets/scripts/Cards/CardDefinition.cs:118-145`)은 `CreateInstance<CardDefinition>`과 새 `CardEffect`/`Effect`를 사용해 cost, cooldown, sprite, cardType, effects를 깊게 복제한다. 후보 UI에도 원본 ScriptableObject를 직접 공유하지 않도록 `CardOptionGenerator`가 표시용 `CardDefinition` runtime clone을 만들거나, client에서 pool index를 복원한 직후 `Clone()`한 표시용 정의를 `PreparationOption.Card`에 넣는다. 서버의 `FinalDeck`에는 기존 `ApplyAddCard` clone을 계속 사용한다.
- Single의 카드 상세 semantics는 `MapManager.RefreshDeckSelectionPopup()`(`Assets/scripts/MapManager.cs:101-110`)가 선택된 `CardDefinition`의 `GetEffects()`를 순서와 중복 그대로 `EffectType` 목록으로 변환하는 `GetCardEffectTypes`(`MapManager.cs:113-125`)를 `PopupManager.Show`에 전달하는 것이다. `PopupManager.Show`(`Assets/scripts/Popup/PopupManager.cs:93-132`)가 각 EffectType을 emoji/summary로 바꾼다.
- 실제 카드 표면은 `DeckDisplay.SetDeck`(`Assets/scripts/Cards/DeckDisplay.cs:75-173`) -> `SpawnCard`(`DeckDisplay.cs:244-257`) 순서다. `SpawnCard`는 `Resources/Prefabs/Card`를 instantiate하고, `CardVisual.SetPopupManager`, `new CardInstance(def, null)`, `SetVisual`, `SetFace(true)`, `SetLayer("UI")`를 순서대로 호출한다. `CardInstance.SetVisual`(`Assets/scripts/Cards/CardInstance.cs:43-67`)은 `CardVisual.SetCardDefinition`, cost/cooldown/effects 갱신을 수행한다. `CardVisual.SetCardDefinition`(`Assets/scripts/Cards/CardVisual.cs:89-105`)은 cardType 배경과 sprite를 설정하고, `RefreshEffectDisplays`(`CardVisual.cs:128-145`)는 effect 아이콘/수치를 설정한다.
- Single deck view의 정확한 입력 순서는 `MapManager.LoadDeckDisplayInput`(`MapManager.cs:138-166`)에서 `SetDeckViewActive(true)`로 먼저 활성화, `RefreshOpponentDeckDisplay`로 `SetDeck`, `PlayerInputManager.Load("Select", ...)`, `SelectFirst`, `RefreshDeckSelectionPopup`이다. 닫을 때 `Unload`, `Deselect`, `SetDeckViewActive(false)`, 후보 popup 복구 순서를 따른다. Multi 상단 view도 이 순서를 유지한다.

## (B) MultiPlayMode 상단 영역 scene binding

현재 `MultiPlayMode.unity`의 `MapManager_Multi` 컴포넌트는 YAML의 `MapManager_Multi` 레코드(조사 보고서 line 6319 부근)에서 다음만 가진다: `preparationViewRoot`, `preparationManager`, `networkMatchBridge`, `myDeckDisplay: {fileID: 0}`, `remainingText: {fileID: 0}`, `deckCardSize`, `deckColumns`. `MapSelectPanel`은 `MapManager_Multi`의 자식이며 `MapSelectPanel/Panel1/EnemyDisplay`, `Panel2/EnemyDisplay`, `Panel3/EnemyDisplay`는 기존 준비 선택 visual 경로(`MapManager_Multi.FindOptionVisuals`, `MapManager_Multi.cs:78-117`)다. 상단 owner 카드 정보용 `DeckDisplay`/`PopupManager`/전용 container 이름은 기존 YAML에서 확인되지 않았다.

따라서 구현 시 최소 신규 계층은 `MapManager_Multi` 자식 `UpperCardInfoPanel`이다. 이 object는 상단 위치의 기존 UI 레이아웃 안에 배치하고, 그 자식으로 `UpperDeckDisplay`(`DeckDisplay`)와 `UpperPopupManager`(`PopupManager`)를 둔다. 이미 존재하는 적합한 상단 UI container가 Unity Editor YAML 추가 조사에서 확인되면 그 object를 우선 사용하고 신규 계층을 만들지 않는다. 확인되지 않은 object 이름을 Inspector reference로 가정하지 않는다.

`MapManager_Multi`에 다음 serialized fields를 추가하고 `MultiPlayMode.unity`의 해당 component YAML에 fileID를 바인딩한다.

- `upperCardInfoPanel: GameObject` -> `UpperCardInfoPanel` GameObject.
- `upperDeckDisplay: DeckDisplay` -> `UpperDeckDisplay` component.
- `upperPopupManager: PopupManager` -> `UpperPopupManager` component.
- `upperRemainingText`가 필요하면 기존 `remainingText`를 상단의 실제 TMP_Text에 연결한다. 기존 준비 UI 수치와 분리해야 할 때만 새 field를 추가한다.

새 GameObject의 Transform/GameObject/MonoBehaviour YAML records는 사용하지 않은 fileID를 사용하고, 기존 `MapManager_Multi` Transform의 `m_Children`에 `UpperCardInfoPanel` Transform을 한 번만 추가한다. `UpperCardInfoPanel`은 준비 후보 패널의 child가 아니어야 한다. `upperDeckDisplay`가 null인 현재 상태는 구현 시 실제 binding으로 고친다. scene 변경 후 YAML에서 `m_Name`, parent fileID, `DeckDisplay`, `PopupManager`, `MapManager_Multi.upperDeckDisplay`의 fileID를 정적으로 확인한다.

## (C) 서버 권위 후보 contract

1. 랜덤 draw는 `CardOptionGenerator.GenerateOptions`에 남긴다. `MultiPreparationManager.TryBeginForPlayer(clientId, out options, ...)`가 server에서만 이를 호출하는 현재 구조를 유지하되, server gate 뒤 `NetworkManager.Singleton.ConnectedClients`에서 실제 연결 ClientId를 확인하고 `preparationRegistry`의 player와 일치할 때만 생성한다. `clientId`별 `currentRoundByClient`와 `currentOptionsByClient` dictionary가 독립성을 보장한다.
2. 첫 요청은 `NetworkMatchBridge.RequestBeginPreparationRpc`(`NetworkMatchBridge.cs:607-679`)가 `rpcParams.Receive.SenderClientId`를 사용해 manager를 호출한다. confirm/skip 이후도 같은 sender ID로만 manager를 호출한다. 다른 ClientId의 dictionary를 읽거나 변경하는 경로를 만들지 않는다.
3. 현재 `SendPreparationOptionsRpc`(`NetworkMatchBridge.cs:688-749`)는 `[Rpc(SendTo.ClientsAndHost)]` broadcast primitive이지만 `targetClientId`와 local ID 비교로 화면 처리를 필터링한다. 후보 payload 자체가 모든 peer에 전달되는 구조는 owner-only 요구를 만족하지 않으므로, 구현에서는 NGO 2.13의 targeted RPC primitive로 바꾼다. 계획상 signature는 `[Rpc(SendTo.SpecifiedInParams)] private void SendPreparationOptionsRpc(PreparationOptionNetData option0, PreparationOptionNetData option1, PreparationOptionNetData option2, RpcParams rpcParams = default)`이며 server call은 `new RpcParams { Send = new RpcSendParams { TargetClientIds = new[] { senderClientId } } }` 패턴을 프로젝트의 NGO API에 맞춰 사용한다. 프로젝트 API가 `SendTo.SpecifiedInParams`를 지원하지 않으면 `ClientRpcParams.Send.TargetClientIds = new[] { senderClientId }`를 받는 targeted ClientRpc로 동일한 owner-only contract를 구현한다. `targetClientId`를 payload에 넣고 client에서 버리는 방식은 사용하지 않는다.
4. confirm 결과도 동일하게 targeted 한다. exact payload는 `targetClientId`를 transport target으로만 지정하고, `selectedData`, `remaining`, `hasNextOptions`, `next0`, `next1`, `next2`를 보낸다. skip은 selected data를 Skip으로 채우고 동일한 result RPC를 사용하거나 별도 targeted `SendPreparationSkipResultRpc(target, remaining, hasNext, next0..2)`를 사용한다. 현재 confirm/skip server body가 주석으로 끝나므로 이 wiring은 구현 loop의 별도 필수 수정이다.
5. AddCard를 client로 복원할 때 `CardOptionGenerator.FromNetData`가 pool asset을 반환한 뒤 `Clone()`하여 표시용 CardDefinition을 만든다. server는 `PreparationOption.Card`를 runtime clone으로 저장하고, CardDefinition asset 자체나 다른 player의 mutable clone reference를 전송/공유하지 않는다. 후보 정보가 pool index로만 전송되는 동안 effect/cost/cooldown/icon/type은 server와 동일한 pool asset에서 복원되고, 표시 직전 clone으로 분리된다.
6. owner-only upper snapshot은 준비 후보/현재 FinalDeck 또는 상단에 표시할 카드 list를 server가 해당 ClientId에만 targeted RPC로 보낸다. 이 snapshot에는 pool index와 runtime cost/cooldown/effect/upgrade 상태를 포함하고, 상대 ClientId에는 전혀 전송하지 않는다. 공개 HP/방어 등 `GameNetworkState`의 Everyone 상태와 섞지 않는다. 현재 task에서 upper view를 준비 후보 카드 상세로 한정할 수 있으면 후보 result RPC가 minimal snapshot contract가 되고, FinalDeck 전체 상단 표시가 필요하면 별도의 `SendOwnerDeckSnapshotRpc(target, CardSnapshotNetData[] cards)`를 추가한다.

## (D) 상단 UI binding과 입력 상호배제

상단 view의 `OpenUpperCardInfo`는 다음 순서를 따른다.

1. 현재 preparation option UI 입력을 저장하고 unload한다. `PlayerInputManager`의 기존 Select context를 무조건 임의로 재구성하지 말고, Multi 준비 입력을 로드한 한 곳에서 `upperViewOpen` 상태를 검사하게 한다. 옵션 visual GameObject 또는 option input을 disable한다.
2. `upperCardInfoPanel.SetActive(true)`를 먼저 호출한다.
3. owner snapshot에서 clone한 `CardDefinition` 목록을 `upperDeckDisplay.SetDeck(cards, null, upperCardSize, upperColumns)`로 넣는다.
4. `upperDeckDisplay.SetPopupManager(upperPopupManager)`가 연결됐는지 보장한다.
5. `PlayerInputManager.Load("Select", ...)`로 상단 `MoveSelectionHorizontal/Vertical`과 Cancel을 바인딩한다.
6. `upperDeckDisplay.SelectFirst()`를 호출한다.
7. `RefreshUpperDeckSelectionPopup`가 Single의 `GetCardEffectTypes`와 같은 순서/중복 semantics로 `upperPopupManager.Show(effectTypes)`를 호출한다.

Cancel은 `PlayerInputManager.Unload()`, `upperDeckDisplay.Deselect()`, `upperCardInfoPanel.SetActive(false)`, option visual/input 복구, 현재 preparation options의 highlight와 Select context 복구 순서로 처리한다. 상단 view가 열린 동안 `ConfirmLocalPreparation`이 호출되면 안 되며, preparation visual을 단순히 화면에서만 끄고 입력을 남겨두지 않는다. 준비 panel input은 Cancel 시 열기 전 상태와 같은 `_options`, `_selectedIndex`로 복구한다. 후보 정보 popup과 상단 deck popup은 서로 다른 `PopupManager` instance를 사용한다.

## (E) 향후 10회 flow의 최소 hook

`MultiPreparationManager`에 `[SerializeField] private float transitionDelaySeconds = 1f;`를 추가한다. Inspector에서 0 이상으로 조절할 수 있고, default는 1초로 둔다. `CheckAllPlayersCompleted()`는 server에서만 한 번 통과하는 `transitionStarted` guard를 둔 뒤 `StartCoroutine(WaitForBothPlayersThenTransition())`를 호출한다. coroutine은 두 `PlayerPreparationData.ConditionCompleted`를 다시 확인하고, `yield return new WaitForSeconds(transitionDelaySeconds)` 후 debug log를 남기고 기존 transition entry point를 호출한다. 이 task에서는 full 10-round RPC/result loop, FinalDeck handoff, 실제 scene transition policy를 구현한 것으로 간주하지 않는다. 정확한 10회 loop는 현재 manager의 server counter에 이미 부분 구현되어 있으나 NetworkMatchBridge confirm/skip result delivery가 남은 gap으로 기록한다.

예정 stub의 동작 contract는 다음과 같다.

```csharp
private bool transitionStarted;

private IEnumerator WaitForBothPlayersThenTransition()
{
    while (!AreBothPlayersCompleted())
        yield return null;

    yield return new WaitForSeconds(Mathf.Max(0f, transitionDelaySeconds));
    Debug.Log($"[MultiPreparationManager] Both players completed; transition delay elapsed: {transitionDelaySeconds}s");
    MapManager_Multi.Instance?.NotifyBothPlayersConfirmed();
}
```

`AreBothPlayersCompleted`는 `preparationRegistry.Count == 2`, registry의 두 player가 모두 `ConditionCompleted`인지 server에서 검사한다. 구현 시 disconnect/중복 start는 기존 known blocker로 남기고, coroutine을 두 번 시작하지 않는 guard만 추가한다.

## (F) 구체적 파일 edits와 review stop contract

1. `Assets/scripts/HyeGyoServer/MultipLayMode/CardOptionGenerator.cs` — server pool draw를 유지하고, 표시/네트워크 경계에서 runtime clone을 만들 수 있는 helper를 추가한다. `round` 기반 정책을 새로 확대하거나 Single behavior를 변경하지 않는다.
2. `Assets/scripts/HyeGyoServer/MultipLayMode/MultiPreparationManager.cs` — ConnectedClients 검증, owner별 candidate state 유지, `transitionDelaySeconds`, one-shot coroutine hook을 추가한다. AddCard clone semantics는 유지한다.
3. `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/NetworkMatchBridge Separate/NetworkMatchBridge.cs` — begin/confirm/skip의 result serialization을 완성하고, options/result/snapshot을 실제 sender에게만 targeted RPC로 보낸다.
4. `Assets/scripts/HyeGyoServer/MultiServerCardSelection/MapManager_Multi.cs` — upper serialized bindings, clone snapshot 수신, Single 순서의 deck/popup flow, preparation input lock/restore를 추가한다.
5. `Assets/scripts/HyeGyoServer/MultiServerCardSelection/PreparationOptionVisual_Multi.cs` — 필요할 때 option visual 입력/활성 상태를 명시적으로 잠글 수 있는 최소 method만 추가한다. 카드 효과 계산 UI를 새 semantics로 재작성하지 않는다.
6. `Assets/Scenes/MultiPlayMode.unity` — 기존 상단 object를 재사용할 수 없다는 YAML 확인 결과에 따라 `UpperCardInfoPanel`, `UpperDeckDisplay`, `UpperPopupManager`를 최소 추가하고 `MapManager_Multi`의 upper fields와 `remainingText`를 바인딩한다. fileID uniqueness와 parent-child YAML을 검증한다. Single scene은 변경하지 않는다.
7. 계획 산출물인 `.agents/tasks/multiplayer-card-generation-display/plan.md` — 이 contract와 known blockers를 기록한다.

리뷰 gate의 stop contract는 `c:\Git-Directory\UpnL_FirstGame\remake_project_2026\.agents\tasks\multiplayer-card-generation-display/review.json`이다. 구현 loop는 review.json의 `verdict`가 정확히 `"APPROVED"`가 될 때만 종료한다. 승인 전에는 구현자가 위 파일 목록, targeted RPC, YAML bindings, static validation 결과를 모두 review artifact에 기록해야 한다.

## (G) Not in scope / known blockers

- `NetworkMatchBridge` confirm/skip result delivery는 현재 주석으로 끊겨 있다. 이 task는 해당 결과 delivery를 상단 후보 표시가 동작할 최소 범위까지 부분 개선한다. full 10-round UX와 모든 오류/재접속 정책은 남긴다.
- `PlayerPreparationData.FinalDeck`에서 `CharacterManager_Multi`로의 FinalDeck handoff는 현재 누락이다. 이 task는 upper owner snapshot 표시를 위해 서버 보관/target delivery만 다루며 전투 CharacterManager 연결은 untouched로 남긴다.
- `ApplyEnhance`는 index만 검증하고 실제 mutation을 하지 않는다. 상단 display는 서버가 확정한 현재 data를 표시할 뿐이며, 강화 효과 규칙을 발명하거나 이 gap을 악화시키지 않는다.
- `MapManager_Multi.NotifyBothPlayersConfirmed`가 즉시 `GameManager_Multi.StartBattle()`을 부르는 구조는 기존 entry point다. 이번 task는 configurable delay와 one-shot guard hook을 부분 개선하지만, 전체 scene transition/전투 handoff는 in scope가 아니다.
- owner-only top status snapshot은 기존에 없다. 이번 task에서 upper candidate/owner snapshot contract를 targeted RPC로 부분 추가한다. 상대 손패/덱을 broadcast하지 않는 것이 필수이며, public NetworkVariable 경계는 변경하지 않는다.
- `myDeckDisplay`와 `remainingText`가 현재 YAML에서 null이다. 상단 display binding으로 이 문제를 개선한다. Unity asset reference/prefab pool 값이 실제로 없는 경우 구현을 중단하고 blocker를 review에 기록한다.
- 기존 `handsRoot` 공유 및 `HandManager_Multi`의 owner/remote privacy는 이번 task에서 수정하지 않는다.
- `Assets/_Recovery`의 잔존 파일 및 branch conflict 표시는 이번 계획/구현 범위가 아니다. `_Recovery`를 삭제하거나 재생성하지 않는다.
- reload 비용/token, 입장 비용, 약한 기본 공격/방어 후보 보장, 실제 강화 데이터 선택은 이번 task에 포함하지 않는다.
- SinglePlayMode behavior는 변경하지 않는다. Single의 `MapManager`, `CardVisual`, `CardInstance`, `DeckDisplay`, `PopupManager` semantics만 재사용한다.
- Unity Editor와 실제 Host/Client 두 프로세스 실행은 이 환경에서 사용할 수 없다. 검증은 C# static/compile 가능한 범위, script reference inspection, scene YAML inspection으로 수행한다. 수동 검증은 Host/Client 각각 서로 다른 candidate set 수신, 상대 후보 미노출, upper open 중 Select 차단, Cancel 복구, 10회 완료 후 configurable delay 로그를 확인해야 한다.

## (H) workflow restructuring 판단

이 작업은 서버 candidate/RPC 경계, 상단 owner UI/YAML binding, transition hook이 서로 맞물려 있어 현재 implementation loop에서 하나의 feature로 진행하는 편이 안전하다. 별도 FEAT artifact나 workflow step 교체는 하지 않는다. 구현 loop는 review gate의 `review.json.verdict == "APPROVED"` stop contract를 유지한다.
