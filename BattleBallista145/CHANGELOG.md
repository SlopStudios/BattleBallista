# Battle Ballista 145 (Bannerlord 1.4.5) — Changelog

## v1.2.3-145 (2026-07-20) — Log a session header: mod version, game version, active module list

**Pure logging, no gameplay change.** The first time anything is written to
`BattleBallista145.log` in a session, a header line now records the mod version, the game version
(with build number), and the **full list of active modules**. Crash reports from other users keep
pointing at load-order interactions we can't reproduce — with this, an attached log answers "which
version are you on and what else are you running" without any back-and-forth. Every part of the
header is individually guarded; if an engine API is unavailable the field logs as "?" instead of
failing.


## v1.2.2-145 (2026-07-20) — Mod compatibility: fire the real deployment hook on spawned engines

**Likely fix for the unreproducible "unhandled exception while using an engine in a field battle"
report — root-caused to a mod-interaction gap on our side.** Since v1.1.4, freshly spawned engines
had their threat-seeker initialized by calling the internal `Ai.InitializeThreatSeeker()` directly.
That is what vanilla's deployment-end hook does — but only the *vanilla part of it*. Combat and
siege overhaul mods commonly Harmony-patch `SiegeWeapon.OnDeploymentFinished()` to attach their own
per-weapon state (custom projectile physics, damage models, AI replacements): on a vanilla siege
engine their patch runs at deployment end; on a Battle Ballista engine it **never ran at all**,
so the overhaul mod's later patches on the *usage* path (shooting, aiming, AI ticks) could hit
their own missing state and crash — only in load orders containing such a mod, which is why it
never reproduced here.

**Change.** The post-spawn walk now calls each spawned weapon's **public
`OnDeploymentFinished()`** — exactly what vanilla's `TeamAISiegeComponent.OnDeploymentFinished()`
does per scene siege weapon at the end of a siege deployment phase (decompile-verified on 1.4.7:
the override's entire body is the threat-seeker init; every base implementation in the
`MissionObject` chain is empty). Identical behavior in a vanilla load order; in a modded one,
third-party deployment patches now run for mid-battle spawns too. The walk also matches
`SiegeWeapon` (not just `RangedSiegeWeapon`), mirroring vanilla's `SceneSiegeWeapons` loop, and
keeps per-weapon try/catch — a throwing third-party hook is contained and logged to
`BattleBallista145.log` instead of crashing the mission.

Includes v1.2.1's crash-forensics breadcrumbs unchanged.


This is the **Bannerlord 1.4.5** edition of Battle Ballista, shipped as a separate
module (`BattleBallista145`) alongside the 1.3.15 original. Same mod, same features —
forked only for 1.4.5 API compatibility. Install into `Modules/BattleBallista145/`.

## v1.2.1-145 (2026-07-20) — Diagnostic breadcrumb logging for in-battle engine crashes

**No gameplay change.** A user reported an unhandled-exception crash "while using a ballista in a
regular field battle" with **no crash log of any kind**. A full decompile audit of the 1.4.7
engine-usage path (weapon AI, threat seeker, missile handling) found no reproducible defect in
v1.2.0 — the most probable causes remain stale installs (the pre-fix v1.1.3/v1.1.4 zips, or any
1.3.15-line build). Since this mod deliberately ships no Harmony patches, it cannot catch
exceptions inside vanilla weapon ticks — so instead, v1.2.1 makes future reports diagnosable:

- **Every deploy is logged** (engine kind, running count, prefab, position, scene name).
- **Every pilot change on a deployed engine is logged** — who mounted or dismounted, whether they
  were AI or player-controlled, and their team side.
- **Engine destruction is logged.**

Log file: `Modules/BattleBallista145/BattleBallista145.log`. If a crash happens while an engine is
in use, the last lines of that file identify exactly which engine and which pilot were involved —
ask crash reporters to attach it. Logging is transition-based (a handful of lines per battle, no
per-tick spam) and each tracked engine is wrapped in its own guard so a broken engine drops out of
tracking instead of cascading.

## v1.2.0-145 (2026-07-17) — MCM setting: configurable deploy hotkey

**New feature.** The keyboard deploy key (previously hardcoded to **B**) is now configurable through
MCM (Mod Configuration Menu): **Options → Mod Options → Battle Ballista (1.4.5) → Hotkey → Deploy Key**.
Dropdown of ~90 keys (letters, number row, F1–F12, navigation cluster, punctuation, numpad). Applies
immediately — no restart, no new battle needed. The gamepad binding (D-pad Right) is fixed and
unaffected.

**MCM is a SOFT dependency — the mod still works without it.** All MCM access is isolated in one
`NoInlining` guarded method (`DeployHotkey.ReadFromSettings`); if MCM isn't installed (or fails to
load), the type-load exception is caught once and the hotkey permanently falls back to the default
**B** for that session. `SubModule.xml` declares `Bannerlord.MBOptionScreen` with `Optional="true"`
so the launcher sorts MCM before us when present but never blocks enabling the mod when absent.
This deliberately diverges from the sibling ROT mods' hard MCM dependency: Battle Ballista's whole
feature set is usable without configuration, so a missing MCM shouldn't kill the mod (see the
2026-07-17 missing-button investigation — dependency-not-installed is the top real-world failure mode).

Settings persist globally at `Documents/Mount and Blade II Bannerlord/Configs/ModSettings/Global/BattleBallista145/`
(MCM `json2` format). No save data, as before.

Built against the live Bannerlord **1.4.7** game bin (the module remains named/branded 1.4.5-line;
a same-day audit of every API the mod touches found zero drift between 1.4.5 and 1.4.7 — see
llm-notes). DLL grew from 15,360 → 18,944 bytes due to the new settings code; the old
"optimized ≈ 15,360 B" size heuristic is superseded — verify builds via the `Debuggable` attribute
check instead.

## v1.1.6-145 (2026-06-21) — Proactive: block siege-engine deployment in sally-out battles

**Defensive hardening (not tied to a confirmed report).** A sally-out battle (defenders sallying out of a
besieged settlement) runs as `MissionMode.Battle` but on a **siege-derived scene** that the vanilla
`RangedSiegeWeapon` AI / ThreatSeeker is not set up to handle. Battle Ballista's deploy gate previously
blocked only arena/tournament/hideout/indoor controllers and allowed any other `MissionMode.Battle` mission,
so a player *could* plant a ballista/catapult in a sally-out — a context where the engine's native AI tick
is a plausible source of an `AccessViolationException` in `MissionState.TickMission` (which managed code
can't catch). We have not reproduced this, and a sally-out load-screen crash investigated this build was
confirmed by the player to be **unrelated to Battle Ballista**; this change simply removes a risky,
unsupported deployment context that never made sense to allow.

**Change.** `HasBlockedMissionBehavior()` now also blocks **every sally-out controller variant**
(`SallyOutMissionController` / `SandBoxSallyOutMissionController` / `CustomSallyOutMissionController`, plus
any mod-renamed one) via a case-insensitive `"SallyOut"` substring match. Pressing the deploy key in a
sally-out now shows the normal "only deployable in regular battles and sieges" toast. Normal siege assaults
and field battles are unaffected.

## v1.1.5-145 (2026-06-04) — Build fix: v1.1.4 shipped as a Debug build and crashed on startup

**v1.1.4's code was correct, but its binary was built wrong.** v1.1.4 was accidentally compiled as a **Debug / unoptimized / Edit‑and‑Continue** assembly (15,872 bytes, `Debuggable(DisableOptimizations | EnableEditAndContinue)`) instead of an optimized Release build (15,360 bytes, the profile every prior release including v1.1.3 shipped with). **Edit‑and‑Continue assemblies cannot be reliably processed by MonoMod/Harmony**, so in a heavily Harmony‑patched load order the module failed during startup processing — the game **crashed on launch** for modded users (matching the `GetNativeMethodBody` / "method not fully instantiated" MonoMod errors in their logs). On a light load order it loaded fine, which is why it wasn't caught here. v1.1.3 launched for everyone because it was a normal optimized build (it still had the *in‑battle* catapult NRE — that's what v1.1.4's code fixes).

**Fix.** Identical source to v1.1.4 — only the **build** changed: recompiled as an optimized, non‑EnC Release assembly (15,360 bytes, clean `Debuggable`), matching the working v1.1.3 profile. The `.csproj` now pins `<Optimize>true</Optimize>` + `<DebugType>portable</DebugType>` so a stray `dotnet build` can't emit a Debug/EnC binary again. Portable PDB still included for crash reports.

If you were on the broken v1.1.4 ("crashes on startup"), this is the build to install. It contains the same catapult/trebuchet threat‑seeker fix v1.1.4 introduced.

## v1.1.4-145 (2026-06-04) — Crash fix (for real this time): catapults & trebuchets *(code correct, but shipped as a broken Debug build — see v1.1.5)*

**The v1.1.3 fix only ever covered the ballista.** Catapults (mangonel), fire catapults, and trebuchets still crashed the mission the instant an AI agent mounted one — the exact `NullReferenceException` in `RangedSiegeWeaponAi+ThreatSeeker.GetAllThreats()` that v1.1.3 was supposed to eliminate.

**Why v1.1.3 missed them.** `Mission.CreateMissionObjectFromPrefab` returns only the **root** entity's first `MissionObject` script. Among the engine prefabs, only `ballista_a` carries its weapon script (`Ballista`) on the root. `mangonel_a` and `trebuchet_a` put a `SynchedMissionObject` **wrapper** on the root and the actual `Mangonel` / `Trebuchet` weapon script on a **child** entity. So v1.1.3's `mo is RangedSiegeWeapon` pattern-match succeeded for ballistas but silently returned `false` for catapults and trebuchets — `InitializeThreatSeeker()` was never called on them, their `_potentialTargetObjects` stayed null, and they NRE'd on AI mount exactly as before. (The v1.1.3 note claiming it "hits both ballista and catapult (and trebuchet)" was verified by decompile signature, not by where the script actually lives in the prefab tree — so it shipped broken.)

**Fix.** `src/BattleBallistaMissionBehavior.cs` now ignores the returned `MissionObject`'s type entirely. It captures the spawned **root `GameEntity`** (via the spawn callback) and walks the whole entity tree recursively (`GetScriptComponents<RangedSiegeWeapon>()` + `GetChildren()`), calling `Ai.InitializeThreatSeeker()` on **every** `RangedSiegeWeapon` it finds — wherever in the hierarchy the script sits. Accessing `.Ai` lazily creates the concrete `BallistaAI`/`MangonelAI`/`TrebuchetAI` (all `RangedSiegeWeaponAi` subclasses) and the init populates the threat-seeker target lists, mirroring vanilla's `OnDeploymentFinished`. Idempotent, per-weapon try/catch, harmless on non-weapon prefabs.

Built against the live Bannerlord 1.4.5 game bin (June 2 update). Replaces the v1.1.3-145 zip, which crashed every field battle the moment an AI mounted a player-spawned catapult or trebuchet.

## v1.1.3-145 (2026-05-31) — Crash fix: AI agents on player-spawned engines in field battles

**Crash fix.** If an AI agent ever mounted a player-spawned ballista or catapult during a regular field battle, the mission crashed with a `NullReferenceException` inside vanilla `RangedSiegeWeaponAi+ThreatSeeker.GetAllThreats()`. **(Superseded by v1.1.4-145 — this fix only worked for the ballista; see above.)**

**Root cause.** Every freshly-created ranged siege weapon has its threat-seeker target lists (`_potentialTargetObjects`, `_referencePositions`) populated by exactly one call site in the engine — `RangedSiegeWeapon.OnDeploymentFinished()` calls `(Ai as RangedSiegeWeaponAi).InitializeThreatSeeker()` — and that hook only fires **once**, at the end of the battle's deployment phase. Engines spawned **mid-battle** via `Mission.CreateMissionObjectFromPrefab` (which is BattleBallista's whole job) miss that hook, so both target lists stay `null`. The moment any AI agent mounts the engine, the very first tick goes `RangedSiegeWeaponAi.OnTick → FindNextTarget → ThreatSeeker.PrepareThreatSeekerTask → GetAllThreats`, and the first line of `GetAllThreats` (`for (int num = _potentialTargetObjects.Count - 1; ...)`) dereferences the null list and the mission NREs out.

**Fix.** In `src/BattleBallistaMissionBehavior.cs`, immediately after the engine spawns, the just-returned MissionObject is pattern-matched to `RangedSiegeWeapon` and its `Ai.InitializeThreatSeeker()` is called — exactly mirroring vanilla's `OnDeploymentFinished` path. Wrapped in try/catch; the pattern-match silently skips any non-`RangedSiegeWeapon` prefab. Verified against the 1.4.5 decompiles of `RangedSiegeWeaponAi.ThreatSeeker.GetAllThreats` and `RangedSiegeWeapon.OnDeploymentFinished`.

Hits both ballista and catapult (and trebuchet) — any ranged siege engine — and applies whether the AI agent is friendly or enemy. Replaces the v1.1.2-145 zip, which would crash any field battle the moment an AI mounted a deployed engine.

## v1.1.2-145 (2026-05-25) — Fork for Bannerlord 1.4.5

Built from the v1.1.2 source against Bannerlord 1.4.5 game DLLs as a fully standalone
module: its own module Id, DLL, namespace, and UIExtenderEx registration name
(`BattleBallista145`), so it never collides with the 1.3.15 original if both are present.

### Fixed — 1.4.5 API drift

- **`Mission.CreateMissionObjectFromPrefab`** gained two parameters in 1.4.5 —
  `(bool hasCustomRestOffset, float restOffset)` — between the `MatrixFrame` and the
  `Action<GameEntity>` callback. The deploy call now passes `false, 0f`, which keeps each
  engine's prefab-default rest offset (identical to the pre-1.4.5 placement). This is the
  only signature that drifted; every other engine/scene/agent/input call compiles
  unchanged against 1.4.5.

### Re-validated for 1.4.5 (no code change needed)

- **MapBar cycle button (UIExtenderEx).** The injection xpath
  `descendant::Prefab/Window/Widget/Children` still resolves against 1.4.5's
  `SandBox/GUI/Prefabs/Map/MapBar.xml`, and `MapNavigationVM` plus the `{MapNavigation}`
  DataSource context are intact — so the Ballista/Catapult/Fire Catapult/Trebuchet cycle
  button still appears and binds. (Shares the exact injection point as ROT AI 145's World
  Events button; they sit side-by-side without overlap.)

### Unchanged

Hotkey deploy (B / D-pad right), the 3-per-kind-per-battle cap, terrain alignment,
obstacle clamping, mission-type gating, and all toasts are identical to the 1.3.15 v1.1.2
release. Purely a recompile plus the one signature fix above.

## v1.1.2 (2026-05-19) — silence the deploy hotkey outside battle missions

**Upgrade as always: extract on top of your existing `Modules/BattleBallista/` folder.**

### Fixed

- **The yellow "Battle Ballista: only deployable in regular battles and sieges" toast no longer fires during AI chat conversations, town walks, or any other non-battle mission.** Root cause: `BattleBallistaMissionBehavior` attaches to every mission (intentional — lobby and cutscene missions are normally harmless), and `OnMissionTick` polled `Input.IsKeyDown(InputKey.B)` directly from raw OS keyboard state. Gauntlet text-field focus is invisible to that API, so typing the letter `b` in a ROT AI chat reply (or any other text input layered over a conversation mission) rose-edged through the input poll, hit the `Mission.Mode != MissionMode.Battle` guard inside `TrySpawnSiege`, and surfaced the warning toast over the chat. Fix at `src/BattleBallistaMissionBehavior.cs:79-89`: early-return in `OnMissionTick` whenever `Mission.Current.Mode != MissionMode.Battle`, before reading input. The rising-edge state (`_wasKeyDown`) is also reset on bail, so a key held across a mode transition cannot false-trigger when the next Battle mission begins. The redundant `MissionMode.Battle` check inside `TrySpawnSiege` is left in place as defense-in-depth.

### Unchanged

The hotkey still works exactly as before in real field battles and sieges, and the helpful "this mission has no terrain to plant a ballista on" / "Ballista limit reached" toasts still fire when you press B inside a Battle mission that has a specific blocker (lord's hall fight, tournament, arena, naval scenes without ground terrain, hideout ambushes, indoor scripted missions). Only the case where you can't possibly have meant to deploy — non-battle missions — now silently swallows the keypress.

### Why now

User reported pressing B during an AI chat surfaced the "only deployable in battles" toast over the chat window. The toast itself was harmless — the underlying spawn attempt was correctly blocked by `TrySpawnSiege`'s guards — but the popup broke conversation immersion and leaked the existence of the hotkey conflict into the chat fiction.
