# Battle Ballista (TAOM) — Changelog

The **TAOM** edition of Battle Ballista, shipped as a separate module (`TAOMBattleBallista`)
alongside the standalone `BattleBallista` (1.3.15) and `BattleBallista145` (1.4.5–1.4.7) editions.
Same mod, same features — forked so it resolves its dependencies through **TAOM.Dependencies**
instead of the standalone Harmony / UIExtenderEx / MCM modules. Install into
`Modules/TAOMBattleBallista/`.

## v1.0.0-TAOM (2026-07-20) — Initial TAOM edition

Forked from `BattleBallista145` v1.2.3, which is feature-current: MCM-configurable deploy hotkey,
the mod-compatible deployment hook, crash-forensics logging, sally-out blocking and the full
mid-battle threat-seeker fix. No gameplay differences from the 145 edition.

### TAOM-specific changes

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
