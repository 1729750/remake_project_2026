# CardGameServer

오라클 인스턴스(`161.33.142.112`)에서 도는 멀티플레이 서버. NGO/Relay 대신 이 서버가 방과 게임 진행을 전부 판정한다.

- .NET 10, ASP.NET Core WebSocket
- `ws://161.33.142.112:7777/ws` — 게임 연결 (JSON 텍스트 메시지, 형식은 `CardGameServer/Protocol.cs` 주석 참고)
- `http://161.33.142.112:7777/health` — 상태 확인

## 게임 흐름

방에 2명이 모이면 서버가 `Game/Match.cs`로 한 판을 진행한다.

1. 카드 선택 10라운드 — 라운드마다 선택지 [카드 추가 / 강화 / 무작위] 중 하나, 또는 건너뛰기 (15초, 시간 초과 시 무작위)
   - 강화: 덱의 카드 하나에 효과를 붙이고 코스트/쿨다운이 늘어난다. 카드 이름(그림)은 그대로 (`Game/Enhance.cs`, 가격표는 Unity `Resources/Data/EffectPrices.json`)
2. 카운트다운 3초
3. 전투 — 1.5초 턴, 규칙은 Unity 싱글 전투(`Assets/scripts/Battles`, `Cards`)를 `Game/`으로 옮긴 것
4. 결과 — 턴 종료 후 HP 0 이하인 쪽이 패배, 둘 다면 무승부. 둘 다 "다시 하기"를 누르면 새 판

Unity 클라이언트는 `Assets/Scenes/MultiplayLobby_Oracle.unity`(로비) → `MultiPlayMode_Oracle.unity`(게임), 스크립트는 `Assets/scripts/OracleServer/`.

카드 데이터는 `CardGameServer/Data/cards.json`. Unity에서 카드 에셋을 고쳤으면 다시 만든 뒤 배포한다:

```bash
node tools/export-cards.mjs
```

## 로컬 실행

```bash
dotnet run --project CardGameServer
node tools/smoke-test.mjs                     # 로컬 서버 테스트
node tools/smoke-test.mjs ws://161.33.142.112:7777/ws   # 오라클 서버 테스트
node tools/match-test.mjs ws://161.33.142.112:7777/ws   # 봇 두 개로 한 판 끝까지
node tools/sync-test.mjs ws://161.33.142.112:7777/ws    # 두 클라이언트 턴 수신 시각 차이, 이벤트 확인
node tools/enhance-test.mjs ws://161.33.142.112:7777/ws # 강화만 골라서 같은 카드가 수치만 바뀌는지 확인
```

## 배포

Git Bash에서 `Server` 폴더 기준:

```bash
./deploy/deploy.sh
```

로컬에서 `linux-arm64`로 publish → 서버 `/opt/cardgame`에 업로드 → `cardgame` systemd 서비스 재시작.
처음 실행 시 ASP.NET Core 런타임 설치, `cardgame` 시스템 계정 생성, iptables 7777 개방까지 한다.

서버 로그: `ssh ... 'sudo journalctl -u cardgame -f'`

## 밸런스 시뮬레이터 (BalanceSim)

AI끼리 카드 선택 10라운드 → 전투를 수천 판 돌려 카드/효과/강화 밸런스를 잰다. 규칙은 게임 서버와 같은 `CardGame.Core`라서 실제 게임과 어긋나지 않는다. 내 PC에서 3만 판에 약 1분.

```bash
dotnet run --project BalanceSim -c Release -- --games 30000
dotnet run --project BalanceSim -c Release -- --games 30000 --patch BalanceSim/patches/example-high-cost-cards.json
```

- AI: `random`(기준선), `default`(싱글 DefaultEnemyAI 이식), `greedy`(같은 점수를 코스트로 나눈 효율). 카드 선택은 덱 가치가 가장 오르는 선택지, 25%는 무작위(모든 카드를 재기 위해)
- 카드·효과 지표는 같은 AI끼리 붙은 판(무작위 제외)에서만 센다. AI 실력 차이가 카드 승률에 섞이지 않게
- 결과: `BalanceSim/reports/<시각>-<이름>/` (git에 안 올림)
  - `report.html` 브라우저로 보는 차트·표 (패치 비교는 `compare.html`)
  - `summary.md` 요약 · `cards.csv` 카드별 · `effects.csv` 효과별 · `matchups.csv` AI 상성
  - `--patch`를 주면 같은 시드로 패치 전/후를 돌려 `compare.md`, `compare.csv`
- 패치 파일(`BalanceSim/patches/*.json`)은 시뮬레이터에서만 수치를 덮어쓴다. 마음에 들면 Unity 카드 에셋을 고치고 `tools/export-cards.mjs`로 반영

| 지표 | 뜻 |
| --- | --- |
| 포함 승률 차이 | 그 카드가 덱에 있을 때 승률 − 없을 때 승률 (95% 구간과 함께). ±3%p 넘고 구간이 0을 안 걸치면 센/약한 카드 |
| 손에 든 판 승률 | 한 번이라도 손에 든 판의 승률 (시작 덱 카드는 이걸 본다) |
| 사용률 / 바로 못 내는 비율 | 손에 있던 턴 중 낸 비율 / 코스트 부족이나 큐가 가득 차서 못 낸 비율 |
| 발동당 피해 | 큐에서 발동했을 때 상대에게 준 피해 평균 (방어로 막힌 뒤) |
| 선택률 | 카드 추가 선택지로 나왔을 때 고른 비율 |
| 판 길이, 무승부, 역전승 | 판 전체 흐름 (역전승 = 이긴 쪽 HP가 30% 이하까지 갔던 판) |

### 오라클 서버에서 웹으로 (버튼 실행, 모드, AI 관전)

브라우저: **http://161.33.142.112:7777/sim/** (사용자 이름 아무거나, 비밀번호는 `Server/tools/.sim-password`)

- 게임 서버가 `/sim/`을 같은 서버의 `balancesim` 서비스(127.0.0.1:5080)로 넘긴다. 새 포트 없음. 시뮬레이터는 CPU 우선순위가 낮아 게임에 영향이 적다
- 판 수·AI·모드를 고르고 **실행** / **현재와 비교** / **AI 한 판 관전**. 결과는 실행 기록에서 보고서·비교로 연다
- **모드** = 화면에서 카드 수치·강화 가격을 고쳐 이름 붙여 저장한 패치. Unity 에셋은 그대로
- **카드 데이터가 바뀌면 자동 실행**: 서버가 같은 설정으로 한 번 돌리고 이전 카드 데이터 결과와 비교한다

배포: `bash deploy/deploy-sim.sh` (처음이면 비밀번호를 만들어 `tools/.sim-password`에 저장)

Unity/Git 수정을 서버에 바로 반영:

```bash
node tools/sim-sync.mjs --watch       # 켜 두면 Unity에서 카드 에셋을 저장할 때마다 서버로
node tools/sim-sync.mjs               # 한 번만
bash tools/install-sim-hooks.sh       # git 커밋/pull 때 자동 (카드 에셋 → sim-sync, 규칙 코드 → deploy-sim)
```
