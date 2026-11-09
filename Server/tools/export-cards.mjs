// Unity CardDefinition 에셋(.asset)을 읽어 서버용 cards.json을 만든다.
// 카드를 추가/수정했으면 다시 실행: node Server/tools/export-cards.mjs
// 시작 덱은 SinglePlayMode 씬의 PlayerManager.deck을 그대로 쓴다.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const assets = path.join(root, "Assets");
const out = path.join(root, "Server/CardGameServer/Data/cards.json");

// enum 순서는 Assets/scripts/Cards/effects/EffectType.cs, CardType.cs와 같아야 한다
const EffectTypes = ["Attack", "Defend", "Weak", "Vulnerable", "Strength", "Harden", "Guard", "PoseBreak",
  "EnergyHeal", "AddDump", "Burning", "CostToCooldown", "CooldownToCost", "DivideCooldown", "TimeSkip",
  "Quicker", "Preserve", "Disposable", "DefenseToCooldown", "EnergyDrain"];
const CardTypes = ["Instant", "Continuous", "Mix"];
const Targets = ["User", "Opponent"];

const metaGuid = (file) => /^guid: (\w+)/m.exec(fs.readFileSync(file + ".meta", "utf8"))[1];
const cardScriptGuid = metaGuid(path.join(assets, "scripts/Cards/CardDefinition.cs"));

function walk(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) return e.name === "옛날꺼" || e.name === "_Recovery" ? [] : walk(p);
    return e.name.endsWith(".asset") ? [p] : [];
  });
}

const num = (text, key) => Number(new RegExp(`^  ${key}: (-?\\d+)`, "m").exec(text)?.[1] ?? 0);

const cards = [];
const byGuid = {};
for (const file of walk(assets)) {
  const text = fs.readFileSync(file, "utf8");
  if (!text.includes(`guid: ${cardScriptGuid}`)) continue;

  const name = /^  m_Name: (.+)$/m.exec(text)[1].trim();
  const effects = [];
  const effectRe = /- _effect:\s+_effectType: (\d+)\s+_magnitude: (-?\d+)\s+_target: (\d+)(?:\s+appliedCategory: (\d+))?/g;
  for (const m of text.matchAll(effectRe)) {
    effects.push({
      type: EffectTypes[Number(m[1])],
      magnitude: Number(m[2]),
      target: Targets[Number(m[3])],
      appliedCategory: Number(m[4] ?? 0),
    });
  }

  cards.push({ name, cost: num(text, "cost"), cooldown: num(text, "cooldown"), cardType: CardTypes[num(text, "cardType")], effects });
  byGuid[metaGuid(file)] = name;
}

cards.sort((a, b) => a.name.localeCompare(b.name));

// 시작 덱: SinglePlayMode 씬의 PlayerManager
const playerManagerGuid = metaGuid(path.join(assets, "scripts/PlayerManager.cs"));
const scene = fs.readFileSync(path.join(assets, "Scenes/SinglePlayMode.unity"), "utf8");
const block = scene.split(/^--- /m).find((b) => b.includes(`guid: ${playerManagerGuid}`));
const deckSection = /\n  deck:\r?\n((?:  - .+\r?\n)+)/.exec(block)[1];
const startDeck = [...deckSection.matchAll(/guid: (\w+)/g)].map((m) => byGuid[m[1]] ?? `?${m[1]}`);
const maxHealth = num(block, "maxHealth") || 100;

// 강화 가격표: 싱글 GameManager가 쓰는 Resources/Data/EffectPrices.json 그대로
const effectPrices = JSON.parse(fs.readFileSync(path.join(assets, "Resources/Data/EffectPrices.json"), "utf8")
  .replace(/^﻿/, "")).prices;

fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify({ maxHealth, startDeck, effectPrices, cards }, null, 2) + "\n");
console.log(`${cards.length} cards, start deck [${startDeck.join(", ")}] -> ${path.relative(root, out)}`);
