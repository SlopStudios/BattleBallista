# BattleBallista — Features

Scannable list of capabilities. Version differences noted inline where they exist.

## Deploy

- **Hotkey siege deployer** — press the deploy key (keyboard, default **B**) or **D-pad right** (gamepad) to spawn the currently-selected engine ~4 m in front of the player
- **Configurable deploy key** *(145 line, v1.2.0+)* — MCM dropdown (~90 keys) under Mod Options → Battle Ballista (1.4.5); applies live, no restart. **Soft dependency**: without MCM the key is simply B. Gamepad binding is fixed. (1.3.15 original: hardcoded B, no MCM.)
- **Rising-edge detection** — one engine per keypress, not per held frame
- **Per-kind cap: 3 per battle** — Ballista, Catapult, Fire Catapult, Trebuchet each
- **Terrain-aligned spawn** — engine's "up" axis matches the local terrain normal; firing axis points the player's look direction
- **Obstacle raycast clamp** — slides the spawn point closer to the player when geometry intercepts, so engines don't spawn inside walls / props
- **Mode gating** — silently no-ops outside `Mission.Mode == MissionMode.Battle` (so typing "B" in text fields doesn't trigger toasts)
- **Blocked mission-type list** — refuses on arena practice, arena duel, tournament, hideout (regular + ambush), lord's hall fight, indoor scripted scenes, stealth, sabotage — matched by behavior class name (resilient to game version drift)
- **Sea / ship bail-out** — terrain query NaN/Infinity → "this mission has no terrain to plant a ballista on" toast
- **Mid-battle ThreatSeeker init** (v1.1.3+) — calls vanilla `RangedSiegeWeaponAi.InitializeThreatSeeker()` on the just-spawned engine so AI agents can pilot it without crashing

## Engine catalog

- **Ballista** (`ballista_a`) — bolts carried on the engine itself
- **Catapult** (`mangonel_a`) — bundled stone pile, spawns together with the engine
- **Fire Catapult** (`mangonel_a_fire`) — bundled pot pile (fire ammo, NOT regular stones)
- **Trebuchet** (`trebuchet_a`) — bundled stone pile
- **One press = one full deployment** (engine + matching ammo together — no alternation)
- **Fire Trebuchet intentionally NOT in the cycle** — the vanilla prefab is unverified in normal play, so it is deliberately left out

## MapBar cycle button

- **UIExtenderEx-injected** — `PrefabExtension` on `MapBar.xml` via xpath `descendant::Prefab/Window/Widget/Children` + `InsertType.Child`
- **Single button on the campaign map** — cycles Ballista → Catapult → Fire Catapult → Trebuchet → back to Ballista
- **Coexists with ROT AI's World Events button** — same xpath, ordered visually by `MarginRight` (BB=170, ROT AI=12); side-by-side, no overlap
- **Session-only state** — resets to Ballista on game restart
- **Cross-mod static-class entry point** — `SiegeCycle.Current` / `SiegeCycle.Next()` are public statics so ROT AI's mixin can flip the cycle via reflection without a hard DLL dependency

## Toasts (info-feed feedback)

- **Success** — "<EngineName> deployed (N/3). Walk up and press F to use." (green)
- **Limit reached** — yellow toast naming the engine and the cap
- **Wrong mission type** — yellow "only deployable in regular battles and sieges"
- **No terrain** — yellow "this mission has no terrain to plant a ballista on"
- **Spawn returned null / threw** — red, includes exception type / prefab name

## Logging

- **Lazy log file** at `Modules/BattleBallista/BattleBallista.log` (initialized on first write; mirrored as `BattleBallista145.log` on the 145 line)
- **BasePath.Name lazy init** — never evaluated in a static field initializer (BLSE NRE protection)
- **Crash-forensics breadcrumbs** *(145 line, v1.2.1+)* — logs every deploy (kind, prefab, position, scene) and every pilot mount/dismount/destruction on deployed engines; transition-based, no per-tick spam. Ask crash reporters for this file.

## What this mod does NOT do

- **No Harmony patches of its own** (Harmony listed as a DependedModule only because UIExtenderEx transitively needs it)
- **No required MCM** — the 145 line has one optional MCM setting (deploy key, v1.2.0+) behind a soft-dependency guard; everything else (cycle, cap) is constant. The 1.3.15 original has no MCM surface at all.
- **No save data** — purely session state
- **No campaign behaviors** — no `CampaignBehaviorBase` subclasses

## Version differences

| Capability | Original (1.3.15) | 145 (1.4.5) |
|---|---|---|
| Hotkey deploy | identical | identical |
| Deploy key configurable (MCM, soft dep) | no — hardcoded B | yes (v1.2.0+), default B |
| MapBar cycle button | identical | identical |
| Engine roster | identical | identical |
| `CreateMissionObjectFromPrefab` call | 3-arg | 5-arg with `(hasCustomRestOffset=false, restOffset=0f)` |
| Mid-battle `InitializeThreatSeeker` call (v1.1.3+) | source pre-applied, DLL not yet rebuilt | shipped in `dist/BattleBallista145-v1.1.3.7z` |
| All other behaviors | identical | identical |
