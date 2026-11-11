// Unity 카드 에셋 → cards.json → 오라클 서버의 밸런스 시뮬레이터에 바로 반영한다.
// 서버는 카드 데이터가 바뀌면 자동으로 한 번 돌려서 이전 버전과 비교한다 (http://161.33.142.112:7777/sim/).
//
//   node Server/tools/sim-sync.mjs            한 번 올리기
//   node Server/tools/sim-sync.mjs --watch    Unity에서 카드 에셋을 저장할 때마다 올리기 (켜 두고 작업)
//
// 비밀번호: 환경 변수 SIM_PASSWORD, 없으면 Server/tools/.sim-password (deploy-sim.sh가 만듦)
// 서버 주소: 환경 변수 SIM_URL (기본 http://161.33.142.112:7777/sim/)
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const toolsDir = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(toolsDir, "../..");
const assets = path.join(root, "Assets");
const cardsJson = path.join(root, "Server/CardGameServer/Data/cards.json");
const simUrl = (process.env.SIM_URL ?? "http://161.33.142.112:7777/sim/").replace(/\/?$/, "/");

const args = process.argv.slice(2);
const watch = args.includes("--watch");
const sourceIndex = args.indexOf("--source");
const source = sourceIndex >= 0 ? args[sourceIndex + 1] : "PC 업로드";

function password() {
  if (process.env.SIM_PASSWORD) return process.env.SIM_PASSWORD;
  const file = path.join(toolsDir, ".sim-password");
  if (fs.existsSync(file)) return fs.readFileSync(file, "utf8").trim();
  throw new Error("비밀번호가 없습니다. SIM_PASSWORD를 설정하거나 Server/deploy/deploy-sim.sh를 한 번 실행하세요.");
}

const now = () => new Date().toTimeString().slice(0, 8);

async function sync(label) {
  // export-cards.mjs: Unity 에셋에서 cards.json을 다시 만든다 (게임 서버용 파일도 같이 갱신됨)
  execFileSync(process.execPath, [path.join(toolsDir, "export-cards.mjs")], { stdio: "pipe" });
  const body = fs.readFileSync(cardsJson);
  const res = await fetch(simUrl + "api/cards", {
    method: "POST",
    headers: {
      Authorization: "Basic " + Buffer.from("sync:" + password()).toString("base64"),
      "Content-Type": "application/json",
      "X-Source": encodeURIComponent(label), // 헤더에는 한글을 못 넣는다
    },
    body,
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`서버 응답 ${res.status}: ${text}`);
  const result = JSON.parse(text);
  console.log(`[${now()}] ${label} → ${result.result} (카드 데이터 ${result.version})${result.result === "반영됨" ? " · 서버에서 자동 실행 시작" : ""}`);
}

if (!watch) {
  sync(source).catch((e) => {
    console.error(`[${now()}] 실패: ${e.message}`);
    process.exit(1);
  });
} else {
  // Unity가 에셋을 저장하면 .asset 파일이 바뀐다. 몰아서 저장되므로 2초 모아서 한 번만 올린다
  const targets = [
    { dir: path.join(assets, "cards"), match: (f) => f.endsWith(".asset") },
    { dir: path.join(assets, "card_claude"), match: (f) => f.endsWith(".asset") },
    { dir: path.join(assets, "Resources/Data"), match: (f) => f === "EffectPrices.json" },
    { dir: path.join(assets, "Scenes"), match: (f) => f === "SinglePlayMode.unity" },
  ];
  let timer = null;
  const changed = new Set();

  for (const { dir, match } of targets) {
    if (!fs.existsSync(dir)) continue;
    fs.watch(dir, { recursive: true }, (_event, file) => {
      if (!file || !match(path.basename(file))) return;
      changed.add(path.basename(file));
      clearTimeout(timer);
      timer = setTimeout(async () => {
        const label = `Unity 저장 (${[...changed].slice(0, 3).join(", ")}${changed.size > 3 ? " 외" : ""})`;
        changed.clear();
        try {
          await sync(label);
        } catch (e) {
          console.error(`[${now()}] 실패: ${e.message}`);
        }
      }, 2000);
    });
  }

  console.log(`[${now()}] Unity 카드 에셋 감시 중 → ${simUrl}  (Ctrl+C로 종료)`);
  sync("감시 시작").catch((e) => console.error(`[${now()}] 실패: ${e.message}`));
}
