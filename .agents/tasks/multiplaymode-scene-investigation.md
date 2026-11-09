# MultiPlayMode 씬 조사 보고서

## 요약 답변

현재 `MultiPlayMode` 씬과 관련 코드는 **실제 NGO ClientId 2명 등록, 서버 권위의 플레이어 역할 결정, 플레이어별 준비 선택지 생성/보관, 카드 선택 요청의 서버 전달, 기본적인 서버 전투 요청 흐름**까지 일부 구현되어 있습니다. 그러나 사용자가 요청한 전체 흐름은 아직 연결되지 않았습니다.

가장 큰 문제는 `NetworkMatchBridge`의 준비 확정 및 skip RPC가 서버에서 `MultiPreparationManager`를 호출한 뒤 결과를 클라이언트에 보내는 코드가 주석으로 끝난다는 점입니다. 따라서 클라이언트가 1회 선택 후 다음 후보를 받지 못하고, `FinalDeck`도 MultiPlayMode의 `CharacterManager_Multi`에 전달되지 않습니다. 또한 양쪽 10회 완료 시 `MapManager_Multi.NotifyBothPlayersConfirmed()`가 즉시 `GameManager_Multi.StartBattle()`을 호출하므로, 요구된 양쪽 완료 후 서버 권위 delay가 없습니다.

`Assets/_Recovery`는 비어 있지 않습니다. 현재 `Assets/_Recovery/0.unity`와 `Assets/_Recovery/0.unity.meta`가 남아 있습니다. 이번 조사는 읽기 전용으로 수행했으며 코드, 씬, 복구 파일을 수정하지 않았습니다. Unity Editor 실행이나 실제 Host/Client 런타임 검증은 수행할 수 없었습니다.

## 조사 기준과 주요 파일

- Unity 버전: `ProjectSettings/ProjectVersion.txt:1-2` — Unity `6000.3.11f1`
- NGO 패키지: `Packages/manifest.json` — `com.unity.netcode.gameobjects: 2.13.0`
- 조사 대상 씬: `Assets/Scenes/MultiLobbyScene.unity`, `Assets/Scenes/MultiPlayMode.unity`, 비교 대상 `Assets/Scenes/SinglePlayMode.unity`
- Multi 준비 UI/입력: `Assets/scripts/HyeGyoServer/MultiServerCardSelection/MapManager_Multi.cs`
- 서버 준비 상태/10회 카운터: `Assets/scripts/HyeGyoServer/MultipLayMode/MultiPreparationManager.cs`
- 후보 생성: `Assets/scripts/HyeGyoServer/MultipLayMode/CardOptionGenerator.cs`
- NGO 준비 RPC: `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/NetworkMatchBridge Separate/NetworkMatchBridge.cs`
- 접속자 등록: `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/NetworkLauncher분리버전/PlayerPreparationRegistry.cs`
- 플레이어 준비 데이터: `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/whileChoosingCard/PlayerPreparationData.cs`
- Multi 전투 진입: `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/MultiPlayMode_Scripts/임시방편게임구조/Game/GameManager_Multi.cs`, `BattleManager_Multi.cs`
- 전투 캐릭터: `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/MultiPlayMode_Scripts/임시방편게임구조/Game/CharacterManager_Multi.cs`
- Multi 역할/공개 상태: `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/MultiPlayMode_Scripts/MultiPlayGameBootstrap.cs`, `GameNetworkState.cs`, `MultiPlayGamePresenter.cs`
- Single 카드 정보 표시 기준: `Assets/scripts/MapManager.cs`, `Assets/scripts/Cards/CardVisual.cs`, `Assets/scripts/Cards/DeckDisplay.cs`, `Assets/Scenes/SinglePlayMode.unity`

## 씬과 직렬화 연결

`Assets/Scenes/MultiPlayMode.unity`에는 다음 오브젝트와 컴포넌트가 직렬화되어 있습니다.

- `MatchServerController` — line 226 부근
- `GameNetworkRoot` — line 461 부근; `MultiPlayGameBootstrap` 컴포넌트는 line 519 부근
- `DebugsCollection` — line 623 부근; `MultiPlayModeEntryDebug`의 `mapManagerRoot`는 line 641 부근
- `CardOptionGenerator` — line 908 부근
- `MultiPreparationManager` — line 1320 부근, 컴포넌트 line 1337 부근
- `MultiGameRoot` — line 1641 부근
- `Player` — line 1954 부근
- `EnemyManager` — line 2088 부근; `CharacterManager_Multi` line 2105 부근
- `NetworkMatchState` — line 2783 부근
- `PlayerInputManager` — line 3262 부근
- `BattleManager` — line 3465 부근; `BattleManager_Multi` line 3527 부근
- `PlayerpreparationRegistry` — line 6237 부근
- `MapManager_Multi` 컴포넌트 — line 6319 부근. `preparationViewRoot`는 연결되어 있지만 `myDeckDisplay`는 line 6323에서 `{fileID: 0}`으로 비어 있음.
- 두 번째 `CharacterManager_Multi` 직렬화 컴포넌트가 line 7482 부근에 존재하며, 씬에는 Inspector `startDeck` 데이터가 남아 있음.
- `BattleManager_Multi.playerCharacterManager`와 `enemyCharacterManager`는 각각 line 3531-3532에서 씬 객체를 참조합니다.

`MapManager_Multi.FindOptionVisuals()`는 Inspector 참조 대신 다음 이름/경로를 런타임에 찾습니다(`Assets/scripts/HyeGyoServer/MultiServerCardSelection/MapManager_Multi.cs:78-117`).

- `MapSelectPanel/Panel1/EnemyDisplay`
- `MapSelectPanel/Panel2/EnemyDisplay`
- `MapSelectPanel/Panel3/EnemyDisplay`

이 방식은 사용자가 말한 name-based GameObject lookup과 일치합니다. 다만 문자열 경로가 씬 계층과 정확히 일치해야 하며, 누락 시 세 옵션 중 해당 visual은 null로 남습니다. `MapManager.cs`의 Single 씬 쪽은 `GetComponentsInChildren<MapVisual>(true)`로 캐시하므로, 두 씬의 UI 탐색 방식은 서로 다릅니다.

## 요구 19단계 매핑

상태 표기: **구현**은 소스상 경로가 존재함, **누락**은 코드상 연결이 끊겼거나 요구 동작이 없음, **미검증**은 소스만으로 런타임 성공을 확정할 수 없음을 뜻합니다.

| 단계 | 요구 | 현재 판단 | 근거 및 문제 |
|---:|---|---|---|
| 1 | Lobby Host 방 생성 | 미검증 | `MatchServerController`가 Host 시작 요청을 검사하지만 실제 Relay/transport 방 생성 성공은 런타임 확인이 필요함. `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/NetworkMatchBridge Separate/MatchServerController.cs:80-130` 부근에서 Host 권한과 서버 상태를 검사함. |
| 2 | 실제 Client 1명 입장 | 미검증 | `NetworkConnectionMonitor` 이벤트를 받는 구조는 있으나 실제 두 프로세스 접속은 실행하지 않음. |
| 3 | 실제 NGO ClientId 2개 등록 | 부분 구현 | `PlayerPreparationRegistry.cs:50-104`가 `ConnectedClientsList`를 읽고 최대 2명을 실제 `clientId` 키로 등록함. 실제 연결/재접속은 미검증. |
| 4 | Host가 게임 시작 | 부분 구현 | `MatchServerController.TryStartMatch()`가 Host만 허용하고 `ConnectedClientsList.Count == 2`를 검사함. 시작 버튼부터 RPC 연결까지 런타임 미검증. |
| 5 | Server가 각 플레이어 카드 후보 생성 | 부분 구현 | `MultiPreparationManager.TryBeginForPlayer()`가 sender `clientId`별로 `CardOptionGenerator.GenerateOptions(player, round)`를 호출함(`MultiPreparationManager.cs:30-137`). 별도의 초기 카드 후보 서비스인 `CardSelectionServerService.TryChooseCard()`는 여전히 “Blank”로 false를 반환(`CardSelectionServerService.cs:193-211`). 두 준비 시스템이 혼재함. |
| 6 | Host/Client가 각자 자기 후보 Panel 표시 | 부분 구현/미검증 | 서버 응답은 target client를 검사하는 `SendPreparationOptionsRpc`와 `MapManager_Multi.ShowOptions()`로 자기 UI를 갱신함(`NetworkMatchBridge.cs:687` 부근). 실제 양쪽 화면 분리는 런타임 미검증. |
| 7 | 각자 독립 선택 | 부분 구현 | `currentOptionsByClient`와 `currentRoundByClient`가 clientId별 Dictionary임(`MultiPreparationManager.cs:15-26`). 그러나 확정 결과의 다음 옵션 전송이 주석 처리되어 실제 연속 선택은 동작하지 않을 가능성이 높음. |
| 8 | Server가 두 선택 검증/저장 | 부분 구현 | 옵션 index, 현재 옵션 목록, 대상 카드 index를 서버에서 검증함(`MultiPreparationManager.cs:146-222`). AddCard는 clone 후 `FinalDeck`에 저장하지만 Enhance는 유효 index만 검사하고 실제 변경은 하지 않음(`MultiPreparationManager.cs:337-356`). 카드 후보 선택 서비스 자체는 미연결. |
| 9 | 강화/추가 준비 | 부분 구현 | `PreparationOptionType.AddCard`, `EnhanceCard`, `Skip` 처리와 후보 생성은 있음. 강화 데이터 적용은 TODO 상태이고 `MapManager_Multi.ApplyConfirmedOptionToLocalView()`도 Enhance 시 덱을 다시 그릴 뿐 실제 카드 변경을 반영하지 않음(`MapManager_Multi.cs:397-421`). |
| 10 | 각 FinalDeck 확정 | 누락 | 서버 메모리의 `PlayerPreparationData.FinalDeck`에는 AddCard 결과가 쌓이지만, “정확히 10회 완료 후 FinalDeck 확정”을 별도 상태로 전파/봉인하지 않음. `ConditionCompleted`만 true가 됨(`MultiPreparationManager.cs:230-237`). 클라이언트에 FinalDeck 전달하는 부분은 주석임(`NetworkMatchBridge.cs:785` 부근). |
| 11 | 둘이 함께 MultiPlayMode 이동 | 부분 구현/누락 | Host의 `TryLoadGameplayScene()`은 SceneManager로 MultiPlayMode를 로드하는 경로를 갖지만, 현재 준비 완료 경로 `CheckAllPlayersCompleted()`는 씬 전환이 아니라 `MapManager_Multi.NotifyBothPlayersConfirmed()`를 호출함(`MultiPreparationManager.cs:362-381`). 이미 MultiPlayMode에 진입한 뒤 준비 UI를 띄우는 구조와 Lobby에서 전환하는 구조가 혼재함. |
| 12 | Server가 Player0/Player1 결정 | 구현 | `MultiPlayGameBootstrap.InitializePlayersServer()`가 연결된 두 ClientId를 읽고 작은 ID를 Player0으로 정렬함(`MultiPlayGameBootstrap.cs:21-52`). 두 명일 때만 초기화함. |
| 13 | Lobby에서 확정한 FinalDeck 연결 | 누락 | `BattleManager_Multi.InitializeBattleServer()`가 주석으로 “나중에 FinalDeck 연동”이라고 남기고 두 `CharacterManager_Multi`에 `InitializeConfiguredCharacterServer()`를 호출함(`BattleManager_Multi.cs:200-215`). 씬 Inspector `startDeck`를 복제할 뿐 Registry의 FinalDeck을 읽지 않음. |
| 14 | CharacterManager_Multi 2개 생성/초기화 | 부분 구현 | 씬에 두 CharacterManager와 BattleManager 양쪽 참조가 직렬화되어 있고, 서버에서 기존 객체를 초기화함. 요구는 FinalDeck 기반 초기화인데 현재는 Inspector `startDeck` 기반임. 동적 생성은 확인되지 않음. |
| 15 | Server 덱 셔플/손패/전투 진행 | 부분 구현 | `CharacterManager_Multi.CharacterInit()`에서 서버만 덱 생성/셔플/손패 채움을 수행함(`CharacterManager_Multi.cs:146-177`). 실제 전투 tick 경로는 존재하지만 두 클라이언트 런타임 전투는 미검증. |
| 16 | Client는 행동 요청만 전송 | 부분 구현 | `CharacterManager_Multi.SelectCard()`가 직접 상태 변경 없이 `BattleManager_Multi.RequestSelectCard()`를 호출하고, `RequestSelectCardServerRpc`에서 서버가 실행함(`CharacterManager_Multi.cs:426-464`, `BattleManager_Multi.cs:590-649`). |
| 17 | Server 판정 | 부분 구현 | `SelectCardServer()`와 서버 권위 `CharacterManager_Multi` 상태 변경 구조가 있음. 카드 효과 전체, 승패의 실제 네트워크 런타임 결과는 미검증. |
| 18 | 공개 상태 + 각자 개인 상태 동기화 | 부분 구현/위험 | `GameNetworkState`의 HP/방어/코스트/guard NetworkVariable은 Everyone 읽기 권한이라 공개 상태는 있음. `CharacterManager_Multi.SyncPublicStateServer()`가 이를 갱신함. 그러나 손패/덱을 owner에게만 보내는 개인 NetworkList/Target RPC 구조는 확인되지 않았고, 현재 손패 표시 연결은 검증 필요. |
| 19 | 승패 | 부분 구현/미검증 | `GameNetworkState.WinnerClientId`, `MultiMatchState` 및 BattleManager 상태가 존재하지만 실제 HP 0→winner→종료 UI/전환을 Host+Client로 확인하지 못함. |

## 드래프트 규칙 매핑

- **카드+강화 합계 정확히 10회:** 서버 `MaxRounds = 10`, client별 `currentRoundByClient`, `TryConfirmOption()`/`TrySkip()`의 10회 제한은 구현되어 있음(`MultiPreparationManager.cs:8, 118, 230-237, 454-458`). 그러나 확정 결과 RPC와 FinalDeck 확정/전달이 빠져 end-to-end는 누락입니다.
- **첫 회 카드 선택, 강화 가능 카드가 없으면 내부 후보 재생성:** 현재 `CardOptionGenerator.GenerateOptions(player, round)`는 `round`를 받지만 사용하지 않습니다. 첫 회를 별도로 처리하지 않으며, 첫 옵션은 강화 가능 카드가 없을 때 AddCard로 바꾸는 정도입니다(`CardOptionGenerator.cs:11-48`). “내부 reload”의 명시적 횟수/재생성 정책은 없습니다.
- **두 번째부터 카드 1개 + 유효 강화 1개 + 랜덤 1개:** 현재 옵션 0은 강화 가능 시 강화, 옵션 1은 항상 새 카드, 옵션 2는 50% 강화/카드입니다(`CardOptionGenerator.cs:16-42`). 따라서 방향은 유사하지만, 강화는 `EnhanceId = 0` 하드코딩이고 실제 강화 가능성/강화 효과가 구현되지 않아 “유효 강화”를 보장하지 못합니다.
- **“두번째부터는 10개”와 “총 10회” 충돌:** 현재 코드와 UI가 명확히 `10`을 사용하므로 실제 구현 기준은 플레이어당 총 10회입니다. `MapManager_Multi.SetRemainingChoices()`가 `remaining/10`을 표시하고(`MapManager_Multi.cs:453-459`), 서버도 10회에서 완료합니다. 권고는 “첫 회를 포함해 총 10회”로 요구사항을 확정하고, 문서/UI에서 “2회차부터 10회” 표현을 “총 10회 중 2회차부터 고정 슬롯 규칙 적용”으로 정정하는 것입니다.
- **카드 추가/강화/skip:** AddCard와 Skip은 서버 함수가 있고, Enhance 경로는 대상 index만 검사하고 실제 강화하지 않습니다. skip 입력은 `RequestSkipPreparation()`까지 있으나 server RPC 결과 전달이 주석입니다(`NetworkMatchBridge.cs:792-829`).
- **약한 기본 공격/방어:** Multi 전투에는 `DefenseAction = -1`, guard 및 카드 전투 코드가 있으나 “준비 후보에 약한 공격/방어 카드를 항상 보장”하는 로직은 확인되지 않았습니다. `CardOptionGenerator.cardPool`이 Inspector에서 무엇을 포함하는지는 씬에 후보 목록이 직렬화되어 있지 않아 런타임/Inspector 확인이 필요합니다.
- **reload 버튼/비용/token:** 준비 UI 코드에서 reload 버튼이나 내부 비용을 구현한 흔적은 확인되지 않았습니다. `CharacterManager_Multi`의 `RedrawAction`은 전투 중 손패 redraw와 cost 감소 기능이므로 준비 후보 재생성과는 별개입니다. token 시스템도 준비 흐름에서 확인되지 않았습니다.
- **입장 시 비용만 부과:** 입장 비용 로직은 이번 조사 대상 흐름에서 확인하지 못했습니다. 내부 선택/재생성 비용이 없는 것은 코드상 준비 manager에 비용 차감이 없다는 점에서 부분적으로 부합하지만, 입장 시 과금 자체는 미검증입니다.
- **deck 표시 중 기존 선택 UI disable:** `MapManager.cs` Single 구현은 deck view를 켜고 입력 context를 `Select`로 교체한 뒤 Cancel에서 복구합니다(`MapManager.cs:112-166`). Multi `MapManager_Multi`에는 `myDeckDisplay`가 씬에서 null이고, 준비 선택 중 deck view를 여는 동일한 상호 배제 입력 흐름이 없습니다. 요구사항은 누락입니다.
- **SinglePlayMode 카드 정보 표시 의미:** Single `MapManager.RefreshDeckSelectionPopup()`은 현재 선택 카드의 모든 `CardEffect`를 원래 순서대로 `PopupManager.Show()`에 전달합니다(`MapManager.cs:86-110`). `CardVisual`/`DeckDisplay`는 카드 정의를 시각화합니다. Multi `PreparationOptionVisual_Multi`는 옵션을 표시하지만 현재 소스에서 Single의 `PopupManager` effect 목록을 연결하는 경로는 찾지 못했습니다. 따라서 카드 정보 표시가 Single semantics를 따른다고 확인할 수 없습니다.

## 심각도별 결론

### 심각 — 준비 흐름이 확정되지 않음

`NetworkMatchBridge.RequestConfirmPreparationOptionRpc()`와 `RequestSkipPreparationRpc()`는 서버 검증 호출 뒤 “remaining + FinalDeck + nextOptions 전달” 주석에서 끝납니다(`NetworkMatchBridge.cs:761-829`). 이 때문에 첫 선택 이후 UI가 다음 후보를 받지 못하고, 서버가 저장한 선택 결과가 클라이언트 deck 표시나 다음 라운드로 연결되지 않습니다. 우선 이 RPC 결과 전송과 client 대상 필터링을 완성해야 합니다.

### 심각 — FinalDeck이 전투에 연결되지 않음

`PlayerPreparationData.FinalDeck`은 서버 Registry에만 존재합니다. `BattleManager_Multi`는 `InitializeConfiguredCharacterServer()`를 호출하고, 그 메서드는 Inspector `startDeck`을 복제합니다(`CharacterManager_Multi.cs:184-224`). 따라서 양쪽이 선택한 덱으로 전투하는 요구는 현재 충족되지 않습니다.

### 높음 — 양쪽 완료 후 delay 및 중복 전환 방지 없음

`CheckAllPlayersCompleted()`는 두 `ConditionCompleted`가 true이면 곧바로 `MapManager_Multi.NotifyBothPlayersConfirmed()`를 호출하고, 그 함수는 즉시 `GameManager_Multi.StartBattle()`을 호출합니다(`MultiPreparationManager.cs:362-381`, `MapManager_Multi.cs:376-394`). Inspector 조정 가능한 delay 필드, 서버가 시작하는 countdown, 한 번만 시작하는 transition guard가 없습니다. Disconnect/late join 시 Registry에서 플레이어를 제거하지만 이미 완료 처리/전환 중인 coroutine 취소나 match abort 정책은 없습니다.

### 높음 — 강화는 이름만 있고 데이터 변경이 없음

`ApplyEnhance()`는 target index 범위만 확인하고 true를 반환합니다(`MultiPreparationManager.cs:337-356`). `CardDefinition` clone 또는 `CardUpgrade` 적용이 없습니다. “유효 강화 옵션”을 보장하려면 강화 가능 목록과 실제 upgrade 적용 결과를 서버에서 정의해야 합니다.

### 중간 — Multi 카드 정보 UI가 Single 흐름과 분리됨

Single은 선택 카드의 전체 effect 목록을 PopupManager에 전달하지만 Multi 준비 visual에는 같은 데이터 source/PopupManager 호출이 없습니다. 또한 `MultiPlayMode.unity`의 `MapManager_Multi.myDeckDisplay`가 null입니다. 카드 후보를 선택하고 상세 정보를 확인하는 사용자 흐름을 구현할 때 씬 이름 경로와 Single의 popup semantics를 통일해야 합니다.

### 중간 — 공개/개인 상태 경계가 불완전함

HP/방어/코스트 등 공개 NetworkVariable은 확인되지만, 개인 손패/덱 상태를 owner 대상 RPC/NetworkVariable로 분리하는 연결은 확인하지 못했습니다. 실제 2-client 실행으로 상대방에게 손패가 노출되는지 반드시 확인해야 합니다.

### 보조 — 복구 파일이 남아 있음

`Assets/_Recovery/0.unity`와 `Assets/_Recovery/0.unity.meta`가 아직 존재합니다. 사용자가 수동으로 해결했다고 했지만, 저장소 상태에는 복구 파일이 남아 있으므로 Unity import/branch conflict 화면이 계속 표시되는지 별도로 확인해야 합니다. 이번 조사에서는 삭제하지 않았습니다.

## 권장 구현 순서

1. `NetworkMatchBridge`의 confirm/skip server RPC를 완성합니다. 서버가 sender의 현재 options와 index를 검증하고, 선택 결과·remaining·다음 3개 후보를 **sender에게만** 보내도록 구현합니다. 완료 시 다음 후보 없이 waiting 상태를 보내고, 중복 confirm/완료 client의 요청을 거절합니다.
2. `CardOptionGenerator`에 `round == 0` 첫 회 정책과 `round >= 1` 고정 슬롯 정책을 명시합니다. 총 횟수는 현재 코드와 UI에 맞춰 10회로 확정하고, 옵션 1개는 실제 강화 가능한 target과 유효 `EnhanceId`를 가져야 합니다. cardPool이 비었을 때의 약한 기본 공격/방어 fallback도 서버에서 결정합니다.
3. `ApplyEnhance()`에 실제 `CardDefinition`/`CardUpgrade` 변경을 구현하고, AddCard/Enhance/Skip 모두 서버의 `FinalDeck` 또는 runtime card 상태에만 적용합니다. 클라이언트는 서버가 보낸 결과를 표시만 하도록 합니다.
4. `PlayerPreparationData`와 `PlayerPreparationRegistry`에 `FinalDeckConfirmed` 또는 동등한 서버 전용 완료 상태를 명시하고, 각 client가 정확히 10회 끝났을 때만 true로 고정합니다. 양쪽 완료 전에는 SceneManager 전환이나 BattleStart를 호출하지 않습니다.
5. 양쪽 완료를 서버가 감지한 뒤 시작하는 coroutine을 `MatchServerController` 또는 명확한 서버 전용 flow manager에 둡니다. `transitionDelaySeconds`를 `[SerializeField]`로 Inspector에 노출하고, countdown 상태를 NetworkVariable/target RPC로 표시한 뒤 delay가 끝나면 한 번만 MultiPlayMode로 전환합니다. 중복 호출 guard, client disconnect 취소/abort, late join 거부를 함께 정의합니다.
6. Lobby Registry의 두 FinalDeck을 MultiPlayMode의 server-side handoff로 연결합니다. 현재 씬 변경 방식에 맞춰 persistent server service 또는 serializable network handoff를 하나로 선택하고, Player0/Player1 ClientId와 FinalDeck을 매칭한 뒤 두 `CharacterManager_Multi.CharacterInit(finalDeck, maxHealth)`를 호출합니다. `InitializeConfiguredCharacterServer()`의 Inspector `startDeck` fallback은 개발용으로 분리하거나 제거합니다.
7. `MapManager_Multi`에 Single `MapManager.RefreshDeckSelectionPopup()`과 동일한 effect 목록 전달을 적용하고, Multi 씬에 PopupManager/DeckDisplay를 실제로 연결합니다. deck detail 입력 중 preparation option input을 잠그고 Cancel에서 복구하며, 기존 선택 UI가 다시 활성화되지 않은 채 남지 않도록 상태를 한 곳에서 관리합니다.
8. 네트워크 상태 경계를 점검합니다. 공개 HP/방어/턴/승패는 Everyone, 손패/덱/선택 후보는 해당 owner만 읽도록 NetworkVariable/RPC 수신 대상을 분리합니다. 서버만 shuffle, deal, effect, damage, winner를 결정하도록 유지합니다.
9. 실제 2-client 런타임 검증을 수행하고 ClientId, 후보 목록, 각 client의 UI, 10/10 완료, delay, FinalDeck count/card names, Player0/1, 첫 손패, 상대 손패 비공개, 행동 RPC, HP/승패를 양쪽 로그와 화면에서 확인합니다.

## 런타임 검증 절차

현재 환경에서는 Unity Editor/두 프로세스 실행을 하지 못했으므로 아래 절차가 필요합니다.

1. Unity `6000.3.11f1`로 프로젝트를 열고 Console을 Clear합니다. `Assets/_Recovery`에 `0.unity`와 `.meta`가 남아 있는 상태도 확인합니다.
2. Build Settings/Scene list에 `MultiLobbyScene`과 `MultiPlayMode`가 포함되어 있는지 확인합니다. Host/Client가 같은 프로젝트 빌드 또는 Editor+빌드로 실행되도록 준비합니다.
3. Host에서 Lobby를 실행하고 방을 만든 뒤, Client 프로세스에서 접속합니다. Server Console에 `실제 플레이어 등록`, `ClientId`, `Count: 2/2` 로그가 두 ID에 대해 나타나는지 확인합니다.
4. Host에서 Start를 누릅니다. Client가 Start를 누르거나 임의의 UI 입력을 보내도 Host 권한 검사가 유지되는지 확인합니다. 두 프로세스가 같은 씬에 들어간 뒤 `MultiPlayModeEntryDebug`의 LocalClientId/IsHost/IsServer/IsClient 로그를 저장합니다.
5. 양쪽 화면에 서로 다른 후보 카드가 표시되는지 확인합니다. Host에서 옵션을 움직이고 선택해도 Client의 `_selectedIndex`/highlight가 바뀌지 않는지, 반대로도 확인합니다.
6. 양쪽에서 1회 선택합니다. 서버가 각 sender에 대해 선택 결과와 다음 후보를 보내는지 확인합니다. 한쪽을 먼저 선택했을 때 그쪽만 다음 후보를 받고 상대는 현재 후보를 유지해야 합니다.
7. AddCard, Enhance, Skip을 각각 최소 한 번씩 실행합니다. 서버 로그에서 해당 client의 `FinalDeck` count와 실제 card/upgrade 결과를 확인하고, 상대 client 화면에 개인 덱 세부가 노출되지 않는지 확인합니다.
8. 각 client가 총 10회를 완료하되 한쪽을 먼저 10/10까지 끝냅니다. 먼저 끝난 client는 waiting 상태여야 하고 Battle/Scene 전환이 일어나면 안 됩니다. 둘째 client도 10/10을 완료한 뒤에만 서버가 delay를 시작해야 합니다.
9. Inspector의 `transitionDelaySeconds` 값을 예를 들어 3초로 설정하고, 양쪽 완료 순간부터 양쪽 화면의 countdown 및 실제 전환 시점을 측정합니다. 같은 완료 이벤트로 SceneManager가 두 번 호출되지 않는지 확인합니다.
10. 전환 후 서버 로그에서 Player0/Player1 ClientId, 각 FinalDeck의 count와 카드 식별자, 두 CharacterManager 초기화 source가 일치하는지 확인합니다. Inspector의 기본 `startDeck`과 다른 선택 결과가 실제 첫 손패에 반영되어야 합니다.
11. 한쪽에서 카드 선택/방어/기타 행동을 수행하고, Client 로그에는 요청 전송만, Server 로그에는 검증·효과·공개 상태 갱신만 나타나는지 확인합니다. 상대 손패와 개인 덱이 상대 화면에 나타나지 않는지 확인합니다.
12. HP를 줄여 한쪽을 0으로 만들고, 두 화면의 winner와 종료 처리가 동일한지 확인합니다.
13. 준비 중 Client를 disconnect하거나 3번째 Client를 접속시켜 Registry의 제거/거부 로그를 확인합니다. 양쪽 완료 직전/transition delay 중 disconnect가 발생했을 때 전환이 취소되거나 명시적으로 match abort 되는지 확인합니다. late join은 기존 match에 들어오지 못해야 합니다.

## SinglePlayMode 카드 표시 로직 추적

### 데이터 source와 카드 정의

Single 카드의 authoring source는 `Assets/scripts/Cards/CardDefinition.cs:5-29`의 `CardDefinition` ScriptableObject입니다. 직렬화된 `cooldown`, `cost`, `_effects`, `sprite`, `cardType`를 `GetCooldown()`, `GetCost()`, `GetEffects()`, `GetSprite()`, `GetCardType()`로 제공합니다. 강화가 적용된 runtime 정의는 `CardDefinition.Clone()`과 `ApplyUpgrade()`(`CardDefinition.cs:104-145`)로 원본 asset과 분리됩니다.

실제 전투 손패 데이터는 `CardInstance`가 보유합니다(`Assets/scripts/Cards/CardInstance.cs:5-21`). `CharacterManager`가 덱에서 `CardInstance`를 만들고, `HandManager.FillHand()`가 손패를 채운 뒤 visual을 연결합니다(`Assets/scripts/Battles/HandManager.cs:43-73`). `CardInstance.SetVisual()`은 순서대로 `CardVisual.SetCardDefinition(_definition)`을 호출하고 기본 cost/cooldown/effect 수치를 `RefreshCardVisual()`로 밀어 넣습니다(`CardInstance.cs:43-67`). 따라서 CardVisual은 데이터를 직접 조회하지 않고 CardInstance가 계산한 값을 받는 표시 계층입니다.

### 기존 카드 프리팹과 표시 필드

`Assets/scripts/Cards/CardVisual.cs:7-49`는 카드 프리팹의 이름 기반 자식 경로를 찾습니다.

- `Front`, `Back`
- `Front/background`, `Front/sprite`, `Front/sprite/background`
- `Front/effect`
- `Front/cost/CostText`
- `Front/cooltime/cooltimeText`
- `Front/SelectHighlight`
- `Front/IsUnplayable`

`CardVisual.SetCardDefinition()`은 `CardDefinition.GetCardType()`에 따라 `cardTypeBackgrounds`를 고르고, `GetSprite()`를 `Front/sprite`에 넣습니다(`CardVisual.cs:89-105`). 각 `CardEffect`의 `EffectType`은 `_pendingEffectTypes`로 저장되어 선택 시 popup의 데이터가 됩니다. `SetCostText()`, `SetCooldownText()`, `RefreshEffectDisplays()`가 각각 비용, 남은 쿨다운, 효과 아이콘/수치를 채웁니다(`CardVisual.cs:113-145`). `SetUnplayable()`은 비용 부족 overlay인 `Front/IsUnplayable`을 토글합니다.

`CardInstance.RefreshCardVisual()`은 effect를 reset한 뒤 `GetCost()`, `_cooldownLeft`, `_effects`를 위 필드에 반영합니다(`CardInstance.cs:53-67`). 전투 중 버프가 반영된 값은 `CardInstance.RefreshDisplay()`가 clone을 만들고 `ResolveUse(..., false)` 및 owner/target effect 계산을 한 뒤 preview cost/effects를 다시 visual에 씁니다(`CardInstance.cs:267-293`). 이 흐름이 Single의 실제 cost/stats 의미입니다. 카드 정의의 cooldown/cost와 runtime effect magnitude가 별도로 표시되며, `CardVisual`에는 별도의 description 문자열 필드가 없습니다.

### 팝업과 선택 semantics

`Assets/scripts/Popup/PopupManager.cs:12-29`는 화면별 `PopupManager`를 사용하고 자식 `PopupComponent`를 자동 검색합니다(`PopupManager.cs:38-57`). `Show(IReadOnlyList<EffectType>)`는 `BattleManager.GetEmoji(effectType)`와 `GameManager.GetEffectSummary(effectType)`로 아이콘과 설명을 만들고, page index를 0으로 초기화합니다(`PopupManager.cs:93-132`). `NextPage`/`PrevPage`는 `Popup` overlay input으로 처리됩니다(`PopupManager.cs:68-91`).

CardVisual은 `SetPopupManager()`로 화면별 popup을 받고(`CardVisual.cs:51-58`), `SetLayer("UI")`와 `SetSelected(true)`가 동시에 성립할 때만 `_pendingEffectTypes`를 popup에 보냅니다(`CardVisual.cs:147-158`). 즉 카드 select highlight와 effect detail popup은 같은 CardVisual 상태에서 연결됩니다.

Single의 deck 열기 흐름은 `Assets/scripts/PlayerDeckPanel.cs:5-41`입니다. 씬의 `DeckPanel` 오브젝트에는 `PlayerDeckPanel`과 `DeckDisplay`가 직렬화되어 있으며(`Assets/Scenes/SinglePlayMode.unity:534-569`), `Awake()`에서 panel을 비활성화하고 popup manager를 deck display에 전달합니다. `Open()`은 panel을 켜고 `PlayerManager.Instance.GetDeck()`을 `DeckDisplay.SetDeck()`에 넣은 다음 `Select` context를 로드하고 `SelectFirst()`를 호출합니다. `Close()`는 입력 context를 unload하고 `Deselect()`한 뒤 panel을 끕니다.

`Assets/scripts/Cards/DeckDisplay.cs:84-157`는 `Resources/Prefabs/Card`를 한 장씩 instantiate하여 `SpawnCard()`에서 `CardVisual`, 표시용 `CardInstance(def, null)`, `SetVisual()`, `SetFace(true)`, `SetLayer("UI")`를 순서대로 연결합니다(`DeckDisplay.cs:244-257`). 기존 카드 object는 `ClearCards()`에서 destroy되고, `SetDeck()` 재호출 때 새 카드가 생성됩니다. `SelectFirst()`는 첫 카드만 selected로 만들고, 이동 시 이전 카드 deselect 후 현재 카드 select를 적용합니다. 따라서 owner deck/상태 카드의 표시용으로 재사용할 수 있는 핵심 구성은 `DeckDisplay + CardInstance(def, null) + CardVisual + PopupManager`입니다.

Single의 전투 손패는 이와 별도로 `HandManager`가 `GameObjectPool`에서 카드 visual을 재사용합니다(`HandManager.cs:55-73`). `CardInstance.SetVisual()` 뒤 `SetFace(_isHandVisualized)`와 `RefreshDisplay()`를 호출합니다. `ApplySelection()`은 이전 카드만 해제하고 현재 카드만 켜서 popup이 마지막 슬롯의 deselect에 의해 덮어써지지 않도록 합니다(`HandManager.cs:180-203`).

### MapManager의 name-based lookup와 표시/비활성 타이밍

Single `MapManager`는 `Awake()`에서 `GetComponentsInChildren<MapVisual>(true)`로 후보 visual을 캐시합니다(`Assets/scripts/MapManager.cs:37-48`). 후보를 표시할 때 `ShowEnemySelection()`이 후보 수만큼 visual을 켜고 나머지는 끈 뒤 `SetCharacter()`를 호출합니다(`MapManager.cs:59-82`). 선택 변경 때 `RefreshSelectionHighlight()`가 visual highlight와 후보 popup을 갱신합니다. 덱 상세를 열면 `LoadDeckDisplayInput()`이 먼저 `SetDeckViewActive(true)`로 trophy를 끄고 deck display를 켠 뒤, **활성화 후** `RefreshOpponentDeckDisplay()`를 호출합니다(`MapManager.cs:112-166`). 이는 비활성 object에 instantiate/즉시 SetCardDefinition을 해서 Awake 순서가 깨지는 것을 막기 위한 명시적 순서입니다. deck context를 로드하는 동안 후보 선택 context의 입력은 deck context로 대체되고, Cancel에서 `Unload()`, `Deselect()`, `SetDeckViewActive(false)`, 후보 popup 복구가 진행됩니다.

Multi의 name-based lookup은 `MapManager_Multi.FindOptionVisuals()`가 `MapSelectPanel/Panel1/EnemyDisplay`, `Panel2/EnemyDisplay`, `Panel3/EnemyDisplay`를 `transform.Find()`하는 방식입니다(`Assets/scripts/HyeGyoServer/MultiServerCardSelection/MapManager_Multi.cs:78-117`). 이는 Single의 `GetComponentsInChildren` 캐시와 다르며, `MapManager_Multi`에는 Single의 `RefreshDeckSelectionPopup()`에 대응하는 카드 상세 popup 호출이 없습니다.

### Multi 현재 상태 카드 표시

Multi에는 Single의 별도 `CardVisual`을 그대로 쓰는 경로와 Multi 전용 경로가 혼재합니다. `CardVisual_Multi`는 `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/MultiPlayMode_Scripts/임시방편게임구조/Game/CardVisual_Multi.cs:9-30`에서 Single 카드 prefab 구조를 동일하게 가정하지만 `PopupDisplay_Multi`와 `EffectDisplay_Multi`를 내부에 직접 찾습니다. `SetCardDefinition()`은 sprite와 effect type을 채우고, `SetCostText()`, `SetCooldownText()`, `RefreshEffectDisplays()`로 카드 표면을 갱신합니다(`CardVisual_Multi.cs:127-211`). Multi `CardInstance_Multi.SetVisual()`/`RefreshDisplay()`도 같은 push 방식입니다.

Multi 전투 캐릭터는 `CharacterManager_Multi.Init()`에서 `HandManager_Multi`를 생성하고 `handsRoot`를 넘깁니다(`Assets/scripts/HyeGyoServer/MultiplayLobbyScene/MultiPlayMode_Scripts/임시방편게임구조/Game/CharacterManager_Multi.cs:540-558`). `HandManager_Multi`는 `Resources/Prefabs/Card`를 instantiate하고 `GetComponent<CardVisual>()`을 찾습니다(`HandManager_Multi.cs:22-32, 73-96`). 여기서 `CardVisual_Multi`를 찾지 않고 Single `CardVisual`을 찾으므로, 씬/프리팹이 실제로 어떤 컴포넌트를 갖는지 런타임 확인이 필요합니다. 또한 `HandManager_Multi`는 현재 popup manager를 생성자에 받지 않고, owner/remote 손패 데이터를 분리하는 별도 UI binding도 없습니다.

`MultiPlayMode.unity`에서 `Player`와 `EnemyManager`는 각각 `CharacterManager_Multi`를 가지고 있으며 두 컴포넌트의 `isHandVisualized: 1`, `handsRoot`가 line 2117-2118 및 line 7494-7495 부근에 있습니다. 두 캐릭터가 같은 `handsRoot` fileID를 가리키는 것으로 직렬화되어 있어, 각자 상단 owner 상태 카드라는 요구와 현재 hand object 구조가 일치하는지 위험합니다. `MapManager_Multi`의 `myDeckDisplay`와 `remainingText`는 line 6323 부근에서 `{fileID: 0}`이며, 상단 owner 상태용 `DeckDisplay`/popup/카드 상태 panel의 연결은 확인되지 않습니다.

## 상단 owner 상태 카드 권고

Single semantics를 재사용할 때는 `CardDefinition` asset 자체를 client가 임의로 읽게 하지 말고, 서버가 owner에게만 전송한 카드 상태 snapshot을 owner client에서 `CardDefinition` 또는 표시용 immutable model로 복원해야 합니다. 표시 계층은 `CardVisual`이 이미 cost, cooldown, effects, icon, card type background, unplayable overlay를 채우므로 `DeckDisplay.SpawnCard()`의 `CardInstance(def, null)` 패턴과 `CardVisual.SetPopupManager()`를 재사용하는 것이 가장 작은 경계입니다.

권장 분리는 다음과 같습니다.

1. 서버 `CharacterManager_Multi`가 owner별 현재 손패/상태 카드 데이터만 Target RPC 또는 owner read permission 상태로 전송합니다. opponent client에는 card definition, cost, cooldown, effect magnitude, upgrade state를 보내지 않습니다. 공개 HP/방어/턴은 기존 `GameNetworkState`처럼 Everyone 상태로 유지합니다.
2. Multi client의 owner 전용 `TopOwnerStatusPanel`을 `MultiPlayMode.unity`에 새로 두고, `DeckDisplay` 또는 작은 fixed-slot view가 owner snapshot을 받아 카드 visual을 생성합니다. 카드 surface는 `CardVisual`/`CardInstance`를 사용하되 전투용 `CardInstance_Multi`와 상태 계산을 공유하지 않습니다.
3. `CardDefinition`에 이미 있는 `cost`, `cooldown`, `effects`, `sprite`, `cardType`를 snapshot source로 채우고, runtime 강화는 `CardDefinition.Clone()` 뒤 `ApplyUpgrade()`를 적용하여 표시합니다. 현재 CardDefinition/CardVisual에는 별도 description/upgrade badge 필드가 없으므로 upgrade state를 표시하려면 별도 TMP label 또는 CardVisual의 명시적 `SetUpgradeState` 경계를 추가해야 합니다.
4. effect detail은 Single `PopupManager.Show(effectTypes)` semantics를 재사용합니다. Multi popup을 계속 사용할 경우 `PopupDisplay_Multi`를 owner panel에 연결하고 동일한 EffectType→emoji/summary 변환을 제공해야 합니다. 상대 손패 카드에는 `PopupManager`와 select input을 연결하지 않습니다.
5. owner panel은 battle state가 시작될 때만 활성화하고, hand/deck snapshot 수신 시 기존 카드 visual을 clear 후 새 상태를 표시합니다. 준비 선택 panel이 열려 있을 때는 owner status panel을 disable하거나 입력만 잠그고, 준비 선택 confirm/10회 완료/scene transition 때는 기존 `PlayerInputManager` context를 unload하여 중복 선택을 막아야 합니다. Single `PlayerDeckPanel.Open/Close()`와 `MapManager.LoadDeckDisplayInput()`의 activate→populate→input load→SelectFirst 및 unload→Deselect→disable 순서를 그대로 따르는 것이 안전합니다.
6. `MapManager_Multi`의 문자열 경로 lookup은 옵션 panel에만 유지하고, 상단 owner panel은 Inspector serialized reference를 사용합니다. 카드 상태 panel을 name-based로 찾아야 한다면 `transform.Find("TopOwnerStatusPanel")`처럼 한 곳에서만 검증하고, 실패 시 명시적 error를 내도록 합니다. 기존 Multi `handsRoot`를 양쪽 캐릭터가 공유하는지 먼저 분리하여 owner client의 상단 panel과 remote 상태 표시가 섞이지 않게 해야 합니다.

## 조사 결론

`MultiPlayMode.unity` 자체에는 준비 화면, 네트워크 상태, 전투 UI와 두 CharacterManager의 뼈대가 들어 있습니다. 사용자가 원하는 “각자 독립적인 카드 선택”을 위한 clientId별 서버 Dictionary도 시작점으로는 적절합니다. 하지만 현재는 준비 RPC 결과 전달과 FinalDeck handoff가 끊겨 있고, 강화가 실제로 적용되지 않으며, 완료 후 delay/중복 전환/접속 이탈 정책이 없습니다. 추가로 SinglePlayMode의 카드 표시 semantics는 `CardDefinition → CardInstance → CardVisual` push 흐름과 화면별 `PopupManager`를 중심으로 동작하지만, Multi 상단 owner 상태 panel과 owner-only snapshot 연결은 아직 없습니다. 다음 구현은 서버 확정 상태와 FinalDeck handoff를 완성한 뒤 owner 전용 상태 전달을 분리하고, 그 다음 Single 카드 표시 계층과 popup semantics를 Multi 상단 panel에 연결하는 순서가 안전합니다.
