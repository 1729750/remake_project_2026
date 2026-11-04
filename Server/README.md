# CardGameServer

오라클 인스턴스(`161.33.142.112`)에서 도는 멀티플레이 서버. NGO/Relay 대신 이 서버가 방과 게임 진행을 전부 판정한다.

- .NET 10, ASP.NET Core WebSocket
- `ws://161.33.142.112:7777/ws` — 게임 연결 (JSON 텍스트 메시지, 형식은 `CardGameServer/Protocol.cs` 주석 참고)
- `http://161.33.142.112:7777/health` — 상태 확인

## 게임 흐름

방에 2명이 모이면 서버가 `Game/Match.cs`로 한 판을 진행한다.

1. 카드 선택 3라운드 — 라운드마다 각자 무작위 3장 중 1장을 덱에 추가 (15초, 시간 초과 시 무작위)
2. 카운트다운 3초
3. 전투 — 1.5초 턴, 규칙은 Unity 싱글 전투(`Assets/scripts/Battles`, `Cards`)를 `Game/`으로 옮긴 것
4. 결과 — 턴 종료 후 HP 0 이하인 쪽이 패배, 둘 다면 무승부. 둘 다 "다시 하기"를 누르면 새 판

Unity 클라이언트는 `Assets/Scenes/MultiPlayMode_Oracle.unity` (`Assets/scripts/OracleServer/`).

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
```

## 배포

Git Bash에서 `Server` 폴더 기준:

```bash
./deploy/deploy.sh
```

로컬에서 `linux-arm64`로 publish → 서버 `/opt/cardgame`에 업로드 → `cardgame` systemd 서비스 재시작.
처음 실행 시 ASP.NET Core 런타임 설치, `cardgame` 시스템 계정 생성, iptables 7777 개방까지 한다.

서버 로그: `ssh ... 'sudo journalctl -u cardgame -f'`
