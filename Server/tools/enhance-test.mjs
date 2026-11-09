// 카드 선택 10라운드 동안 강화 선택지만 골라서, 덱의 같은 자리 카드가 이름은 그대로 두고 수치만 바뀌는지 본다.
// 사용법: node enhance-test.mjs [ws://host:port/ws]   (Node 22+)
const url = process.argv[2] ?? "ws://localhost:7777/ws";

const fmt = (c) => `${c.name}${c.level ? "+" + c.level : ""}[${c.cost}/${c.cooldown} ${c.cardType} ${c.effects.map((e) => `${e.type}${e.magnitude}${e.target[0]}${e.category[0]}`).join(",")}]`;

function client(name) {
  return new Promise((resolve) => {
    const ws = new WebSocket(url);
    const c = { name, send: (m) => ws.send(JSON.stringify(m)), log: [], lastDeck: null, done: false };
    ws.onopen = () => resolve(c);
    ws.onmessage = (e) => {
      const m = JSON.parse(e.data);
      if (m.type === "room_joined") c.onJoined?.(m.code);
      if (m.type !== "state") return;
      const s = m.state;
      if (s.phase === "picking") {
        c.lastDeck = s.myDeck;
        if (!s.picked) {
          const i = s.offers.findIndex((o) => o.offerKind === "enhance");
          if (i >= 0) {
            const o = s.offers[i];
            c.log.push(`R${s.round}: 강화 #${o.target} ${fmt(s.myDeck[o.target])}  →  ${fmt(o)}`);
            c.pending = { target: o.target, name: s.myDeck[o.target].name, after: fmt(o) };
          }
          c.send({ type: "pick", index: Math.max(i, 0) });
        } else if (c.pending) {
          const now = s.myDeck[c.pending.target];
          if (now.name !== c.pending.name || fmt(now) !== c.pending.after) c.log.push(`  !! 불일치: ${fmt(now)}`);
          c.pending = null;
        }
      }
      if (s.phase === "countdown" || s.phase === "battle") { c.lastDeck ??= s.myDeck; c.done = true; c.finalDeck = s.myDeck; }
    };
  });
}

const a = await client("A");
const b = await client("B");
a.onJoined = (code) => b.send({ type: "join_room", code });
a.send({ type: "create_room" });

const until = Date.now() + 60_000;
while (!(a.done && b.done) && Date.now() < until) await new Promise((r) => setTimeout(r, 200));

console.log(a.log.join("\n"));
console.log("\n최종 덱 (A):");
for (const c of a.finalDeck ?? []) console.log("  " + fmt(c));
const enhanced = (a.finalDeck ?? []).filter((c) => c.level > 0).length;
const mismatch = a.log.some((l) => l.includes("불일치"));
console.log(a.done && enhanced > 0 && !mismatch ? `\nOK: 강화된 카드 ${enhanced}장, 이름 유지` : "\nFAIL");
process.exit(a.done && enhanced > 0 && !mismatch ? 0 : 1);
