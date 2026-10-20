# MultiPlayMode 카드 생성 및 상단 표시 구현 계획

## 조사 범위와 결론

이 단계는 계획만 수정한다. 코드/씬 구현, 빌드, 테스트, commit을 수행하지 않으며 `.git` mutation은 계획과 후속 구현 모두 금지한다. `.agents/tasks/multiplaymode-scene-investigation.md`, design-review.md, review.json 및 실제 source/scene/prefab/package source를 확인했다. review.json은 CHANGES_REQUESTED이며 아래 계약은 HIGH 1–4와 MEDIUM 5–9를 모두 다룬다.

부모 세션의 명시적 결정: `PanelGameObject`는 로컬 플레이어가 서버에서 확정한 **누적 준비 카드와 실제 강화 상태**만 표시한다. 현재 회차 후보 상세는 기존 후보 Panel에서 표시한다. 두 집합을 상단에 섞지 않는다. 선택 요청이 서버에서 승인된 직후 owner snapshot으로 상단을 갱신한다. 강화 target 검증도 동일 owner 상태를 기준으로 한다. 전투 FinalDeck handoff는 범위 밖이다.

## (A) 재사용할 Single/Multi 카드 생성 및 표시 semantics

### 실제 Single producer와 재사용 경계 (HIGH 1)

- `Assets/scripts/GameManager.cs:285`의 `ShowEnemySelection()`은 `_weakEnemyCandidatePool`/`_enemyCandidatePool`에서 `PickRandomDistinct(pool, EnemySelectionCount - 1)`로 서로 다른 두 CharacterData를 뽑아 pool에서 제거하고, 세 번째는 `GenerateRandomEnemy()`로 생성한다. 이어 `characterScaler.Scale(...)`, `mapManager.ShowEnemySelection(candidates)`를 호출한다. MapManager는 생성자가 아니라 전달받은 후보 표시자다.
- 같은 파일 `GenerateRandomEnemy()`의 실제 draw는 `CardDefinition[] rewardCards = rewardManager.GetRewardCards();`, `int cardCount = UnityEngine.Random.Range(8, 13);`, `CardDefinition source = rewardCards[UnityEngine.Random.Range(0, rewardCards.Length)];`, `deck.Add(source.Clone());`이다. `Assets/scripts/Battles/RewardManager.cs`에는 `[SerializeField] private CardDefinition[] rewardCards;`와 `public CardDefinition[] GetRewardCards() => rewardCards;`가 있다.
- `Assets/scripts/CharacterScaler.cs`의 `Scale()`은 CharacterData를 clone하고, `ApplyRandomUpgrades()`는 `RewardManager.RollEnhanceOption()`, `RewardManager.CanEnhance(def, option)`으로 eligible 카드들을 만든 뒤 균등 선택하여 `ApplyUpgrade()`한다. 기존 후보의 weighted upgrades는 별도 경로다.
- Multi가 재사용하는 것은 Single의 **rewardCards authoring pool, 균등 샘플링, 중복 허용, draw마다 deep Clone**이다. Single의 8–12장 적 덱 크기, 적 HP 80~상한, weak/boss AI, 캐릭터 후보 소모, enemy round scaling/자동 강화는 준비의 3개 슬롯에 적용하지 않는다. RewardManager의 별도 카드 보상 3장 중복 없는 draw와 혼동하지 않는다.
- Multi의 기존 `CardOptionGenerator.GenerateOptions` 3슬롯 정책은 유지한다: 강화 대상이 있으면 첫 슬롯 Enhance, 없으면 AddCard; 둘째 AddCard; 셋째 50% Enhance/AddCard(대상이 없으면 AddCard). round 정책이나 실제 강화 규칙을 발명하지 않는다. 각 ClientId마다 서버가 독립 호출하되 동일 후보가 우연히 나오는 것은 정상이다.

### 실제 pool 연결 및 invalid policy (HIGH 2)

`Assets/Scenes/MultiPlayMode.unity`의 기존 generator component `224265908`에는 cardPool null 3개가 있다. 기존 component/fileID를 유지하고 이 list만 SinglePlayMode.unity:2388의 rewardCards와 동일 순서로 아래 29개 `{fileID: 11400000, guid: ..., type: 2}`로 교체한다. 확인된 asset들은 `Assets/cards` 및 `Assets/card_claude` 아래이며 첫 GUID는 `Assets/cards/attack-big.asset`(cost 8, cooldown 7, Attack 40, target 1)이다.

```text
6e4e4276562262248aecbc66706b5a51
9cd2a7c063871e14495a645c9488be38
0f5fba19a89ce0d4ca8f9e31e19b809a
b8e489ebc6e8b4344ab6f7328817a5b0
d8282a6efacc94948870bdc623775219
e3905ac462ed1064bb284a2c7278014d
b6f12a9d57055c9459cedcfdb93442fc
fd0cd6fd89259c64eb1a1d8b72537eb8
61c2840774674e3f9c1c449ae8866cf2
e1875c35347d4f329d55fd6fe8f6a982
487e1117532a455bbf97bb73edfd001c
7b5dfdeb1ab14e0f83732438e0804218
d4826697bfa348ffbf83db02c670e1db
7941aedc84ff4262be4b816c332100dc
f42992e211d149fdaaeb501fa66b2124
139a1bc889eb4b3bbfb5f23d53e2df1b
40616cad6c51413d8ed07afd648fb018
6794d21a31874765a7334e396b07bc3f
f38c189ec00642e0b41978b1b8a2f08d
1d0f4083eb82443ca4739191f210b0dc
64d603a27f1e4efeb8ea6cd43ebd5dc8
25c9899db6794fab9460fa5fae71efb5
7394bf1a30f94a87ae3b5ee32e4c0218
8ac2a13904754e5aa42070b1ac54f545
ccf8f9dfcc5f4a8e90a171c84d2323d5
fcaa4318c10e47eaa173c9aa749c997b
7b514779bf0242409baeb31507eaa992
ce7596a0626246bb9b31e06b87e1ace5
237f6ed46cf7484eaf83b6465bb9d8bf
```

選定 policy: invalid entriesを除外してindexを詰めることはしない。list全体を検証し、空/list null/null definition/null effects list/null CardEffect/null underlying Effect/不正enumを一つでも検出したらconfiguration rejectionとする。実装では serialized effects listのnullもGetEffects前に安全に判定する最小のvalidation helperを用意する。使用可能なpoolがゼロの場合も必ずowner-only configuration errorを返し、候補入力はdisabled、round/deckは変更しない。これで全peerのsource indexと分布を一致させる。カード効果数ゼロはnullとは区別し、既存表示が許容する限り有効とする。上記各GUIDのasset/metafileとeffect dataを実装時に機械的に検証する。

### clone identity と Single表示 (HIGH 3)

- `PreparationOption`に生成時一度だけ設定する `CardPoolIndex` を追加する。AddCardはpool indexを決定してから `cardPool[index].Clone()` をserver candidateに保存する。Enhance/Skipは -1。以後変更しない。`ToNetData`はこの保存値をserializeし、cloneの `cardPool.IndexOf(card)` を使わない。clientはpool indexを検証して別のcloneを作る。
- 選択保存の既存 `ApplyAddCard` cloneは維持する。保存時にordered source index metadataもFinalDeckと同順に保管し、後のsnapshotでもreference equalityによるidentity復元をしない。初期deckがあれば生成元でindexを記録する。既存初期カードをpoolに対応づけられない場合、推測で別カードを表示せずblockerとして報告する。
- `CardDefinition.Clone()`はCardEffect、Effect、appliedCategoryをdeep copyしspriteは共有する。server候補cloneは置換/選択消費/manager終了時に破棄、保存deckのcloneはそのowner lifecycle終了時に破棄する。client表示cloneはcandidateと累積deckで独立所有し、visualをクリアしてから古いcloneをDestroyする。Cancelで同じsnapshotのcloneを破棄しない。scene unload/OnDestroyでも清掃する。assetをDestroyしない。
- Single表示をそのまま使う: `DeckDisplay.SetDeck -> SpawnCard -> SetPopupManager -> new CardInstance(def, null) -> SetVisual -> SetFace(true) -> SetLayer("UI")`。CardVisualがsprite/cardType背景/cost/cooldown/effect icon・value/highlight/使用不可overlayを供給する。候補も既存EnemyDisplayの集計だけで済ませず `Resources/Prefabs/Card` を候補Panel内の最小childに生成し同じ経路を使う。Enhanceはowner snapshotのtarget cardを表示し、未適用の強化をカード値に偽装しない。Skipはカードを生成せず既存Skip表示を維持する。
- popupはSingle `MapManager.RefreshDeckSelectionPopup/GetCardEffectTypes`と同じく、選択カードのGetEffectsの順序・重複を保ったEffectType listを `PopupManager.Show` に渡す。新しい効果集計semanticsは作らない。

## (B) MultiPlayMode 上部領域 scene binding (MEDIUM 7)

既存 `PanelGameObject` GameObject `885946566` / Transform `885946567`、parent `1474451738`、MapManager_Multi component `1474451739`を維持する。位置 `(0.04, 2.41, 0)`、scale `(1, 0.4, 1)`、SpriteRendererも変更しない。Transformのchildren listへの追記とcomponent serialized bindingsのみ変更する。上部は常時累積カードを表示し、Upで詳細入力modeに入る。Cancelで上部のカード表示そのものを消さない。

serialized field名は `panelGameObject`, 既存 `myDeckDisplay`, 新規 `myPopupManager` に統一する。child名は `MySelectedCardsDisplay` / `MySelectedCardsPopup` としserialized bindingを説明する容器であって別manager階層ではない。fallbackは `transform.Find("PanelGameObject")` およびその名前のchildren。remainingTextは既存独立fieldとして扱う。

- Display source: `Assets/Resources/Prefabs/DeckDisplay.prefab`, GUID `d8164d610507d7e42ae34de252ad9d9b`; root GO `1489693399071395392`, RectTransform `680747832752182091`, DeckDisplay component `3996031509450874117`, script GUID `f1f8d35b43c62fa4e9dd4c621d17c089`。最小prefab instanceとstripped RectTransform/component referencesを追加してbindingsに用いる。既存別階層displayは共有しない。
- Popup source: `Assets/Resources/Prefabs/PopupManager.prefab` の内容。root GO `4255318710238124244`, Transform `7422807454270366460`, component `2296612810257439161`, script GUID `0ccd61196d076744daec66a7b29aeece`, PageText GO `1485576535307114763` / TMP `5737690650376727833`。**このprefab.metaにはmerge conflict markersと複数GUIDが残るため、そのGUIDを参照するPrefabInstanceは追加しない。** 内容をscene-local recordsにコピーしunique fileIDへ全内部参照をremapする。meta修復/Single変更はしない。
- popup childは既存 `Assets/Resources/Prefabs/PopUpComponent.prefab`, 有効GUID `c10456d99d112ab419058ab3ea581e96`, root `2836450954090337283`, Transform `3505375662516667731`を利用する。PopupManager内容のnested PrefabInstanceとstripped Transform(元 `1787513101948861402`)をscene-local ownerへremapする。pageText bindingもコピーする。candidate popupは別scene-local instanceとして同じ内容を使う。PopupManager.Awakeはinactive childも検索し、Show前にrootをactivateして初期化を完了させる。
- layout: 子displayとpopupのlossyScaleを `(1,1,1)` にするためlocalScaleを親lossyScaleの逆数に設定する(現在単位の祖先なら `(1,2.5,1)`)。displayをpanel中心に置く。毎回SetDeck前にPanelのSpriteRenderer.bounds.sizeからworld幅/高さを求め、左右上下5%のmarginを引いた `SetSize(bounds.size * 0.9f)` を呼ぶ。100×100のRectTransform defaultはレイアウトに使わない。
- 既存 `myDeckColumns=6` を正値clamp、`myDeckCardSize=0.7f`を正値として使い、Card prefabの背景world sizeから6列と最低1行が内側に収まる最大scaleとのminをeffective cardSizeにする。狭い高さでも1行が収まることを検証し、追加行は既存DeckDisplay pagingで表示する。cardSize/columnsのゼロ・負値を許容しない。カードはUI layer/sorting、panel背景(Default order 0)より上、popupは既存UI order 100を保つ。popupのworld位置はpanel右側にそのbounds幅の半分＋popup背景幅半分＋0.1の間隔で配置し、親scaleにより潰れないようにする。候補カードは各既存Panelのbounds内に同じサイズ計算で1枚を配置する。
- unused scene fileIDsを事前に抽出して採番し、追加prefab instance/stripped recordのm_CorrespondingSourceObject、m_PrefabInstance、parent-child reciprocity、pageText/component referenceを静的検証する。既存 `885946566/885946567/1474451739` を置換しない。

## (C) Server authority / owner snapshot contract (HIGH 4, MEDIUM 8–9)

### APIの確定

installed `Library/PackageCache/com.unity.netcode.gameobjects@0f12e689d980/Runtime/Messaging/RpcParams.cs` を読んだ。実signatureは `public struct ClientRpcSendParams { public IReadOnlyList<ulong> TargetClientIds; ... }`、`public struct ClientRpcParams { public ClientRpcSendParams Send; public ClientRpcReceiveParams Receive; }`。一方universal `RpcSendParams`は `public BaseRpcTarget Target;`であり、旧案のTargetClientIds initializerは誤りだった。

レスポンスは**legacy server-only ClientRpc**に統一する:

```csharp
[ClientRpc]
private void SendPreparationStateClientRpc(
    PreparationStateSnapshot snapshot,
    ClientRpcParams clientRpcParams = default) { /* local UI receive */ }

// server-only send wrapper: IsServer必須、sender connectionを検証
new ClientRpcParams {
    Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } }
};
```

begin/confirm/skip/rejectionはこの一つのcombined state responseを使う。候補/private card broadcast RPCを削除し、targetClientId入りbroadcast＋local filterにはしない。Clientからresponseをemitするpublic wrapper/pathを作らない。requestは既存 `[Rpc(SendTo.Server)]`を維持し `rpcParams.Receive.SenderClientId` を唯一のownerとする。clientが指定したowner idを信頼しない。hostも同じtransport boundaryを通る。

### 一つの revisioned PreparationStateSnapshot

準備専用 `INetworkSerializable` DTOを追加する(戦闘公開NetworkVariableに混ぜない)。field contract:

- `RequestId`(client要求との照合), `Revision`(ownerごとに成功commit時だけ増加), `Round`(消費済回数), `Remaining = 10 - Round`, `Completed`, `Accepted`, error code/reason, `NeedsOptionsRetry`。
- `Options`: 0または3の `PreparationOptionNetData` records。既存 Type/CardPoolIndex/TargetCardIndex/EnhanceIdを維持する。snapshot revisionが候補のrevisionでもある。
- `SelectedCards`: FinalDeckと同順のruntime card records。各recordはSourceCardPoolIndex、Cost(int)、Cooldown(int)、CardType(enum)、順序付きEffectsを含む。effect recordはEffectType、Magnitude(既存Effectのint値)、EffectTarget、AppliedCategory bitflags。spriteはsource pool indexから取得するのでsprite/network objectを送らない。recordsは現時点のserver値そのもので、EnhanceIdからupgradeを推測しない。
- arraysは明示的lengthと要素のNetworkSerializeを実装する。カード数/効果数は準備の既存許容値から上限を定め、受信lengthを検証してからallocationする。初期deck数+最大10追加をcard上限の根拠にする。既存許容effect slot数を調査してeffect上限を設定し、正当なauthoringを切り捨てない。
- reconstruct前にenum、index、definition/effect null、cost/cooldown、supported category、Enhance targetの範囲を検証する。不正snapshotは現在UIを部分更新せずerror/pending解除、再同期要求を可能にする。全cards/optionsを一時clone群へ復元して成功後だけatomic UI swapする。Effect順序/重複/categoryを保存する。
- runtime record復元は `CardDefinition`への最小の追加static factoryで明示的cost/cooldown/cardType/sprite/effectsを設定する。既存Create/Clone/Single呼出しや挙動を変えない。effectsは `new CardEffect(new Effect(type, magnitude), target)` とSetAppliedCategoryで個別生成する。candidate AddCardはsource.Clone、Enhanceの表示はSelectedCards[target]の独立cloneを使う。

### request validation / failure / atomicity

1. 全3requestでserver、ConnectedClients membership、registry owner存在、許可準備phase、current options、非completedを検証する。beginの既存WaitingForPlayers→ChoosingCard条件は実際の2 connected registered IDでのみ判定する。辞書は実NGO ClientIdで分離する。
2. beginは同revisionの既存optionsを返し、再生成しない。completed beginには同owner completed snapshotを返し、回数を消費しない。NeedsOptionsRetryのbeginは同Round/Revisionで欠けたoptionsだけ生成し、成功するまで入力無効とする。
3. confirm/skipには `expectedRevision`, `expectedRound`, `requestId` を追加する。成功時にrevision/roundを各1増加する。古いrevision、二重confirm、範囲外index、未知enum、無効pool/targetはmutation前に拒否する。Skipは独立combined responseで同じvalidation、round消費規則を使う。
4. atomicityは**commit後のnext-options生成失敗を成功＋recoverable errorとして返す**方式に確定する。先に選択とsource metadataを保存しround/revisionを1増加、古いoptionsを破棄。その後nextを生成しvalidateする。失敗時Accepted=true、NeedsOptionsRetry=true、Options空、保存済deck/remainingをsnapshotで返す。clientは確定済選択を再送せずbegin retryだけ可能。再生成は未消費roundを変更しない。10回目はCompleted=true、Options空、remaining=0。
5. mutation前rejectionはAccepted=falseで現在のowner snapshotを返す(未登録/設定errorなら空snapshot)。同revisionのrejectionもRequestId照合でpendingを解除する。古いresponseはrevision/request照合で新状態を上書きしない。count/deckはclient側で楽観更新しない。invalid poolはSkipに化けさせない。
6. `ApplyEnhance`は現在target bounds確認だけでno-opである。このtaskでmutationを発明しない。targetの非nullとowner deck範囲を強化し、成功レスポンスには実際に変わらなかったruntime値とdeferred-mutation statusを記録する。review reportでno-opを明記する。相手には候補/selected effects/remainingを送らない。

## (D) 上部 UI binding / input mutual exclusion (MEDIUM 5–6)

入力stack policyを一つに固定する。`PlayerInputManager.Load`はpush、Unloadはpopなので、MapManager_Multiは `ownsPreparationInput`, `ownsUpperInput`, `upperViewOpen`, `requestPending` を管理する。準備contextは一度だけpushし、上部contextはその上に一つだけpushする。ShowOptions/responseごとにLoadしない。

- preparation Select context: Left/Right候補移動、Select→RequestConfirmPreparationOption(index, revision, round, requestId)、Up→OpenUpperCardInfo。既存skip pathにも同じguard/revisionを適用する。cardクリックも直接ローカル確定せず同じconfirm entry pointを経由する。upper openまたはpending時にmove/confirm/skipをguardする。pending中は上部を新規に開かない。
- 上部通常表示はsnapshot受信ごとに更新するが、inactive popupを渡し、Deselectしてcandidate popupを奪わない。上部に候補を入れない。
- Open順序: candidate popup.Hide→candidate popup無効化(overlay unload)、候補visualをdisabled、panelGameObjectをactive、myPopupManager root active/Awake完了、myDeckDisplay.SetPopupManager(myPopupManager)、SetSize、SetDeck、upper Select contextをpush、SelectFirst、Single semanticsでpopup refresh。SetPopupManagerは必ずカード生成前。
- 上部Select contextは横/縦deck移動とCancelだけで準備confirmに繋がらない。上部移動ごとにpopupを更新する。候補と上部でPopupManagerを別instanceにする。両方のPopup overlayを同時にenableしない。
- responseがupper open中に届く場合、最新revisionのbacking options/deck/countを更新し上部を再描画、上部selected card indexをclampする。候補をreactivateしない、preparation contextをpushしない。candidate indexも最新optionsにclampする。Cancelで古い_optionsを復元しない。
- Cancel順序: upper inputをownしている場合一つpop、上部Deselect、myPopupManager.Hide→無効化(overlay unload)、上部カードは累積状態として表示を維持、最新有効候補のvisual/highlightを復元、candidate popup enable→選択候補popup refresh。下の既存preparation contextは再Loadしない。completed/error/pendingなら候補入力を復活させない。
- completionではsnapshotを最後に表示してwaitingへ入り、own upperがあれば先にpop、次にown preparationをpopする。popupも両方Hide/disable。scene exitも同じowned順でcleanupする。他managerのcontextを無条件にpopしない。候補を隠すrootはPanelGameObjectの祖先ではなく既存MapSelectPanelとする。

## (E) 今後10回flowの最小 hook

`MultiPreparationManager`に `[SerializeField, Min(0f)] private float transitionDelaySeconds = 1f;`を追加する。Inspector可変、実行時Mathf.Max(0f, value)。serverの2 registered connected ownersがConditionCompletedとなった場合だけone-shot `transitionStarted` guardでcoroutineを開始する。最終owner snapshot送信後に開始し、delay後completionを再検証してlogを残し既存 `MapManager_Multi.Instance?.NotifyBothPlayersConfirmed()` entry pointへ接続する。

これはdelay stubであり、実scene migration、battle FinalDeck handoff、lobby→Multi lifecycle完成を意味しない。delay中disconnect時はtransitionを実行せずlogに留め、新しいreconnect方針は設計しない。server以外からこのhookを呼ばない。既存NotifyBothPlayersConfirmedの即StartBattleはknown gapとして記録する。

## (F) 具体的file edits / validation / review stop contract

後続実装の限定編集リスト:

1. `Assets/scripts/HyeGyoServer/MultipLayMode/PreparationOption.cs`: immutable-by-contract source CardPoolIndex。
2. `Assets/scripts/HyeGyoServer/MultipLayMode/CardOptionGenerator.cs`: pool validation、per-draw clone、stored index serialization、client clone reconstruction。3slot policy維持。
3. `Assets/scripts/HyeGyoServer/MultipLayMode/MultiPreparationManager.cs` と必要最小の `PlayerPreparationData.cs`: owner revision/request boundary、ordered source metadata、snapshot export、committed-success retry rule、clone cleanup、delay stub。registry/combatをrefactorしない。
4. `Assets/scripts/HyeGyoServer/MultiServerCardSelection/PreparationOptionNetData.cs` と同directoryの新規 `PreparationStateSnapshot.cs`(+Unity meta): bounded INetworkSerializable recordsとsnapshot。既存option fieldを保持。
5. `Assets/scripts/Cards/CardDefinition.cs`: 既存挙動を変えない最小runtime reconstruction factory/serialized-effects validation helperのみ。Single code pathは変更しない。
6. `Assets/scripts/HyeGyoServer/MultiplayLobbyScene/NetworkMatchBridge Separate/NetworkMatchBridge.cs`: request revision/round/id、single server-only targeted ClientRpc、begin/confirm/skip/error owner snapshot。broadcast private paths撤去。
7. `Assets/scripts/HyeGyoServer/MultiServerCardSelection/MapManager_Multi.cs`: PanelGameObject bindings、atomic clone swap、cumulated owner view、stack ownership/popup順序/pending response handling。
8. `Assets/scripts/HyeGyoServer/MultiServerCardSelection/PreparationOptionVisual_Multi.cs`: existing candidate Panel内にSingle CardVisual surface、Enhance target/Skip表示、confirm click guard。無関係の集計規則は変えない。
9. `Assets/Scenes/MultiPlayMode.unity`: component224265908の29個pool binding、上記最小children/serialized references、必要な候補popup child。既存Panel transform/fileIDs保持。
10. `.agents/tasks/multiplayer-card-generation-display/plan.md` およびレビュー成果物のみ。PopupManager.prefab.metaは既知conflictとして保持、修復やRecovery再生成はしない。

実装検証: GUID→asset存在/definition/effects/pool順序一致、scene fileID uniqueness/reference解決/parent-child reciprocity、popup initialization/order、候補描画surface chain、broadcast private payload不在を静的確認する。利用可能なC# build/Unity batch runnerを調べ、使用できるプロジェクトチェックを実行する。revision duplicate/rejection、既commit後生成失敗retry、2 ClientId間隔離、snapshot round-trip effect順序/category、clone identity/cleanupについて意味のあるboundary testsを優先する。Unity Editor/Host+Clientが使えない場合は実行できたチェックと未実行runtimeを区別する。plan-only今回の段階ではbuild/testしない。

review reportにはHIGH1–4/MEDIUM5–9各項目の実装・証拠、actual Select/click path、pool null対策、clone index、server-only targeting、Add/Enhance/Skip count、Enhance no-op、async stack handling、layout/GUID/fileID、known prefab.meta conflict、runtime限界を記録する。

**stop contract**: review gateが監視するpathは `c:\Git-Directory\UpnL_FirstGame\remake_project_2026\.agents\tasks\multiplayer-card-generation-display/review.json`。実装/review loopはその `verdict` fieldが正確に `"APPROVED"` の時だけ終了する。計画修正者がreview.jsonを自分でAPPROVEDへ変更してはならない。

## (G) Not in scope / known blockers

- 現在confirm/skip result deliveryはcommentで止まっている。今回準備表示に必要なwiringと正確なcount boundaryを改善するが、lobby全体/再接続/全10回UX/scene policyが完成したとは主張しない。
- ApplyEnhanceの実mutation、RollEnhanceOption/CanEnhanceをMulti実upgradeへ接続する作業はdeferred。snapshotは実データだけ表示し、未適用の強化を成功した表示にしない。既存no-op/count behaviorを隠さずreportする。
- `FinalDeck -> CharacterManager_Multi`のhandoff、server combat shuffle/hand/actions/results、battle hand/deck privacy、handsRoot共有は変更しない。準備owner snapshotとbattle stateは別境界。
- PopupManager.prefab.metaの実merge markersは既知import問題。scene-local popupのコピーで新たな壊れたGUID依存を避けるが既存import成功を保証しない。buildを妨げるなら既存blockerとして正確に報告し、勝手にmeta/Singleを修復しない。
- remainingText nullを上部deck bindingと混同しない。初期カードsource index不明やasset参照欠落を勝手な代替カードで隠さない。
- reload/token/料金、弱い初期攻撃防御追加、敵HP/AI scaling、強化効果規則変更は範囲外。
- SinglePlayMode scene/behaviorを変更しない。Singleの表示/生成semanticsを再利用し、共有クラスへの追加factoryも既存呼び出しを変えない。
- `.git` mutation、commit/push/PR、Assets/_Recovery削除/再生成は禁止。task内の一般的commit指示よりこの具体的制約を優先する。
- この調査ではUnity runtimeを実行していない。後続runtime受入では実Host+Client、実2 NGO IDs、owner-only各3候補、確定後上部累積表示、Enhance targetのowner検証、upper中confirm拒否、Cancel最新状態復元、stale/repeated消費無し、10回完了/設定delayを確認する。乱数の結果がたまたま一致してもowner独立性failureとはしない。

## (H) workflow restructuring permission / 判断

remaining stepsの分割/FEAT artifact作成は許可されるが、本作業は一つのrevisioned owner snapshotを軸にpool identity、candidate surface、累積表示、入力stack、scene bindingが連動する。独立機能として別々に完成・検証できる分解ではないため既存一featureのimplementation/review loopを維持する。FEAT artifactやstep置換は行わない。後続で分割が必要と判断してworkflowを変更する場合も、Fの監視pathと `review.json.verdict == "APPROVED"` のみで終了するstop contractをそのまま保存する。
