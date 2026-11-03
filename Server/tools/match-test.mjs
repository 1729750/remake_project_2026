// 봇 두 개가 방을 만들고 카드 선택 → 전투 → 결과까지 한 판을 끝까지 진행한다.
// 사용법: node match-test.mjs [ws://host:port/ws]   (Node 22+)
const url = process.argv[2] ?? "ws://localhost:7777/ws";
const timeoutMs = 180_000;

function bot(name, onState) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(url);
    const handlers = {};
    const send = (m) => ws.send(JSON.stringify(m));
    ws.onerror = () => reject(new Error(`${name}: 연결 실패`));
    ws.onmessage = (e) => {
      const msg = JSON.parse(e.data);
      if (msg.type === "error") console.log(`[${name}] error:`, msg.message);
      if (msg.type === "state") onState(msg.state, send);
      handlers[msg.type]?.(msg);
    };
    ws.onopen = () => resolve({ send, on: (t, f) => (handlers[t] = f), close: () => ws.close() });
  });
}

let lastTurnPlayed = {};
let finished = 0;
const results = {};

function makeBrain(name) {
  return (s, send) => {
    if (s.phase === "picking" && !s.picked) {
      const pick = Math.floor(Math.random() * s.offers.length);
      console.log(`[${name}] 라운드 ${s.round}/${s.totalRounds}: ${s.offers.map((o) => o.name).join(", ")} → ${s.offers[pick].name}`);
      send({ type: "pick", index: pick });
    }
    if (s.phase === "battle" && lastTurnPlayed[name] !== s.turn) {
      lastTurnPlayed[name] = s.turn;
      const me = s.me;
      const playable = me.hand.map((c, i) => [c, i]).filter(([c]) => c.name && c.playable);
      if (playable.length) {
        const [card, index] = playable[Math.floor(Math.random() * playable.length)];
        send({ type: "play", index });
      } else if (Math.random() < 0.15) {
        send({ type: "play", index: -1 }); // 가끔 가드
      }
      if (s.turn % 10 === 0)
        console.log(`[${name}] 턴 ${s.turn}: HP ${me.hp}/${me.maxHp} 방어 ${me.defense} 코스트 ${me.cost} | 상대 HP ${s.opponent.hp} | 큐 [${me.queue.map((q) => `${q.name}(${q.cooldownLeft})`).join(" ")}] 효과 [${me.effects.map((e) => `${e.type}${e.magnitude}`).join(" ")}]`);
    }
    if (s.phase === "finished" && !results[name]) {
      results[name] = s.result;
      console.log(`[${name}] 결과: ${s.result} (${s.reason}) — 턴 ${s.turn}, 내 HP ${s.me?.hp}, 상대 HP ${s.opponent?.hp}`);
      if (++finished === 2) done();
    }
  };
}

const a = await bot("A", makeBrain("A"));
const b = await bot("B", makeBrain("B"));
a.send({ type: "hello", name: "봇A" });
b.send({ type: "hello", name: "봇B" });
a.on("room_joined", (m) => b.send({ type: "join_room", code: m.code }));
a.send({ type: "create_room" });

const timer = setTimeout(() => {
  console.log("FAIL: 시간 초과");
  process.exit(1);
}, timeoutMs);

function done() {
  clearTimeout(timer);
  const ok = (results.A === "draw" && results.B === "draw") ||
    (results.A === "win" && results.B === "lose") || (results.A === "lose" && results.B === "win");
  console.log(ok ? "\nOK: 카드 선택 → 전투 → 승패 판정까지 정상" : "\nFAIL: 결과가 서로 맞지 않음");
  a.close();
  b.close();
  process.exit(ok ? 0 : 1);
}
