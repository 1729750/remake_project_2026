# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

A Unity prototype for a card-battler (Unity Editor **6000.3.11f1**, URP). A player fights an AI-controlled enemy using the same combat rules on both sides. See `README.md` (Korean) for the design pitch, card iconography, and known design issues the author is actively reconsidering (cost pacing, enemy turn speed).

There is no CLI build, lint, or test pipeline in this repo — it's driven entirely through the Unity Editor:
- Open the project in Unity Editor 6000.3.11f1, open `Assets/Scenes/SampleScene.unity`, press Play to run the prototype.
- `com.unity.test-framework` is a listed package dependency but no test assembly/`*Tests.asmdef` exists yet — there are no automated tests to run.
- There are no `.asmdef` files at all; every script compiles into the default `Assembly-CSharp`.
- A custom editor tool exists at `Assets/Editor/CardPrefabSetup.cs` (`Tools > Add Card Text Boxes` menu item) that regenerates the card prefab's text child objects. **Its `PrefabPath` constant still points at the old `Assets/resources/preFabs/Card.prefab` location** — the asset now lives at `Assets/preFabs/Card.prefab` (see restructuring note below), so this tool needs its path fixed before it will run.

## Unity MCP integration

`.mcp.json` wires up the `mcp-unity` MCP server (from the `com.gamelovers.mcp-unity` package under `Library/PackageCache`), which lets Claude Code interact with the live Unity Editor (scene state, assets, console, etc.) directly rather than only editing files on disk.

## In-progress asset restructuring

Working tree currently shows a move from a `Assets/resources/{scripts,cards,preFabs,sprites}` layout (Resources-loaded) to top-level `Assets/{scripts,cards,preFabs,sprites}`. Only `CharacterManager.Init()`'s `PlayerAction` input asset is still loaded via `Resources.Load`; everything else is wired through scene/prefab references. Be aware some file paths (like the `CardPrefabSetup` tool above) may still reference the old `resources/` layout and need updating as part of this migration.

## Architecture

### Game/turn loop
- **`GameManager`** (`MonoBehaviour` singleton, `GameManager.Instance`) is now a thin scene entry point: it holds the serialized inspector references (both `CharacterManager`s, `turnText`, `turnDuration`, `startDelay`), builds the `TurnTimerOverlay` GameObject and `TurnManager` in `Start()`, constructs a `BattleManager` from them, and forwards `Update()` ticks to it. It no longer exposes any battle-domain methods itself.
- **`BattleManager`** (plain C# class, not a `MonoBehaviour`, own static `Instance` set from its constructor) owns the actual battle: the state machine (`GameState`: `GameStarting → TurnStart → Turn → TurnEnd → GameFinish`), the pre-battle `startDelay` countdown, ticking the `TurnManager`, and is the hub other classes reach through (`BattleManager.Instance.GetOpponent(...)`, `.GetEmoji(...)`, `.NotifyDefeat(...)`, `.CurrentState`). `GameManager.Update()` just calls `BattleManager.Instance.Tick(Time.deltaTime)`.
- **`TurnManager`** is a plain C# class constructed and ticked by `BattleManager`. It fires `OnTurnStarted`/`OnTurnEnded` events that `BattleManager` subscribes to, and drives the `TurnTimerOverlay` (a procedurally-built pie-slice mesh showing remaining turn/start time) plus the turn-number `TextMeshPro` label.
- Turn resolution order per turn: `OnTurnStart` (both character managers gain cost, refill hand) → player has the whole turn duration to select a card via Input System actions (`PlayCard1..4`, only while `GameState.Turn`) or, for the AI side, `SelectRandomCard()` picks affordable random card → `OnTurnEnd` ticks the queue (cooldowns/resolution), applies end-of-turn effect hooks, and moves the selected card from hand to queue if its cost is affordable.

### Combatant state — `CharacterManager`
Both the player and the enemy are the *same* `CharacterManager` script; a `playerControlled` bool toggles between reading the Unity Input System (`PlayerAction` action map, actions `PlayCard1..4`) and `SelectRandomCard()` AI logic. It holds all per-side runtime state: HP, defense, cost (capped at `MaxCost`), the active `Effect` list, the deck (`List<CardInstance>`), and references to its own `HandManager` (hand) and `QueueManager` (queue). It's also the sole place HP/defense math and effect application happens — see `ApplyEffect`/`TakeDamage`/`AddDefense`.

### Card pipeline
`CardDefinition` (ScriptableObject asset, authored per-card data: cooldown, cost, sprite, `CardType`, list of `CardEffect`) → on deck build, wrapped into a runtime **`CardInstance`** (owns cooldown-left/played state, tied to an owning `CharacterManager`) → **`HandManager`** holds a fixed 4-card hand array, draws from the owner's deck to refill, and spawns/updates `CardVisual` prefab instances for display and selection highlighting → selecting+using a card hands it to **`QueueManager`**, a fixed 3-slot queue. Each turn end, `QueueManager.TickQueueCards` decrements cooldowns and calls `CardInstance.Play()` on any card whose cooldown has reached zero, then returns it to the owner's deck (`ReturnToDeck`).

### Effect system (the tricky part — read both files before changing)
- `Effect` is the serialized base class embedded in each `CardDefinition`'s `CardEffect` list; there it only exists to describe a `(EffectType, magnitude)` pair for authoring in the inspector.
- At *apply time*, `Effect.Create(type, magnitude)` builds the real runtime subclass (`HardenEffect`, `StrengthEffect`, `VulnerableEffect`, `WeakEffect` — anything else falls back to plain `Effect`), and it's this runtime instance that lives in `CharacterManager._effects` and receives lifecycle hooks: `OnApplying` (mutate an outgoing `CardEffect` before it resolves, based on the *acting* combatant's own buffs, e.g. Strength/Harden add flat bonuses), `OnApplyed` (mutate it based on the *target's* own effects, e.g. Vulnerable/Weak multiply incoming/outgoing damage), `OnApplied`/`OnExpired` (effect gained/lost), `OnTurnStarted`/`OnTurnEnded` (e.g. Vulnerable/Weak decay their own magnitude and self-remove via `CharacterManager.RemoveEffect<T>()`).
- `CardEffect` wraps a `CardDefinition`'s static `Effect` plus an `EffectTarget` (`User`/`Opponent`) and *mutable* `adder`/`multiplier` fields that get built up across both hook passes before `GetMagnitude()` computes the final resolved number.
- Resolution order in `CardInstance.Play()` → `CharacterManager.ApplyEffect()`: reset the `CardEffect`'s modifiers → run the acting `CharacterManager`'s own effects' `OnApplying` → resolve the target via `CardEffect.GetTarget` → on the target, run its own effects' `OnApplyed` → finally branch on `EffectType`: `Attack`/`Defend` do immediate HP/defense math, everything else stacks (`AddMagnitude`) onto an existing same-type `Effect` or creates a new one via `Effect.Create`.
- Card iconography (emoji per `EffectType`) is centralized in `BattleManager.GetEmoji` and consumed by both `CardVisual` (hand/queue display) and `CharacterManager.UpdateEffectList` (active-effects readout) — add new `EffectType`s there too.
