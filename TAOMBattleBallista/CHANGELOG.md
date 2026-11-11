# Battle Ballista (TAOM) — Changelog

The **TAOM** edition of Battle Ballista, shipped as a separate module (`TAOMBattleBallista`)
alongside the standalone `BattleBallista` (1.3.15) and `BattleBallista145` (1.4.5–1.4.7) editions.
Same mod, same features — forked so it resolves its dependencies through **TAOM.Dependencies**
instead of the standalone Harmony / UIExtenderEx / MCM modules. Install into
`Modules/TAOMBattleBallista/`.

## v1.3.2 (TAOM) 2026-11-11 — Bannerlord 1.5.7 sweep: 13 new blocked mission types + hotkey-poll micro-optimization

Game moved 1.5.5→1.5.6 (11-04)→1.5.7 (11-10). A full refaudit of the shipped DLL against live
1.5.7 shows **zero reference drift** (87/87, same as 1.5.3/1.5.5) and all ten existing blocked
controller names still exist — this release is therefore compatibility-hardening + perf, not a
breakage fix.

- **Block list extended by 13 scripted/arena scene controllers** found in the 1.5.7 + NavalDLC
  assemblies where deploying an engine would be nonsensical or could break quest state:
  tournament archery/jousting, town horse race, prison break, training field (tutorial), the
  villa sneak, and seven NavalDLC storyline set-pieces (alley fight, captivity, floating
  fortress, helping-an-ally, blocked estuary, quest-5 set piece, wounded beast). Deliberately
  NOT blocked: PirateBattle/NavalRaid (real combat — ship decks already bail via the terrain
  guard, shore raids are legitimate deploys), town/village alley fights (user-loved context),
  sally-outs (MCM toggle governs). String-matched as before — degrade-safe.
- **Perf: the deploy-key MCM read is now cached (1 s TTL).** `DeployHotkey.Current` is polled
  every mission tick (~60 Hz); each read walked MCM's settings-provider lookup and string-parsed
  the key name. The tick path is now two field reads; MCM changes still apply within a second.
  (From the 2026-11-11 performance review; the map-bar mixin and breadcrumb tracking were
  reviewed and are already event-/transition-driven.)
- Optimized build verified (Debuggable=IgnoreSymbolStoreSequencePoints), refaudit clean on
  1.5.7, Game Pass mirror refreshed (both runtime folders).


## v1.3.1-TAOM (2026-09-17) — 1.5.3 rebuild (audited clean)

**Maintenance rebuild for Bannerlord 1.5.3 — no code changes.** The game updated 1.4.8 to 1.5.3;
a full member-reference audit of the shipped DLL against the 1.5.3 assemblies found **zero stale
references** (81/81 clean), and the source compiles 0/0 against the 1.5.3 game bin. This release
is that clean rebuild, so the binary provably targets the current game. (The same update broke six
APIs used by SlopTweaks — Battle Ballista's API surface was untouched.) Not yet play-tested on
1.5.3; the audit covers references, not behavior.

## v1.3.0-TAOM (2026-08-28) — Configurable deploy cap (1-10, default 4)

**The per-engine-type deploy cap is now configurable — and the default rises from 3 to 4.**
New MCM setting: Mod Options → Battle Ballista → Deployment → **"Max deployments per engine type"**,
an integer slider from **1 to 10**, default **4** (previously a hardcoded 3). The cap applies to each
engine type separately, exactly as before — at default you can now field up to 4 Ballistas, 4
Catapults, 4 Fire Catapults and 4 Trebuchets in one battle.

Changes apply immediately, even mid-battle: raising the cap un-caps a type you had exhausted;
lowering it stops further deploys of a type already at or over the new value (engines already placed
stay). Both the limit toast and the "deployed (N/M)" counter read the live value.

Follows the established soft-dependency pattern (setting in the MCM class, `DeployCapSetting`
guarded accessor): without MCM installed the cap is simply 4. The value is clamped to 1–10 on every
read, so a hand-edited settings file can never produce a zero or absurd cap.

## v1.2.0-TAOM (2026-08-25) — Sally-out deployment toggle (default: allowed)

**Sally-out battles are deployable again — with an MCM off-switch.** Sally-outs (defenders
sallying out of a besieged settlement while you besiege it) were blocked outright in v1.1.6 as a
precaution after a crash report that was later confirmed unrelated to Battle Ballista; the block was
never backed by a reproduced failure. Since then the evidence has moved the other way:

- The 1.4.8 decompile shows vanilla's siege-engine AI **explicitly supports sally-outs** — the threat
  evaluation handles `TeamAISallyOutAttacker` / `TeamAISallyOutDefender` by name and the mission has a
  dedicated `SallyOut` team-AI type. TaleWorlds designed engine AI to run there.
- The v1.2.2 deployment-hook fix removed the likeliest class of context-specific crash.
- Alley-defense fights (a similarly "unusual" context that was never blocked) were verified crash-free
  in play, spawn through AI-use included.

**New setting:** Mod Options → Battle Ballista → Deployment → **"Allow sally-out deployment"** —
**ON by default** (the owner's call; the maintainer default-off recommendation was considered and
declined in favor of the feature being discoverable). Turn it OFF to restore the old v1.1.6 blanket
block if you hit trouble in battles outside a besieged settlement's walls. Applies immediately, no
restart. Without MCM installed, sally-outs are allowed (matching the default).

Implementation follows the established soft-dependency pattern: the setting lives in the MCM settings
class, read through a NoInlining guarded accessor (`SallyOutToggle`), so no-MCM installs keep working.
The crash-forensics breadcrumbs log every deploy with the scene name, so if a sally-out ever does
misbehave, the log will say so.

**Not yet verified in a sally-out in-game** — if one goes wrong, flip the toggle off and send
the log file.

## v1.1.1-TAOM (2026-08-25) — Hardening of the raised-surface placement

Supersedes v1.1.0 before distribution (its archive was replaced in `dist/`). Same feature; two
defects found in an adversarial review of the v1.1.0 probe, fixed:

**Post-review hardening (adversarial review, same day):**
- **Hits above your feet are rejected** (max step-up 0.5m). The probe ray still *starts* above head
  height to clear clutter, but a hit that high is a parapet/merlon, a crate, or the chassis of an
  engine you already deployed — not the surface underfoot. Rejecting it makes the retry loop step
  closer and find the real walkway, instead of perching the new engine on top of the obstacle.
- **The probe never reaches beyond the obstacle clamp.** With a wall directly ahead the clamp pulls
  the spawn reach very short; the retry floor used to push the probe back out past 1.2m — beyond the
  very obstacle the clamp detected — and repeat the identical probe four times. Distances now cap at
  the clamped reach and duplicate probes are skipped.

## v1.1.0-TAOM (2026-08-23) — Deploy on walls and other raised surfaces

**Siege engines can now be deployed on walls, walkways and other raised surfaces — not just
on the ground.** Reported by a player: standing on a castle wall during a siege and deploying put the
engine on the courtyard floor several metres below, instead of on the wall underfoot. Field battles
were unaffected, which is what made it look like a siege-only quirk.

**Root cause.** The spawn point asked `Scene.GetTerrainHeightAndNormal(...)` for its height. That
returns the height of the **terrain** — and castle walls, wooden walkways, siege-tower platforms,
bridges and ship decks are static mesh entities, not terrain. On a wall, the terrain under your feet
*is* the courtyard, so the engine was placed exactly where the code asked: far below you.

**Fix.** The spawn point is now resolved by probing straight **down from the player's own head
height** and taking the first solid surface found — wall or ground alike. Specifics:

- **Probe band follows the player.** The ray starts slightly above head height (so it begins in the
  same space you occupy rather than catching a roof or arch above) and reaches a few metres down, so
  a step or a short drop still counts but a wall-to-courtyard fall does not.
- **Walks back toward you if there is nothing ahead.** Deploying while looking out over the
  battlements used to fling the engine off the edge; the spot now steps closer to you until it finds
  the walkway you are standing on.
- **Refuses instead of dropping.** If there is genuinely no surface at your level and the terrain is
  far below, you get a yellow "nothing solid in front of you to place on" toast rather than an engine
  silently landing in the courtyard.
- **Tilt on raised surfaces.** The engine gives no surface normal for mesh raycasts, so one is
  derived from three probes around the spot: flat walkways come out level, ramps come out sloped, and
  any implausible result falls back to level via the existing tilt guard.

**Field battles are unchanged by design.** When the probe lands on terrain, the original terrain
height and slope-aligned normal are used exactly as before, and the sea/no-terrain bail-out is still
the first gate, so naval missions behave identically.

**Implementation note for maintainers:** this deliberately does NOT use
`Scene.GetGroundHeightAtPosition(pos, out normal, ...)`, which looks perfect for the job. Verified in
the 1.4.8 decompile that this overload sets `normal = Vec3.Invalid` and never assigns it — trusting
its normal would silently produce a garbage frame. The terrain-vs-mesh test also compares heights
rather than using the hit-entity overload, because that one returns `WeakGameEntity` on 1.4.x and it
is unverified whether the type exists on 1.3.15; the height comparison compiles on every line.

**Not yet verified in-game** — please test on a wall during a siege, and confirm a normal field
battle still behaves as before.

## v1.0.0-TAOM (2026-08-22) — Initial TAOM edition

> **VERIFIED IN-GAME (2026-08-22)** under a real TAOM load order: the module loads, the
> MapBar cycle button injects through TAOM.Dependencies' bundled UIExtenderEx 2.13.2, and
> deployment works. No separate Harmony / UIExtenderEx / MCM modules required.
>
> **Game version:** built against the live game bin, which is **Native v1.4.8**, and verified
> there. (TAOM itself still pins `Native v1.4.7.*`; it runs on 1.4.8 regardless.) No systematic
> 1.4.7 to 1.4.8 API audit was performed — this is a clean compile plus an in-game check, and
> BB's API surface is narrow.

Forked from `BattleBallista145` v1.2.3, which is feature-current: MCM-configurable deploy hotkey,
the mod-compatible deployment hook, crash-forensics logging, sally-out blocking and the full
mid-battle threat-seeker fix. No gameplay differences from the 145 edition.

### TAOM-specific changes

- **Ships both client runtime folders (2026-08-23).** `TAOM` and `TAOM.Dependencies` each ship four
  runtime folders, and Game Pass installs load `Gaming.Desktop.x64_Shipping_Client` — so a port that
  shipped `Win64_Shipping_Client` only would silently fail to load for players TAOM itself supports.
  A post-build MSBuild target now mirrors the DLL and PDB into the Gaming.Desktop folder. Purely
  additive; Win64 users are unaffected. Untested on an actual Game Pass install.

- **Dependencies come from `TAOM.Dependencies`.** `SubModule.xml` declares a single hard
  `<DependedModule Id="TAOM.Dependencies" />` instead of `Bannerlord.Harmony`,
  `Bannerlord.UIExtenderEx` and `Bannerlord.MBOptionScreen`. TAOM load orders install that one
  bundle in place of the three standalone mods, so declaring the standalone modules would make the
  launcher demand mods a TAOM user does not have.
- **Built against TAOM's bundled assemblies** — `Bannerlord.UIExtenderEx.dll` 2.13.2 and
  `MCMv5.dll` 5.12.1 from `Modules/TAOM.Dependencies/bin/Win64_Shipping_Client/`, not the Steam
  Workshop copies (2.13.3 / 5.12.2) the other editions use. Compiling against the older bundled
  versions is the safe direction — the result runs on newer builds too.
- **`TAOM` and `TAOM_Map` are optional load-order metadata only.** Battle Ballista calls no TAOM
  API and works in any battle; they are listed so the launcher sorts them before this module.

### Identity divergence (co-installable with the other editions)

| Surface | Value |
|---|---|
| Module Id | `TAOMBattleBallista` |
| Launcher name | Battle Ballista (TAOM) |
| DLL / namespace | `TAOMBattleBallista` |
| UIExtenderEx registration | `TAOMBattleBallista` |
| MCM settings Id | `TAOMBattleBallista_Settings` |
| Log file | `Modules/TAOMBattleBallista/TAOMBattleBallista.log` |

Every identifier is edition-suffixed, so this module can sit in the same load order as the other
Battle Ballista editions without Id, UIExtender, or settings collisions. (Running two editions at
once still gives you two deploy hotkeys and two map buttons — install only one.)
