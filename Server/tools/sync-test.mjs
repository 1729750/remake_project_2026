// 두 클라이언트가 같은 턴 신호를 얼마나 비슷한 시각에 받는지, 이벤트(play/damage 등)가 오는지 확인한다.
// 사용법: node sync-test.mjs [ws://host:port/ws]   (Node 22+)
const url = process.argv[2] ?? "ws://localhost:7777/ws";

function client(name) {
  return new Promise((resolve) => {
    const ws = new WebSocket(url);
    const c = { name, ws, send: (m) => ws.send(JSON.stringify(m)), turns: new Map(), kinds: {}, rtt: [] };
    let pingAt = 0;
    ws.onopen = () => resolve(c);
    ws.onmessage = (e) => {
      const m = JSON.parse(e.data);
      const now = performance.now();
      if (m.type === "pong") c.rtt.push(now - pingAt);
      if (m.type !== "state") return;
      const s = m.state;
      if (s.phase === "picking" && !s.picked) c.send({ type: "pick", index: 0 });
      if (s.phase === "battle") {
        if (!c.turns.has(s.turn)) {
          c.turns.set(s.turn, now);
          const card = s.me.hand.findIndex((h) => h.name && h.playable);
          if (card >= 0) c.send({ type: "play", index: card });
        }
        for (const ev of s.events ?? []) c.kinds[ev.kind] = (c.kinds[ev.kind] ?? 0) + 1;
      }
      if (s.phase === "finished") c.done = true;
    };
    c.ping = () => { pingAt = performance.now(); c.send({ type: "ping" }); };
  });
}

const a = await client("A");
const b = await client("B");
for (let i = 0; i < 5; i++) { a.ping(); b.ping(); await new Promise((r) => setTimeout(r, 300)); }
a.ws.addEventListener("message", (e) => { const m = JSON.parse(e.data); if (m.type === "room_joined") b.send({ type: "join_room", code: m.code }); });
a.send({ type: "create_room" });

const deadline = Date.now() + 240_000;
while (!(a.done && b.done) && Date.now() < deadline) {
  await new Promise((r) => setTimeout(r, 500));
  if (a.turns.size > 40) break;
}

const diffs = [...a.turns.keys()].filter((t) => b.turns.has(t)).map((t) => Math.abs(a.turns.get(t) - b.turns.get(t)));
const avg = (xs) => (xs.reduce((s, x) => s + x, 0) / Math.max(xs.length, 1)).toFixed(1);
console.log(`RTT  A ${avg(a.rtt)}ms  B ${avg(b.rtt)}ms`);
console.log(`턴 ${diffs.length}개: 두 클라이언트 수신 시각 차이 평균 ${avg(diffs)}ms, 최대 ${Math.max(...diffs).toFixed(1)}ms`);
const ticks = [...a.turns.values()];
const gaps = ticks.slice(1).map((t, i) => t - ticks[i]);
console.log(`턴 간격 평균 ${avg(gaps)}ms (서버 1500ms)`);
console.log("A가 받은 이벤트:", a.kinds);
const ok = diffs.length > 5 && (a.kinds.play ?? 0) > 0 && (a.kinds.turnEnd ?? 0) > 0 && (a.kinds.use ?? 0) > 0;
console.log(ok ? "OK" : "FAIL");
process.exit(ok ? 0 : 1);
