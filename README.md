# Battle Ballista

A hotkey-driven siege-engine deployer for Mount & Blade II: Bannerlord.

Press a key in any regular field battle or siege and the currently-selected siege engine spawns
about 4 metres in front of you — terrain-aligned, fully registered with the mission, ready for you
*or any AI soldier* to man it with **F**. A small button on the campaign map cycles which engine
the next press deploys.

No Harmony patches of its own. No save data. Nothing changes unless you press the key.

## Features

- **Deploy engines mid-battle** — Ballista, Catapult, Fire Catapult, Trebuchet
- **Keyboard or gamepad** — default **B**, or **D-pad Right**
- **Configurable via MCM** (1.4.5 edition; all optional) — deploy key, per-kind deploy cap (1–10, default 4), sally-out toggle. Without MCM everything runs on defaults (B / 4 / sally-outs allowed)
- **AI soldiers can use your engines** — they path to them and man them like any scene engine
- **Terrain-aligned placement** — engines tilt to match the local slope
- **Obstacle-aware** — the spawn point slides closer to you if geometry is in the way
- **Per-kind deploy cap** — default 4 per battle, MCM-configurable 1–10 (1.4.5 edition; the 1.3.15 line keeps the fixed 3)
- **Context-aware** — refuses in arenas, tournaments, hideouts, lord's hall fights, indoor and
  storyline scripted scenes (23 blocked mission types as of v1.5.2, including the naval DLC's
  set-pieces), and missions without real terrain. Sally-outs are deployable (toggleable, default on)

See [FEATURES.md](FEATURES.md) for the complete list.

## Which edition do I install?

This branch holds three co-installable editions of the same mod:

| Folder | Game version | Module ID | Status |
|---|---|---|---|
| [`BattleBallista145/`](BattleBallista145/) | **1.4.5 – 1.5.7** | `BattleBallista145` | Actively shipped (v1.5.2) |
| [`TAOMBattleBallista/`](TAOMBattleBallista/) | **TAOM** total conversion | `TAOMBattleBallista` | Actively shipped (v1.3.2) |
| [`BattleBallista/`](BattleBallista/) | **1.3.15** | `BattleBallista` | **Retired** (2026-10-17); final source kept for reference |

Install **one** — whichever matches your game (or your total conversion). The **TAOM** edition is the
same mod built for [TAOM](https://www.nexusmods.com/mountandblade2bannerlord/) load orders: it resolves
Harmony, UIExtenderEx and MCM through the `TAOM.Dependencies` bundle instead of the standalone modules,
so it needs no separate dependency mods. They use different module IDs, DLL names and
namespaces, so having both present causes no conflict.

> **1.3.15 users:** this line is retired. No released 1.3.15 binary ever included the AI-crash
> fix — if an AI mans your engine on the old build, the mission will crash. The final fixed
> source is kept here for reference; building it requires a 1.3.15 install. Prefer the 1.4.5+ line.

## Requirements

- [Harmony](https://www.nexusmods.com/mountandblade2bannerlord/mods/2006) (required by UIExtenderEx)
- [UIExtenderEx](https://www.nexusmods.com/mountandblade2bannerlord/mods/2102) — **required**; the
  campaign-map cycle button is injected through it
- [MCM](https://www.nexusmods.com/mountandblade2bannerlord/mods/612) — *optional*; unlocks the
  deploy-key rebind, the deploy-cap slider and the sally-out toggle (1.4.5 edition)

## Installation

1. Download a release archive (or build from source).
2. Extract so the module folder lands in your game's `Modules/` directory, e.g.
   `Mount & Blade II Bannerlord/Modules/BattleBallista145/`.
3. Enable it in the launcher, **below** Harmony and UIExtenderEx in the load order.

## Usage

| Action | Input |
|---|---|
| Deploy the selected engine | **B** (rebindable via MCM) or **D-pad Right** |
| Man an engine | Walk up, press **F** |
| Change which engine deploys next | Click the **Battle Ballista** button on the campaign map bar |

The cycle runs Ballista → Catapult → Fire Catapult → Trebuchet → back to Ballista, and resets to
Ballista when you restart the game. Each press deploys one complete engine — catapults and
trebuchets bring their own ammo pile; the ballista carries its bolts.

### Configuration (1.4.5 edition)

With MCM installed: **Options → Mod Options → Battle Ballista (1.4.5)** — Deploy Key, Maximum
engines per kind (1–10, default 4) and Allow sally-out deployment (default on). Changes apply
immediately, even mid-battle. Without MCM, the mod runs normally on the defaults.

## Building from source

Requires the .NET SDK and a local Bannerlord installation.

```bash
cd BattleBallista145/src && dotnet build BattleBallista145.csproj -c Release   # 1.4.5 – 1.5.7
# BattleBallista/src (1.3.15) is retired reference source — building it needs a 1.3.15 install
```

The `.csproj` files reference game and dependency assemblies by **absolute path** (`GamePath`,
`UIExtenderExPath`, `MCMPath` properties near the top) — edit those to match your install before
building. Output goes to `<module>/bin/Win64_Shipping_Client/`.

Each edition builds against the assemblies of *its own* game version; building the 1.3.15 line
against a 1.4.x install will not produce a usable DLL.

> **Always ship optimized builds.** Both projects pin `<Optimize>true</Optimize>` and
> `<DebugType>portable</DebugType>`. A Debug / Edit-and-Continue assembly cannot be reliably
> processed by MonoMod/Harmony and will crash the game at startup in a heavily-modded load order —
> while loading fine on a light one. Verify before releasing: the assembly's `Debuggable`
> attribute must read `IgnoreSymbolStoreSequencePoints` only.

## Troubleshooting

**The campaign-map button doesn't appear.** First check whether the deploy hotkey still works:

- *Hotkey works, button missing* — a UI-side problem. Usually an outdated UIExtenderEx, or another
  mod replacing the vanilla `MapBar.xml`. UIExtenderEx reports this in the in-game message feed as
  the campaign map loads; look for a red `UIExtenderEx: Failed to apply extension to MapBar...`.
- *Nothing works at all* — the module didn't load: UIExtenderEx missing entirely, or you're on
  Game Pass (releases ship `bin/Win64_Shipping_Client` only, not `Gaming.Desktop.x64_Shipping_Client`).

**A crash while using an engine.** Please update to the latest release first — older builds had
crash bugs in this exact path. If it still happens, see below.

## Reporting bugs

Please include:

1. **Which edition and version** — the `<Version>` value in the module's `SubModule.xml`
2. **`BattleBallista145.log`** from inside the module folder — it records the mod and game version,
   your full active module list, every deploy, and every time an engine is manned or destroyed.
   This is usually enough to identify an interaction with another mod.
3. What you were doing: mission type, engine kind, and whether you or an AI was operating it.

## License

[MIT](LICENSE) © 2026 SlopStudios
