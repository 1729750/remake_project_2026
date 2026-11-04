// 서버 동작 확인용: 두 클라이언트가 방을 만들고 참가해서 room_ready까지 가는지 본다.
// 사용법: node smoke-test.mjs [ws://host:port/ws]   (Node 22+ 필요)
const url = process.argv[2] ?? "ws://localhost:7777/ws";

function connect(name) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(url);
    const inbox = [];
    const waiters = [];
    ws.onmessage = (e) => {
      const msg = JSON.parse(e.data);
      console.log(`[${name}] <-`, e.data);
      const i = waiters.findIndex((w) => w.type === msg.type);
      if (i >= 0) waiters.splice(i, 1)[0].resolve(msg);
      else inbox.push(msg);
    };
    ws.onerror = () => reject(new Error(`${name}: 연결 실패 (${url})`));
    ws.onopen = () => {
      const client = {
        send: (msg) => ws.send(JSON.stringify(msg)),
        expect: (type) =>
          new Promise((res, rej) => {
            const i = inbox.findIndex((m) => m.type === type);
            if (i >= 0) return res(inbox.splice(i, 1)[0]);
            waiters.push({ type, resolve: res });
            setTimeout(() => rej(new Error(`${name}: ${type} 대기 시간 초과`)), 5000);
          }),
        close: () => ws.close(),
      };
      resolve(client);
    };
  });
}

const a = await connect("A");
const b = await connect("B");
await a.expect("welcome");
await b.expect("welcome");

a.send({ type: "hello", name: "Alice" });
b.send({ type: "hello", name: "Bob" });

a.send({ type: "create_room" });
const { code } = await a.expect("room_joined");

b.send({ type: "join_room", code: code.toLowerCase() });
await b.expect("room_joined");
await a.expect("player_joined");
await a.expect("room_ready");
await b.expect("room_ready");

b.send({ type: "join_room", code });
await b.expect("error");

a.send({ type: "ping" });
await a.expect("pong");

b.send({ type: "leave_room" });
await b.expect("room_left");
await a.expect("player_left");

a.close();
b.close();
console.log("\nOK: 방 생성 → 참가 → room_ready → 나가기 모두 정상");
