MULTI NETCODE UPDATE - 2026-09-20

Included:
- GameNetworkState.cs (updated public network state)
- MultiPlayGamePresenter.cs (Host/Client local/enemy mapping)
- BattleManager_Multi.cs (updated)
- CharacterManager_Multi.cs
- HandManager_Multi.cs
- QueueManager_Multi.cs
- CardInstance_Multi.cs
- CardEffect_Multi.cs
- Effect_Multi.cs
- PlayerInputManager_Multi.cs
- Multi effect subclasses supplied by the user

Important:
1. Existing Single files are NOT replaced.
2. Player = local client view; Enemy = remote client view.
3. Server keeps authoritative HP/DEF/Cost/Guard and battle/card logic.
4. Private hand/deck network transport is NOT implemented yet.
   The server-side runtime structure is ready, but owner-only hand sync needs a stable card ID/serialization layer.
5. Public effects/queue snapshots are also not network-synchronized yet.
6. DefenseToCooldown Single source was not supplied, so Effect_Multi.Create falls back to base Effect_Multi for that type.
7. The supplied Single WeakEffect.OnTurnEnded removes BurningEffect. WeakEffect_Multi preserves that current source behavior explicitly.

Inspector outline:
- BattleManager_Multi:
  Game Network State -> GameNetworkRoot/GameNetworkState
  Player Character Manager -> local Player object's CharacterManager_Multi
  Enemy Character Manager -> Enemy object's CharacterManager_Multi
  Turn Text / durations / emojis -> mirror Single references
- CharacterManager_Multi on both Player and Enemy:
  Game Network State -> GameNetworkRoot/GameNetworkState
  Other view references -> mirror Single CharacterManager references
- PlayerInputManager_Multi can reuse the same InputActionAsset as Single.
