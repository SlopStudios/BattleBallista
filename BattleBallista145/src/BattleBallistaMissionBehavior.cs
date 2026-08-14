using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BattleBallista145
{
    /// Hotkey-driven siege deployer. Press the deploy key (keyboard,
    /// default B, configurable via MCM) or D-pad right (gamepad) in any
    /// mission with the player agent alive — the engine
    /// currently selected on SiegeCycle instantiates ~4m in front of the
    /// player at the correct terrain height, tilted to match the local
    /// slope, registered with the mission so the player can mount it via F.
    /// Each press = one full deployment. Mangonel / Fire Mangonel /
    /// Trebuchet prefabs already bundle their own matching ammo pile
    /// (stone / pot / stone respectively); Ballista carries its bolts on
    /// the engine itself. Cap is 3 per kind per battle.
    public class BattleBallista145MissionBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        // Keyboard key comes from DeployHotkey (MCM-configurable, default B,
        // falls back to B when MCM is absent). Gamepad binding is fixed.
        private const InputKey SpawnKeyGamepad = InputKey.ControllerLRight;

        // Direct prefab IDs (not _spawner). Spawner prefabs make
        // CreateMissionObjectFromPrefab return null because the spawner
        // ScriptComponent isn't a MissionObject — but the call still
        // instantiates the spawner, which spawns two untracked child
        // entities (normal + _fire). Using the direct IDs from SiegeCycle
        // avoids that phantom stack. Names match the convention in
        // SpawnerEntityMissionHelper.GetPrefabName (strip _spawner suffix,
        // append _fire for the fire version).

        // Mission types where deploying a siege engine would be nonsensical
        // or game-state-breaking: tournaments, arena practice, duels,
        // hideout ambushes, lord's hall fights, indoor scripted scenes,
        // stealth sabotage. Matched by behavior class name so we don't pull
        // in a SandBox assembly reference. Mission.Mode == Battle alone
        // isn't enough — arena and tournament rounds also use MissionMode.Battle.
        private static readonly string[] BlockedMissionBehaviorNames = new[]
        {
            "ArenaPracticeFightMissionController",
            "ArenaDuelMissionController",
            "TournamentFightMissionController",
            "HideoutMissionController",
            "HideoutAmbushMissionController",
            "LordsHallFightMissionController",
            "HouseMissionController",
            "IndoorMissionController",
            "StealthMissionController",
            "SabotageMissionController",
        };

        private const float SpawnDistance = 4f;
        private const float TerrainLiftEpsilon = 0.05f;
        private const float MaxTiltCosine = 0.7f;
        private const int MaxEnginesPerKindPerBattle = 3;
        private const float MinTerrainHeight = -10000f;
        private const float MaxTerrainHeight = 10000f;

        private bool _wasKeyDown;

        // Per-kind press count for the current battle. For Ballista each
        // press spawns an engine (cap 3 = 3 engines). For others, presses
        // alternate engine/ammo (cap 6 = 3 engines + 3 piles).
        private readonly Dictionary<SiegeKind, int> _pressCount = new Dictionary<SiegeKind, int>();

        // Crash-forensics breadcrumbs (v1.2.1). We can't catch exceptions in
        // vanilla weapon/AI ticks (no Harmony by policy), so instead we log
        // every deploy and every pilot mount/dismount/destruction on OUR
        // spawned engines. A user crash "while using" an engine then leaves a
        // last-events trail in BattleBallista145.log to correlate against.
        private readonly List<RangedSiegeWeapon> _trackedWeapons = new List<RangedSiegeWeapon>();
        private readonly Dictionary<RangedSiegeWeapon, string> _trackedLabels = new Dictionary<RangedSiegeWeapon, string>();
        private readonly Dictionary<RangedSiegeWeapon, Agent> _lastPilot = new Dictionary<RangedSiegeWeapon, Agent>();
        private readonly HashSet<RangedSiegeWeapon> _loggedDestroyed = new HashSet<RangedSiegeWeapon>();

        private void TrackPilotChanges()
        {
            for (int i = _trackedWeapons.Count - 1; i >= 0; i--)
            {
                RangedSiegeWeapon weapon = _trackedWeapons[i];
                try
                {
                    string label = _trackedLabels[weapon];

                    if (!weapon.GameEntity.IsValid)
                    {
                        Log(label + ": entity no longer valid, untracking.");
                        _trackedWeapons.RemoveAt(i);
                        continue;
                    }

                    if (weapon.DestructionComponent != null && weapon.DestructionComponent.IsDestroyed
                        && _loggedDestroyed.Add(weapon))
                    {
                        Log(label + ": DESTROYED.");
                    }

                    Agent pilot = weapon.PilotAgent;
                    _lastPilot.TryGetValue(weapon, out Agent previous);
                    if (!ReferenceEquals(pilot, previous))
                    {
                        _lastPilot[weapon] = pilot;
                        if (pilot != null)
                        {
                            Log(label + ": mounted by " + DescribeAgent(pilot) + ".");
                        }
                        else
                        {
                            Log(label + ": pilot dismounted (was " + DescribeAgent(previous) + ").");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // A weapon whose state can't even be read is a weapon we
                    // want OUT of the loop — log once and drop it.
                    Log("Tracking failed on a spawned weapon (untracking): " + ex);
                    _trackedWeapons.RemoveAt(i);
                }
            }
        }

        private static string DescribeAgent(Agent agent)
        {
            if (agent == null) return "nobody";
            try
            {
                string side = agent.Team != null ? agent.Team.Side.ToString() : "no-team";
                string control = agent.IsAIControlled ? "AI" : "player-controlled";
                return "'" + agent.Name + "' (" + control + ", " + side + ")";
            }
            catch
            {
                return "<unreadable agent>";
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            try
            {
                if (Mission.Current == null) return;
                if (Mission.Current.CurrentState != Mission.State.Continuing) return;

                // Silent-skip outside Battle missions. Without this, typing the
                // letter B in any text input (AI chat, text inquiry) trips the
                // raw key poll below and surfaces a yellow "only deployable in
                // battles" toast over the chat. Reset rising-edge state so a
                // held key doesn't false-trigger on the next Battle mission.
                if (Mission.Current.Mode != MissionMode.Battle)
                {
                    _wasKeyDown = false;
                    return;
                }

                // Breadcrumb tracking runs even while the player is down —
                // AI keeps manning engines after the player falls, and that's
                // exactly the window the forensics exist for.
                if (_trackedWeapons.Count > 0)
                {
                    TrackPilotChanges();
                }

                var player = Agent.Main;
                if (player == null || !player.IsActive()) return;

                bool isKeyDown = Input.IsKeyDown(DeployHotkey.Current) || Input.IsKeyDown(SpawnKeyGamepad);
                if (isKeyDown && !_wasKeyDown)
                {
                    TrySpawnSiege(player);
                }
                _wasKeyDown = isKeyDown;
            }
            catch (Exception ex)
            {
                Log("OnMissionTick exception: " + ex);
                Toast("Battle Ballista tick error: " + ex.Message, Colors.Red);
            }
        }

        private void TrySpawnSiege(Agent player)
        {
            try
            {
                if (Mission.Current.Mode != MissionMode.Battle || HasBlockedMissionBehavior())
                {
                    Toast("Battle Ballista: only deployable in regular battles and sieges.", Colors.Yellow);
                    return;
                }

                SiegeKind kind = SiegeCycle.Current;
                int press;
                _pressCount.TryGetValue(kind, out press);

                if (press >= MaxEnginesPerKindPerBattle)
                {
                    Toast("Battle Ballista: " + SiegeCycle.CurrentLabel + " limit reached (" +
                        MaxEnginesPerKindPerBattle + " per battle).", Colors.Yellow);
                    return;
                }

                // Each press spawns one full deployment — the engine prefab
                // already bundles its own ammo pile (mangonel_a → stone pile,
                // mangonel_a_fire → pot pile, trebuchet_a → stone pile);
                // ballista_a carries its bolts on the engine. No alternation.
                string prefab = SiegeCycle.EnginePrefab;

                var scene = Mission.Current.Scene;
                if (scene == null) { Toast("No scene available.", Colors.Red); return; }

                Vec3 lookDir = player.LookDirection;
                lookDir.z = 0f;
                if (lookDir.LengthSquared < 0.0001f)
                {
                    var fwd2 = player.GetMovementDirection();
                    lookDir = new Vec3(fwd2.x, fwd2.y, 0f);
                }
                lookDir.Normalize();

                Vec3 playerPos = player.Position;
                Vec2 targetXY = new Vec2(
                    playerPos.x + lookDir.x * SpawnDistance,
                    playerPos.y + lookDir.y * SpawnDistance);

                // Slide the spawn point closer if there's geometry between the
                // player and the target — prevents spawning inside walls/props.
                targetXY = ClampSpawnAgainstObstacles(scene, playerPos, targetXY);

                scene.GetTerrainHeightAndNormal(targetXY, out float terrainHeight, out Vec3 terrainNormal);

                // Refuse to spawn on missions without real terrain (sea
                // battles, ship interiors). A bizarre height value indicates
                // the query hit nothing meaningful; bail rather than place
                // under the map or in the sky.
                if (float.IsNaN(terrainHeight) || float.IsInfinity(terrainHeight)
                    || terrainHeight < MinTerrainHeight || terrainHeight > MaxTerrainHeight)
                {
                    Toast("Battle Ballista: this mission has no terrain to plant a ballista on.", Colors.Yellow);
                    return;
                }

                if (terrainNormal.LengthSquared < 0.0001f || terrainNormal.z < MaxTiltCosine)
                    terrainNormal = new Vec3(0f, 0f, 1f);
                else
                    terrainNormal.Normalize();

                Vec3 origin = new Vec3(targetXY.x, targetXY.y, terrainHeight + TerrainLiftEpsilon);

                // Engine prefabs (ballista_a etc.) put the operator stand at
                // body local +Y and the firing axis at local -Y. Pointing
                // frame.rotation.f at -lookDir makes projectiles fly in the
                // player's look direction. Stone piles ignore facing — but
                // the same frame is harmless to use.
                MatrixFrame frame = BuildAlignedFrame(origin, terrainNormal, -lookDir);

                // Capture the spawned ROOT entity. CreateMissionObjectFromPrefab returns
                // only the root's first MissionObject script — which for mangonel_a /
                // trebuchet_a is a SynchedMissionObject wrapper, NOT the weapon. The
                // weapon script lives on a child entity there, so we need the root entity
                // itself to walk the tree and find it (see the threat-seeker init below).
                GameEntity spawnedRoot = null;
                MissionObject mo;
                try
                {
                    // 1.4.5 signature: (string prefab, MatrixFrame frame, bool hasCustomRestOffset,
                    // float restOffset, Action<GameEntity>). hasCustomRestOffset:false keeps the prefab's
                    // default rest offset (restOffset ignored), matching the pre-1.4.5 behavior.
                    // The callback fires before script registration, so we only stash the
                    // entity reference here and traverse it after the call returns.
                    mo = Mission.Current.CreateMissionObjectFromPrefab(
                        prefab, frame, false, 0f, e => { spawnedRoot = e; });
                }
                catch (Exception inner)
                {
                    Toast("Battle Ballista: spawn threw " + inner.GetType().Name + ".", Colors.Red);
                    Log("CreateMissionObjectFromPrefab threw: " + inner);
                    return;
                }

                if (mo == null)
                {
                    Toast("Battle Ballista: prefab '" + prefab + "' did not spawn.", Colors.Red);
                    return;
                }

                // Mid-battle ThreatSeeker init — required for AI agents to operate the
                // engine without crashing. Vanilla RangedSiegeWeapon.OnDeploymentFinished()
                // calls (Ai as RangedSiegeWeaponAi).InitializeThreatSeeker() exactly once
                // at the end of the deployment phase, which populates
                // ThreatSeeker._potentialTargetObjects and _referencePositions (both null
                // until then). Engines spawned mid-battle (here, via
                // CreateMissionObjectFromPrefab) miss that hook, so both lists stay null;
                // the moment any AI agent mounts the engine, RangedSiegeWeaponAi.OnTick →
                // FindNextTarget → ThreatSeeker.PrepareThreatSeekerTask → GetAllThreats
                // dereferences the null _potentialTargetObjects.Count and the mission NREs.
                //
                // CRITICAL — why the v1.1.3 fix didn't work: CreateMissionObjectFromPrefab
                // returns only the ROOT entity's first MissionObject script. Only ballista_a
                // carries its weapon (Ballista) on the root; mangonel_a and trebuchet_a put
                // a SynchedMissionObject wrapper on the root and the actual Mangonel /
                // Trebuchet weapon script on a CHILD entity. So "mo is RangedSiegeWeapon"
                // matched ballistas but silently skipped catapults and trebuchets — those
                // still crashed. The robust fix is to ignore the returned MissionObject's
                // type and walk the whole spawned entity tree, firing the deployment
                // hook on EVERY SiegeWeapon found (wherever it sits in the hierarchy).
                // Idempotent and safe on non-weapon prefabs (the recursion just finds
                // nothing). Since v1.2.2 the walk calls weapon.OnDeploymentFinished()
                // — see InitializeSiegeWeaponAi below for the mod-compat rationale.
                try
                {
                    // spawnedRoot is set by the spawn callback above, which
                    // CreateMissionObjectFromPrefab always fires with the root entity
                    // before returning a non-null mo. The null-guard in the helper covers
                    // the theoretical case where it didn't.
                    InitializeSiegeWeaponAi(spawnedRoot);
                }
                catch (Exception initEx)
                {
                    Log("Siege AI init walk failed: " + initEx);
                }

                _pressCount[kind] = press + 1;

                // Forensics: register every RangedSiegeWeapon in the spawned tree
                // for pilot/destruction breadcrumb logging (see TrackPilotChanges),
                // and record the deploy itself.
                try
                {
                    string deployLabel = SiegeCycle.CurrentLabel + " #" + (press + 1) + " (" + prefab + ")";
                    RegisterWeaponsForTracking(spawnedRoot, deployLabel);
                    Log("Deployed " + deployLabel + " at " + origin.ToString() +
                        ", scene '" + (scene.GetName() ?? "?") + "'.");
                }
                catch (Exception trackEx)
                {
                    Log("Deploy tracking registration failed: " + trackEx);
                }

                Toast(SiegeCycle.CurrentLabel + " deployed (" + (press + 1) + "/" +
                    MaxEnginesPerKindPerBattle + "). Walk up and press F to use.", Colors.Green);
            }
            catch (Exception ex)
            {
                Log("TrySpawnSiege exception: " + ex);
                Toast("Battle Ballista spawn error: " + ex.Message, Colors.Red);
            }
        }

        // Walk the spawned entity tree and register every RangedSiegeWeapon for
        // breadcrumb tracking. Mirrors the InitializeSiegeWeaponAi walk (weapon
        // scripts can sit on the root OR a child entity).
        private void RegisterWeaponsForTracking(GameEntity entity, string deployLabel)
        {
            if (entity == null) return;

            foreach (RangedSiegeWeapon weapon in entity.GetScriptComponents<RangedSiegeWeapon>())
            {
                if (!_trackedLabels.ContainsKey(weapon))
                {
                    _trackedWeapons.Add(weapon);
                    _trackedLabels[weapon] = deployLabel + " [" + weapon.GetType().Name + "]";
                    _lastPilot[weapon] = null;
                }
            }

            foreach (GameEntity child in entity.GetChildren())
                RegisterWeaponsForTracking(child, deployLabel);
        }

        private static bool HasBlockedMissionBehavior()
        {
            var behaviors = Mission.Current.MissionBehaviors;
            for (int i = 0; i < behaviors.Count; i++)
            {
                string name = behaviors[i].GetType().Name;

                // Sally-out battles (defenders sallying out of a besieged settlement)
                // are MissionMode.Battle but run on a siege-derived scene that the
                // RangedSiegeWeapon AI / ThreatSeeker does NOT handle — deploying an
                // engine there produced a native AccessViolationException in
                // MissionState.TickMission (crash id 639f8615; reported "during a siege,
                // enemy sallied out"). Block every sally-out controller variant
                // (vanilla SallyOutMissionController / SandBoxSallyOutMissionController /
                // CustomSallyOutMissionController, plus any mod-renamed one) by substring.
                if (name.IndexOf("SallyOut", StringComparison.OrdinalIgnoreCase) >= 0) return true;

                for (int j = 0; j < BlockedMissionBehaviorNames.Length; j++)
                {
                    if (name == BlockedMissionBehaviorNames[j]) return true;
                }
            }
            return false;
        }

        // Recursively fire the deployment-finished hook on every SiegeWeapon in the
        // spawned entity tree. Walks the whole hierarchy because the weapon script can
        // sit on the root (ballista_a) OR on a child entity (mangonel_a's Mangonel lives
        // under mangonel_a_body; trebuchet_a's Trebuchet under a child too).
        //
        // v1.2.2 MOD-COMPAT CHANGE — call the weapon's PUBLIC OnDeploymentFinished()
        // instead of reaching into Ai.InitializeThreatSeeker() directly. Vanilla-
        // identical: TeamAISiegeComponent.OnDeploymentFinished() calls exactly this,
        // per SiegeWeapon, at the end of a siege's deployment phase, and for ranged
        // weapons the override's entire body is base (empty, verified through the
        // whole MissionObject chain on 1.4.7) + (Ai as RangedSiegeWeaponAi)
        // .InitializeThreatSeeker() — .Ai lazily creates the concrete BallistaAI/
        // MangonelAI/TrebuchetAI. The difference is THIRD-PARTY HOOKS: combat/siege
        // overhaul mods Harmony-patch OnDeploymentFinished to attach their own
        // per-weapon state (custom projectile physics, damage models, AI swaps).
        // Calling the real hook runs their initialization for mid-battle spawns too;
        // bypassing it (v1.1.4–v1.2.1) left such mods' usage-path patches reading
        // state that was never created — an unhandled exception the moment anyone
        // used the engine, only in load orders running that mod. Walking SiegeWeapon
        // (not just RangedSiegeWeapon) matches vanilla's SceneSiegeWeapons loop.
        //
        // Per-weapon try/catch so one bad weapon — or one throwing third-party
        // patch — can't abort the rest of the tree; failures are logged.
        private static void InitializeSiegeWeaponAi(GameEntity entity)
        {
            if (entity == null) return;

            foreach (SiegeWeapon weapon in entity.GetScriptComponents<SiegeWeapon>())
            {
                try
                {
                    weapon.OnDeploymentFinished();
                }
                catch (Exception ex)
                {
                    Log("OnDeploymentFinished failed on a spawned weapon (" +
                        weapon.GetType().Name + "): " + ex);
                }
            }

            foreach (GameEntity child in entity.GetChildren())
                InitializeSiegeWeaponAi(child);
        }

        // Cast a short ray from the player's chest height to the target's chest
        // height. If geometry intercepts, slide the target closer to the hit
        // point so we don't spawn the ballista inside it.
        private static Vec2 ClampSpawnAgainstObstacles(Scene scene, Vec3 playerPos, Vec2 targetXY)
        {
            try
            {
                Vec3 from = new Vec3(playerPos.x, playerPos.y, playerPos.z + 1.0f);
                Vec3 to = new Vec3(targetXY.x, targetXY.y, playerPos.z + 1.0f);
                float collisionDistance = 0f;
                if (scene.RayCastForClosestEntityOrTerrain(from, to, out collisionDistance, 0.05f, BodyFlags.CommonCollisionExcludeFlags))
                {
                    Vec3 fwd = (to - from);
                    float full = fwd.Length;
                    if (full > 0.5f && collisionDistance > 0f && collisionDistance < full)
                    {
                        // Stop ~0.5m before the obstacle.
                        float clamped = Math.Max(0.5f, collisionDistance - 0.5f);
                        fwd.Normalize();
                        Vec3 stop = from + fwd * clamped;
                        return new Vec2(stop.x, stop.y);
                    }
                }
            }
            catch { /* If the raycast API misbehaves, fall back to the unclipped target. */ }
            return targetXY;
        }

        private static MatrixFrame BuildAlignedFrame(Vec3 origin, Vec3 up, Vec3 desiredForward)
        {
            Vec3 fwd = desiredForward - up * Vec3.DotProduct(desiredForward, up);
            if (fwd.LengthSquared < 0.0001f)
                fwd = new Vec3(1f, 0f, 0f);
            fwd.Normalize();

            // Right-handed: side = forward × up. Reversed (up × forward) gives
            // a reflected matrix; downstream cross-product fixups in
            // RangedSiegeWeapon.HandleAiming then propagate the flipped sign
            // and the mounted weapon-camera renders inside-out.
            Vec3 side = Vec3.CrossProduct(fwd, up);
            side.Normalize();

            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin = origin;
            frame.rotation.s = side;
            frame.rotation.f = fwd;
            frame.rotation.u = up;
            return frame;
        }

        // CRITICAL: don't initialize the log path in a static field initializer.
        // BasePath.Name can fire before the engine populates it, throwing
        // TypeInitializationException at assembly load — which manifests as the
        // BLSE "submodule failed to load" crash. Compute lazily and cache.
        private static string _cachedLogPath;

        private static string GetLogPath()
        {
            if (_cachedLogPath != null) return _cachedLogPath;
            try
            {
                _cachedLogPath = System.IO.Path.Combine(
                    BasePath.Name, "Modules", "BattleBallista145", "BattleBallista145.log");
            }
            catch
            {
                _cachedLogPath = "BattleBallista145.log";
            }
            return _cachedLogPath;
        }

        // Keep in sync with SubModule.xml <Version> on every release.
        private const string ModVersion = "v1.2.3";

        private static bool _sessionHeaderWritten;

        // One-time diagnostic header so a user-supplied log identifies the exact
        // environment without any back-and-forth: our version, the game version,
        // and — because crash reports keep pointing at load-order interactions we
        // can't reproduce — the full list of active modules. Pure logging, no
        // gameplay surface; every part is individually guarded.
        private static void WriteSessionHeaderOnce()
        {
            if (_sessionHeaderWritten) return;
            _sessionHeaderWritten = true;
            try
            {
                string gameVersion = "?";
                try { gameVersion = Utilities.GetApplicationVersionWithBuildNumber().ToString(); }
                catch { }

                string modules = "?";
                try { modules = string.Join(", ", Utilities.GetModulesNames()); }
                catch { }

                File.AppendAllText(GetLogPath(),
                    "=== Battle Ballista (1.4.5) " + ModVersion +
                    " | game " + gameVersion +
                    " | session " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===\n" +
                    "Active modules: " + modules + "\n");
            }
            catch { }
        }

        private static void Log(string message)
        {
            try
            {
                WriteSessionHeaderOnce();
                File.AppendAllText(GetLogPath(),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message + "\n");
            }
            catch { }
        }

        private static void Toast(string message, Color color)
        {
            try { InformationManager.DisplayMessage(new InformationMessage(message, color)); }
            catch { }
        }
    }
}
