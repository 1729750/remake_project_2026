# Multiplayer preparation card display implementation

Implemented in the current workspace; no commit/push, import script, recovery change, or Single scene modification performed.

## Files changed

- `Assets/Scenes/MultiPlayMode.unity`: replaced CardOptionGenerator's three null pool entries with the canonical ordered 29 RewardManager.rewardCards references from SinglePlayMode; added and bound the owner DeckDisplay child.
- `Assets/scripts/HyeGyoServer/MultipLayMode/CardOptionGenerator.cs`: independent server draws, deep candidate clones, persistent pool source index, Single RewardManager.RollEnhanceOption/CanEnhance reuse, runtime card snapshot export/reconstruction using Effect.Create.
- `Assets/scripts/HyeGyoServer/MultipLayMode/PreparationOption.cs`: source index and server upgrade payload.
- `Assets/scripts/HyeGyoServer/MultipLayMode/MultiPreparationManager.cs`: sender registration/connection/phase/current choices/round/index/upgrade target checks; exactly one server round increment per accepted Add/Enhance/Skip; stable begin candidates; actual ApplyUpgrade; delayed one-shot existing battle entry hook.
- `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/whileChoosingCard/PlayerPreparationData.cs`: ordered source metadata for confirmed clones.
- `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/NetworkLauncher분리버전/PlayerPreparationRegistry.cs`: destroy confirmed runtime clones when an owner disconnects.
- `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/NetworkMatchBridge Separate/NetworkMatchBridge.cs`: round-bearing server requests; combined owner-only targeted ClientRpc containing confirmed cards, candidates and remaining; real target routing also for legacy enhance responses and rejection messages; resolve scene references after scene changes.
- `Assets/scripts/HyeGyoServer/MultiServerCardSelection/PreparationOptionNetData.cs`: serializable candidate metadata for the combined JSON payload.
- `Assets/scripts/HyeGyoServer/MultiServerCardSelection/MapManager_Multi.cs`: snapshot-only accumulated deck refresh, remaining label, candidate input, ViewDeck detail input on a separate stack context, candidate confirm disabled while viewing deck or awaiting response.
- `Assets/scripts/HyeGyoServer/MultiServerCardSelection/PreparationOptionVisual_Multi.cs`: actual Resources/Prefabs/Card instantiated inside existing candidate panels, CardInstance -> CardVisual for sprite/type/cost/cooldown/effect icons and values, popup assignment and selection.
- `Assets/scripts/Cards/CardDefinition.cs`: safe validated-effects accessor and runtime reconstruction factory; existing Single creation/clone behavior retained.
- `Assets/scripts/GameManager.cs`: existing resource-only price/summary loaders made static so Multi can reuse Single data without requiring a Single GameManager instance.
- `Assets/scripts/Battles/BattleManager.cs`: emoji lookup delegates to Multi battle emoji table when no Single manager exists.

## Behavior and privacy

Candidates are generated on the server per actual NGO sender ClientId; each owner retains their current three choices until a decision succeeds. Card source identity is stored independently from clone reference equality. First round contains card additions; subsequent slots use the existing enhancement/addition policy and Single upgrade eligibility. Enhancement mutates only that owner's runtime card. Runtime snapshots carry exact ordered effects, magnitudes, targets, applied category, cost, cooldown and type, and reconstruct the source sprite. Confirmed deck display is replaced from the full server snapshot, never optimistically accumulated from a local click.

Requests carry the displayed round. After validation succeeds the server applies the decision and advances once; stale, duplicate and invalid requests fail without advancing. Skip uses the same decision boundary. Next-generation failure after commit does not turn the successful decision into failure. Begin can retry generation without consuming another decision. Snapshot routing uses ClientRpcSendParams.TargetClientIds containing only the owner; a broadcast with a local filter is no longer used for preparation or legacy enhance candidate details. Public battle variables were not changed.

JSON receiver checks total payload length (256 Ki characters), deck count (10), candidate count (0/3), remaining (0..10), parallel effect-array lengths (up to 64), enum values, source indices, and supported effect category. Reconstruction completes before the UI deck swap. These checks are source-inspected, not runtime-tested.

## Scene objects and input

Existing actual scene root object is named `MapManager` and contains component `MapManager_Multi` (1474451739). Existing `PanelGameObject` GO 885946566 / Transform 885946567 and its position/scale/background are preserved. Added child `MySelectedCardsDisplay`: GO 885946569, RectTransform 885946570, DeckDisplay 885946571, script GUID f1f8d35b43c62fa4e9dd4c621d17c089. Child parent points to 885946567; parent children points to 885946570. MapManager fields point directly to panel 885946566 and DeckDisplay 885946571. Display uses panel bounds and inverse parent scale. `RemainingChoices` and `MySelectedCardsPopup` are created at runtime; popup uses the valid PopUpComponent resource rather than referencing the known conflicted PopupManager prefab meta.

Real input asset is `Assets/PlayerAction.inputactions`, GUID f5714a0d0d14cd941980715a3304ff60 (matching scene PlayerInputManager binding). `Select/Select` is Z; `Select/ViewDeck` is Tab; `Select/Cancel` is X; A/D/W/S navigate. The requested 'Single C deck input' is not present in this current asset. The C binding found in `Assets/InputSystem_Actions.inputactions` is Crouch, not deck view. Single GameManager opens PlayerDeckPanel through ViewDeck, and PlayerDeckPanel uses DeckDisplay -> CardInstance -> CardVisual; this same display path and push/pop pattern is reused in Multi. No input binding was invented or changed.

## Verification and limits

- Read actual source, investigation, plan, review artifacts, Single MapManager/PlayerDeckPanel and both scenes before implementation.
- Read back saved scene records and compared the 29 pool references/order against actual Single RewardManager.rewardCards, including its final seven entries.
- Read back panel child/component/parent references and script GUID against the DeckDisplay prefab. Added IDs are not substituted for existing objects.
- Source search confirmed removal of old SendPreparationOptionsRpc, SendPreparationConfirmResultRpc and RequestSkipPreparationRpc paths and checked remaining manager call sites against updated signatures.
- Inspected PopupManager Awake/child search, CardVisual initialization and EffectDisplay emoji lookup. Popup child is instantiated before the inactive manager root activates; all candidate surfaces use the existing active card prefab before SetVisual.
- Attempted `dotnet build Assembly-CSharp.csproj --no-restore --verbosity quiet`, then via `cmd.exe /d /c`. Both terminal tool calls returned exit -1 with only repeated command echo and no compiler output. This is a terminal execution blocker, **not a passing build and not a diagnosed C# compiler failure**. Git status similarly failed at the terminal tool. Automated YAML parser/GUID existence checks could not be run through this terminal; reference checks above are direct readback/source inspection.
- Unity import, NGO code generation, actual Host+Client transport, visual sizing, popup position, input timing, 10-decision/duplicate scenarios and effect snapshot round trips have NOT been executed. Compile and runtime acceptance remain outstanding.

## Remaining gaps / follow-up

The latest parent instruction narrowed priority to preparation UI/draft; broader battle/scene work is not completed here. The configurable transitionDelaySeconds hook waits after both connected registered owners complete and calls the existing NotifyBothPlayersConfirmed/StartBattle entry once. It does not load another scene or hand FinalDeck to the battle CharacterManagers. Existing battle Inspector startDeck, battle private hand synchronization, shared handsRoot and broader HP/cost/turn/win runtime acceptance remain follow-up gaps. No claim of complete scene migration or final-deck battle integration is made.

Candidate upgrade cards show the current confirmed target values; the upgrade is applied only after server acceptance and the upper panel then shows actual updated values. The offered upgrade delta is not separately labeled in the candidate surface. Mouse clicks are not bound because the real supplied Select UI is an InputAction map with Z, not a scene Button. Final review verdict remains CHANGES_REQUESTED; this implementation report does not self-approve review.json.
