# BattleBallista — Features

Scannable list of capabilities. Version differences noted inline where they exist.

## Deploy

- **Hotkey siege deployer** — press the deploy key (keyboard, default **B**) or **D-pad right** (gamepad) to spawn the currently-selected engine ~4 m in front of the player
- **Configurable deploy key** *(145 line v1.2.0+, and the TAOM edition)* — MCM dropdown (~90 keys) under Mod Options → Battle Ballista (1.4.5); applies live, no restart. **Soft dependency**: without MCM the key is simply B. Gamepad binding is fixed. (1.3.15 original: hardcoded B, no MCM.)
- **Rising-edge detection** — one engine per keypress, not per held frame
- **Per-kind deploy cap** *(configurable since 145 v1.5.0 / TAOM v1.3.0)* — MCM integer slider 1–10, default **4**, applied per engine kind per battle; changes apply live, even mid-battle. Soft dependency: without MCM the cap is simply 4, clamped 1–10 on every read. (1.3.15 original: hardcoded 3.)
- **Terrain-aligned spawn** — engine's "up" axis matches the local terrain normal; firing axis points the player's look direction
- **Obstacle raycast clamp** — slides the spawn point closer to the player when geometry intercepts, so engines don't spawn inside walls / props
- **Mode gating** — silently no-ops outside `Mission.Mode == MissionMode.Battle` (so typing "B" in text fields doesn't trigger toasts)
- **Blocked mission-type list** — 23 controller class names (145 v1.5.2+/TAOM v1.3.2+): arena practice/duel, all tournament variants + town horse race, hideout (regular + ambush), lord's hall, house/indoor scripted scenes, stealth, sabotage, prison break, training field, the villa sneak, and seven NavalDLC storyline set-pieces. Matched by behavior class name (degrade-safe on renames). Deliberately unblocked: pirate battles / naval raids (terrain guard covers ship decks), town/village alley fights, sally-outs (toggle governs).
- **Sally-out deployment toggle** *(145 v1.4.0+ / TAOM v1.2.0+)* — sally-outs deployable again, MCM bool under Deployment, **default ON** (block was precautionary; vanilla engine AI supports sally-outs — 1.4.8 decompile). OFF restores the v1.1.6 blanket block. 1.3.15 line: still unconditionally blocked (no MCM there).
- **Verified contexts beyond field battles** — sieges (both sides); alley defense confirmed in play 2026-08-25 (never blocked; spawn + AI use worked)
- **Sea / ship bail-out** — terrain query NaN/Infinity → "this mission has no terrain to plant a ballista on" toast
- **Mid-battle deployment-hook init** (v1.1.3+) — fires the deployment hook on every spawned engine so AI agents can pilot it without crashing. Since v1.2.2 this calls the **public `SiegeWeapon.OnDeploymentFinished()`** rather than the internal threat-seeker init, so other mods' Harmony patches on that hook also run for mid-battle spawns (mod-compat fix)

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

- **Success** — "<EngineName> deployed (N/cap). Walk up and press F to use." (green)
- **Limit reached** — yellow toast naming the engine and the cap
- **Wrong mission type** — yellow "only deployable in regular battles and sieges"
- **No terrain** — yellow "this mission has no terrain to plant a ballista on"
- **Spawn returned null / threw** — red, includes exception type / prefab name

## Logging

- **Lazy log file** at `Modules/BattleBallista/BattleBallista.log` (initialized on first write; mirrored as `BattleBallista145.log` / `TAOMBattleBallista.log` on the other editions)
- **BasePath.Name lazy init** — never evaluated in a static field initializer (BLSE NRE protection)
- **Crash-forensics breadcrumbs** *(145 line v1.2.1+, and the TAOM edition)* — logs every deploy (kind, prefab, position, scene) and every pilot mount/dismount/destruction on deployed engines; transition-based, no per-tick spam. Ask crash reporters for this file.

## What this mod does NOT do

- **No Harmony patches of its own** (Harmony listed as a DependedModule only because UIExtenderEx transitively needs it)
- **No required MCM** — the 145/TAOM lines have three optional MCM settings behind soft-dependency guards (deploy key v1.2.0+, sally-out toggle v1.4.0+, deploy cap v1.5.0+ / TAOM v1.3.0+); without MCM everything falls back to defaults (B / ON / 4). The engine cycle itself is constant. The 1.3.15 original has no MCM surface at all.
- **No save data** — purely session state
- **No campaign behaviors** — no `CampaignBehaviorBase` subclasses

## Version differences

Three co-installable editions share one feature set; only dependency wiring and identifiers differ.

| Capability | Original (1.3.15, RETIRED 2026-10-17) | 145 (Bannerlord 1.5.x) | TAOM |
|---|---|---|---|
| Hotkey deploy | identical | identical | identical |
| Deploy key configurable (MCM, soft dep) | no — hardcoded B | yes (v1.2.0+), default B | yes, default B |
| MapBar cycle button | identical | identical | identical |
| Engine roster | identical | identical | identical |
| `CreateMissionObjectFromPrefab` call | 3-arg | 5-arg with `(hasCustomRestOffset=false, restOffset=0f)` | 5-arg |
| Mid-battle deployment-hook init | in source; never shipped (line retired) | shipped (v1.2.2 fires the public `OnDeploymentFinished`) | shipped |
| Dependencies | Harmony + UIExtenderEx (+ MCM optional) | same | **all via `TAOM.Dependencies`** |
| Sally-out deployment | blocked (unconditional) | toggle, default ON (v1.4.0+) | toggle, default ON (v1.2.0+) |
| Deploy cap | hardcoded 3 | MCM 1–10, default 4 (v1.5.0+) | MCM 1–10, default 4 (v1.3.0+) |
| Latest version | v1.2.0 source (RETIRED, never built) | v1.5.2 | v1.3.2 |
| All other behaviors | identical | identical | identical |

## Editions

- **`BattleBallista`** — Bannerlord 1.3.15. **RETIRED 2026-10-17.** The final source (v1.2.0,
  carries every fix) remains published for reference; no 1.3.15 binary was ever released with the
  AI-mount crash fix, and the line will not be built again.
- **`BattleBallista145`** — the actively shipped edition. v1.5.2 (2026-11-11) is built on game
  1.5.7: zero reference drift found 1.5.3→1.5.7, so the release is hardening (13 new blocked
  mission types incl. NavalDLC storyline scenes) + a hotkey-poll perf fix, not a breakage fix.
- **`TAOMBattleBallista`** — for TAOM total-conversion load orders. v1.3.2 matches 145 v1.5.2
  (same 1.5.7 hardening + perf fix). Same features as the 145 edition; resolves
  Harmony/UIExtenderEx/MCM through the `TAOM.Dependencies` bundle instead of the standalone modules,
  and needs no separate dependency mods. Last verified in-game 2026-08-22 (on 1.4.8).

All three use edition-suffixed module Ids, DLLs, namespaces, UIExtenderEx registrations, MCM settings
Ids and log files, so they never collide — but install only one, or you get duplicate hotkeys and
map buttons.
