# Battle Ballista — Changelog

## v1.1.6 (2026-07-19) — Block deployment in sally-out battles (ported from the 1.4.5 edition)

**⚠ SOURCE-ONLY — NOT YET BUILT OR SHIPPED.** The game install is currently on 1.4.7, and this
1.3.15 edition can only be compiled against a 1.3.15 game bin. This entry (and v1.1.4 below) will
ship together in one build at the next 1.3.15 rollback window. **Until then, every distributed
1.3.15 build is still v1.1.2-era and crashes the mission if any AI agent mounts a deployed
engine** — the exact "unhandled exception while using an engine in a field battle" symptom. If a
1.3.15 user reports that crash, it is this known issue; the only workaround is not letting AI man
the engines (or switching to the 1.4.5+ edition, which shipped the fix in v1.1.4/v1.1.5).

**Change.** `HasBlockedMissionBehavior()` now refuses deployment in every sally-out controller
variant (`SallyOutMissionController` / `SandBoxSallyOutMissionController` /
`CustomSallyOutMissionController`, or any mod-renamed one) via case-insensitive `"SallyOut"`
substring match — sally-outs are `MissionMode.Battle` on a siege-derived scene the engine AI
doesn't support (native `AccessViolationException` risk, crash id 639f8615 on the 145 line).
Version numbering jumps 1.1.4 → 1.1.6 to stay aligned with the 1.4.5 edition (its v1.1.5 was a
build-pipeline fix specific to that line; no code from it applies here).

**Also ported (2026-07-20, from BB145 v1.2.2): mod-compat deployment hook.** The post-spawn init
walk now calls each spawned weapon's public `OnDeploymentFinished()` (walking `SiegeWeapon`)
instead of the internal `Ai.InitializeThreatSeeker()` — vanilla-identical, but it lets other mods'
Harmony patches on the deployment hook run for mid-battle spawns too. See the BB145 v1.2.2
changelog entry for the full rationale. This will ship in the same 1.3.15 build as the rest of
this entry.

## v1.1.4 (2026-06-04) — Crash fix (for real this time): catapults & trebuchets

**Upgrade as always: extract on top of your existing `Modules/BattleBallista/` folder.**

**The v1.1.3 fix only ever covered the ballista.** Catapults (mangonel), fire catapults, and trebuchets still crashed the mission the instant an AI agent mounted one — the same `NullReferenceException` in `RangedSiegeWeaponAi+ThreatSeeker.GetAllThreats()` that v1.1.3 was supposed to eliminate.

**Why v1.1.3 missed them.** `Mission.CreateMissionObjectFromPrefab` returns only the **root** entity's first `MissionObject` script. Among the engine prefabs, only `ballista_a` carries its weapon script (`Ballista`) on the root. `mangonel_a` and `trebuchet_a` put a `SynchedMissionObject` **wrapper** on the root and the actual `Mangonel` / `Trebuchet` weapon script on a **child** entity. So v1.1.3's `mo is RangedSiegeWeapon` pattern-match succeeded for ballistas but silently returned `false` for catapults and trebuchets — `InitializeThreatSeeker()` was never called on them, their `_potentialTargetObjects` stayed null, and they NRE'd on AI mount exactly as before.

**Fix.** `src/BattleBallistaMissionBehavior.cs` now ignores the returned `MissionObject`'s type entirely. It captures the spawned **root `GameEntity`** (via the spawn callback) and walks the whole entity tree recursively (`GetScriptComponents<RangedSiegeWeapon>()` + `GetChildren()`), calling `Ai.InitializeThreatSeeker()` on **every** `RangedSiegeWeapon` it finds — wherever in the hierarchy the script sits. Confirmed against the 1.4.5 decompile that this replicates 100% of `OnDeploymentFinished` (its `base` chain is an empty `MissionObject.OnDeploymentFinished()`; no concrete engine overrides it). The 1.4.5 edition (`BattleBallista145` v1.1.4) ships this same fix, built and verified.

**Build status (1.3.15 line):** source is pre-applied with this fix, same as v1.1.3 was. The 1.3.15 DLL is **not** rebuilt here — it's rebuilt on the next 1.3.15 install swap (building against a live 1.4.5 install would produce a wrong-target DLL). The bundled `bin/` DLL remains the pre-v1.1.4 build until then.

## v1.1.3 (2026-05-31) — Crash fix: AI agents on player-spawned engines in field battles *(superseded by v1.1.4 — this approach only worked for the ballista; see above)*

**Upgrade as always: extract on top of your existing `Modules/BattleBallista/` folder.**

**Crash fix.** If an AI agent (friendly or enemy) ever mounted a player-spawned ballista or catapult during a regular field battle, the mission crashed with a `NullReferenceException` inside vanilla `RangedSiegeWeaponAi+ThreatSeeker.GetAllThreats()`.

**Root cause.** Every freshly-created ranged siege weapon has its threat-seeker target lists (`_potentialTargetObjects`, `_referencePositions`) populated by exactly one call site in the engine — `RangedSiegeWeapon.OnDeploymentFinished()` calls `(Ai as RangedSiegeWeaponAi).InitializeThreatSeeker()` — and that hook only fires **once**, at the end of the battle's deployment phase. Engines spawned **mid-battle** via `Mission.CreateMissionObjectFromPrefab` (which is Battle Ballista's whole job) miss that hook, so both target lists stay `null`. The moment any AI agent mounts the engine, the very first tick goes `RangedSiegeWeaponAi.OnTick → FindNextTarget → ThreatSeeker.PrepareThreatSeekerTask → GetAllThreats`, and the first line of `GetAllThreats` (`for (int num = _potentialTargetObjects.Count - 1; ...)`) dereferences the null list and the mission NREs out.

**Fix.** In `src/BattleBallistaMissionBehavior.cs`, immediately after the engine spawns, the just-returned MissionObject is pattern-matched to `RangedSiegeWeapon` and its `Ai.InitializeThreatSeeker()` is called — exactly mirroring vanilla's `OnDeploymentFinished` path. Wrapped in try/catch; the pattern-match silently skips any non-`RangedSiegeWeapon` prefab.

Hits any ranged siege engine (ballista, catapult, fire catapult, trebuchet) regardless of whether the AI pilot is friendly or enemy. The crash was visible on the 1.4.5 lineage (`BattleBallista145` v1.1.3 ships the same fix); the same `InitializeThreatSeeker` lifecycle path exists on 1.3.15, so this fix is also pre-applied here for when you next rebuild against your 1.3.15 install.

## v1.1.2 (2026-05-19) — silence the deploy hotkey outside battle missions

**Upgrade as always: extract on top of your existing `Modules/BattleBallista/` folder.**

### Fixed

- **The yellow "Battle Ballista: only deployable in regular battles and sieges" toast no longer fires during AI chat conversations, town walks, or any other non-battle mission.** Root cause: `BattleBallistaMissionBehavior` attaches to every mission (intentional — lobby and cutscene missions are normally harmless), and `OnMissionTick` polled `Input.IsKeyDown(InputKey.B)` directly from raw OS keyboard state. Gauntlet text-field focus is invisible to that API, so typing the letter `b` in a ROT AI chat reply (or any other text input layered over a conversation mission) rose-edged through the input poll, hit the `Mission.Mode != MissionMode.Battle` guard inside `TrySpawnSiege`, and surfaced the warning toast over the chat. Fix at `src/BattleBallistaMissionBehavior.cs:79-89`: early-return in `OnMissionTick` whenever `Mission.Current.Mode != MissionMode.Battle`, before reading input. The rising-edge state (`_wasKeyDown`) is also reset on bail, so a key held across a mode transition cannot false-trigger when the next Battle mission begins. The redundant `MissionMode.Battle` check inside `TrySpawnSiege` is left in place as defense-in-depth.

### Unchanged

The hotkey still works exactly as before in real field battles and sieges, and the helpful "this mission has no terrain to plant a ballista on" / "Ballista limit reached" toasts still fire when you press B inside a Battle mission that has a specific blocker (lord's hall fight, tournament, arena, naval scenes without ground terrain, hideout ambushes, indoor scripted missions). Only the case where you can't possibly have meant to deploy — non-battle missions — now silently swallows the keypress.

### Why now

User reported pressing B during an AI chat surfaced the "only deployable in battles" toast over the chat window. The toast itself was harmless — the underlying spawn attempt was correctly blocked by `TrySpawnSiege`'s guards — but the popup broke conversation immersion and leaked the existence of the hotkey conflict into the chat fiction.
