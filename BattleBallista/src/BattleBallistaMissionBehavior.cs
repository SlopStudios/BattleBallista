using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BattleBallista
{
    /// Hotkey-driven siege deployer. Press B (keyboard) or D-pad right
    /// (gamepad) in any mission with the player agent alive — the engine
    /// currently selected on SiegeCycle instantiates ~4m in front of the
    /// player at the correct terrain height, tilted to match the local
    /// slope, registered with the mission so the player can mount it via F.
    /// Each press = one full deployment. Mangonel / Fire Mangonel /
    /// Trebuchet prefabs already bundle their own matching ammo pile
    /// (stone / pot / stone respectively); Ballista carries its bolts on
    /// the engine itself. Cap is 3 per kind per battle.
    public class BattleBallistaMissionBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private const InputKey SpawnKeyKeyboard = InputKey.B;
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

        // Surface probing. We look for the surface the PLAYER is standing on, not the
        // terrain: on a castle wall the terrain is the courtyard far below. The probe
        // starts a little above the player head so it begins inside the same space he
        // occupies (starting higher risks catching a roof or arch above), and reaches a
        // few metres down so a step or short drop still counts.
        private const float SurfaceProbeAbove = 1.8f;
        private const float SurfaceProbeBelow = 3.0f;
        private const float MinSpawnDistance = 1.2f;
        private const float TripodRadius = 0.6f;
        // A hit may be at most this far ABOVE the player's feet to count as the surface
        // he is standing on. The probe RAY starts higher (SurfaceProbeAbove) so it clears
        // waist-high clutter, but accepting hits that high would perch engines on merlons,
        // crates, or the chassis of an already-deployed engine. A step up is fine; a
        // parapet up is not.
        private const float MaxStepUp = 0.5f;
        // How close a probe hit must be to the terrain height to count AS terrain.
        private const float TerrainMatchTolerance = 0.15f;
        // If no surface is found ahead, how far below the player terrain may still be
        // before we refuse rather than dropping the engine off an edge.
        private const float MaxTerrainFallbackDrop = 3.0f;
        // Walk the spot back toward the player until a surface at his level appears.
        private static readonly float[] SurfaceRetryFractions = { 1f, 0.75f, 0.5f, 0.3f };
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

                var player = Agent.Main;
                if (player == null || !player.IsActive()) return;

                bool isKeyDown = Input.IsKeyDown(SpawnKeyKeyboard) || Input.IsKeyDown(SpawnKeyGamepad);
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

                // ---- Surface resolution -----------------------------------------------
                // Previously this asked ONLY GetTerrainHeightAndNormal, which returns the
                // TERRAIN height. Walls, wooden walkways, siege-tower tops and ship decks
                // are static mesh entities, NOT terrain -- so deploying while standing on a
                // castle wall put the engine on the courtyard floor metres below (user
                // report, 2026-08-23). Instead probe DOWNWARD from the player head height:
                // the first solid thing under that ray is the surface he is actually
                // standing on, wall or ground alike.
                //
                // Deliberately NOT Scene.GetGroundHeightAtPosition(pos, out normal, ...):
                // verified in the 1.4.8 decompile that this overload sets
                // normal = Vec3.Invalid and never assigns it, so trusting its normal would
                // silently build a garbage frame.
                float playerZ = playerPos.z;
                Vec2 playerXY = new Vec2(playerPos.x, playerPos.y);
                Vec2 lookXY = new Vec2(lookDir.x, lookDir.y);

                // Walk the spot back toward the player until a surface at roughly his own
                // level appears. This is what stops an engine sailing off the battlements
                // when you deploy while looking out over the edge -- it lands on the walkway
                // behind you instead of the courtyard below.
                float reach = (targetXY - playerXY).Length;
                bool haveSurface = false;
                bool surfaceIsTerrain = false;
                float surfaceZ = 0f;
                float previousD = -1f;
                for (int i = 0; i < SurfaceRetryFractions.Length; i++)
                {
                    float d = reach * SurfaceRetryFractions[i];
                    if (d < MinSpawnDistance) d = MinSpawnDistance;
                    // Never probe beyond the obstacle-clamped reach: when a wall is
                    // directly ahead the clamp pulled reach below MinSpawnDistance, and
                    // raising d back up would probe inside/behind that wall.
                    if (d > reach) d = reach;
                    // Clamping can collapse several fractions onto the same distance -
                    // don't repeat an identical probe.
                    if (Math.Abs(d - previousD) < 0.01f) continue;
                    previousD = d;
                    Vec2 candidate = new Vec2(playerXY.x + lookXY.x * d, playerXY.y + lookXY.y * d);
                    if (TryProbeSurface(scene, candidate, playerZ, out float probedZ, out bool probedTerrain))
                    {
                        targetXY = candidate;
                        surfaceZ = probedZ;
                        surfaceIsTerrain = probedTerrain;
                        haveSurface = true;
                        break;
                    }
                }

                scene.GetTerrainHeightAndNormal(targetXY, out float terrainHeight, out Vec3 terrainNormal);

                // Refuse to spawn on missions without real terrain (sea
                // battles, ship interiors). A bizarre height value indicates
                // the query hit nothing meaningful; bail rather than place
                // under the map or in the sky. Kept as the FIRST gate so naval
                // missions behave exactly as they did before this change.
                if (float.IsNaN(terrainHeight) || float.IsInfinity(terrainHeight)
                    || terrainHeight < MinTerrainHeight || terrainHeight > MaxTerrainHeight)
                {
                    Toast("Battle Ballista: this mission has no terrain to plant a ballista on.", Colors.Yellow);
                    return;
                }

                if (!haveSurface)
                {
                    // Nothing solid within the probe band. Falling back to terrain is fine
                    // when the terrain is plausibly underfoot (open field, gentle slope);
                    // but if it is far below, the player is up on something with only air
                    // ahead -- dropping the engine down there is the very bug being fixed.
                    if (playerZ - terrainHeight <= MaxTerrainFallbackDrop)
                    {
                        surfaceZ = terrainHeight;
                        surfaceIsTerrain = true;
                    }
                    else
                    {
                        Toast("Battle Ballista: nothing solid in front of you to place on - step back from the edge.", Colors.Yellow);
                        return;
                    }
                }

                // Terrain keeps the existing slope alignment, so field battles are
                // unchanged. A non-terrain surface gives us no normal, so derive one from a
                // tripod of downward probes: flat walkways come out level, ramps sloped, and
                // anything implausible is rejected by the tilt guard just below.
                Vec3 surfaceNormal = surfaceIsTerrain
                    ? terrainNormal
                    : ProbeSurfaceNormal(scene, targetXY, playerZ);

                if (surfaceNormal.LengthSquared < 0.0001f || surfaceNormal.z < MaxTiltCosine)
                    surfaceNormal = new Vec3(0f, 0f, 1f);
                else
                    surfaceNormal.Normalize();

                Vec3 origin = new Vec3(targetXY.x, targetXY.y, surfaceZ + TerrainLiftEpsilon);

                // Engine prefabs (ballista_a etc.) put the operator stand at
                // body local +Y and the firing axis at local -Y. Pointing
                // frame.rotation.f at -lookDir makes projectiles fly in the
                // player's look direction. Stone piles ignore facing — but
                // the same frame is harmless to use.
                MatrixFrame frame = BuildAlignedFrame(origin, surfaceNormal, -lookDir);

                // Capture the spawned ROOT entity. CreateMissionObjectFromPrefab returns
                // only the root's first MissionObject script — which for mangonel_a /
                // trebuchet_a is a SynchedMissionObject wrapper, NOT the weapon (that
                // sits on a child entity). We need the root to walk the tree below.
                GameEntity spawnedRoot = null;
                MissionObject mo;
                try
                {
                    // v1.3.15 signature: (string, MatrixFrame, Action<GameEntity>).
                    // The callback fires before script registration, so we only stash the
                    // entity reference here and traverse it after the call returns.
                    mo = Mission.Current.CreateMissionObjectFromPrefab(
                        prefab, frame, e => { spawnedRoot = e; });
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
                // FindNextTarget → ThreatSeeker.GetAllThreats dereferences the null
                // _potentialTargetObjects.Count and the mission NREs out.
                //
                // CRITICAL — why pattern-matching `mo` was wrong: CreateMissionObjectFrom
                // Prefab returns only the ROOT entity's first MissionObject script. Only
                // ballista_a carries its weapon (Ballista) on the root; mangonel_a and
                // trebuchet_a put a SynchedMissionObject wrapper on the root and the actual
                // Mangonel / Trebuchet weapon script on a CHILD entity. So "mo is
                // RangedSiegeWeapon" caught ballistas but silently skipped catapults and
                // trebuchets — those still crashed. Walk the whole spawned entity tree and
                // initialize EVERY RangedSiegeWeapon, wherever it sits. See
                // kit/post-spawn-engine-init.md.
                try
                {
                    // spawnedRoot is set by the spawn callback above, which
                    // CreateMissionObjectFromPrefab always fires with the root entity
                    // before returning a non-null mo.
                    InitializeSiegeWeaponAi(spawnedRoot);
                }
                catch (Exception initEx)
                {
                    Log("Siege AI init walk failed: " + initEx);
                }

                _pressCount[kind] = press + 1;
                Toast(SiegeCycle.CurrentLabel + " deployed (" + (press + 1) + "/" +
                    MaxEnginesPerKindPerBattle + "). Walk up and press F to use.", Colors.Green);
            }
            catch (Exception ex)
            {
                Log("TrySpawnSiege exception: " + ex);
                Toast("Battle Ballista spawn error: " + ex.Message, Colors.Red);
            }
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
                // MissionState.TickMission on the 145 line (crash id 639f8615). Block
                // every sally-out controller variant (SallyOutMissionController /
                // SandBoxSallyOutMissionController / CustomSallyOutMissionController,
                // plus any mod-renamed one) by substring. Ported from BB145 v1.1.6.
                if (name.IndexOf("SallyOut", StringComparison.OrdinalIgnoreCase) >= 0) return true;

                for (int j = 0; j < BlockedMissionBehaviorNames.Length; j++)
                {
                    if (name == BlockedMissionBehaviorNames[j]) return true;
                }
            }
            return false;
        }

        // Recursively initialize the ThreatSeeker on every RangedSiegeWeapon in the
        // spawned entity tree. Walks the whole hierarchy because the weapon script can
        // sit on the root (ballista_a) OR on a child entity (mangonel_a's Mangonel lives
        // under mangonel_a_body; trebuchet_a's Trebuchet under a child too). Accessing
        // .Ai lazily creates the concrete BallistaAI/MangonelAI/TrebuchetAI (all
        // RangedSiegeWeaponAi subclasses), then InitializeThreatSeeker() populates the
        // target lists exactly as vanilla's OnDeploymentFinished would. Per-weapon
        // try/catch so one bad weapon can't abort the rest of the tree.
        private static void InitializeSiegeWeaponAi(GameEntity entity)
        {
            if (entity == null) return;

            foreach (SiegeWeapon weapon in entity.GetScriptComponents<SiegeWeapon>())
            {
                try
                {
                    // MOD COMPAT (ported from BB145 v1.2.2): fire the weapon's public
                    // OnDeploymentFinished() — vanilla-identical (TeamAISiegeComponent
                    // calls exactly this per scene SiegeWeapon at deployment end; for
                    // ranged weapons the body is just the ThreatSeeker init) — but it
                    // also runs any third-party Harmony patches on the hook, so combat
                    // overhaul mods get to attach their per-weapon state to mid-battle
                    // spawns like they do to vanilla engines. Base-chain emptiness was
                    // decompile-verified on 1.4.5/1.4.7 but NOT on 1.3.15 game DLLs
                    // (none on hand) — the per-weapon try/catch contains any surprise.
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

        // Probe straight down for the first solid surface near the player own level.
        // Returns its height and whether it was TERRAIN (entity handle invalid) rather
        // than a mesh such as a wall walkway, so the caller can pick the right normal
        // source. The band is deliberately narrow: it must not reach down to the
        // courtyard when the player is up on the battlements.
        private static bool TryProbeSurface(Scene scene, Vec2 xy, float referenceZ,
            out float surfaceZ, out bool isTerrain)
        {
            surfaceZ = 0f;
            isTerrain = false;
            try
            {
                Vec3 from = new Vec3(xy.x, xy.y, referenceZ + SurfaceProbeAbove);
                Vec3 to = new Vec3(xy.x, xy.y, referenceZ - SurfaceProbeBelow);
                if (scene.RayCastForClosestEntityOrTerrain(from, to, out float _, out Vec3 hit,
                        0.01f, BodyFlags.CommonCollisionExcludeFlags))
                {
                    surfaceZ = hit.z;

                    // Reject hits meaningfully ABOVE the player's feet: that is a parapet,
                    // a crate, or an already-deployed engine - not the surface underfoot.
                    // Failing here lets the retry loop step closer and find the walkway.
                    if (surfaceZ > referenceZ + MaxStepUp)
                        return false;

                    // Terrain or mesh? Deliberately decided by comparing the hit height to
                    // the terrain height rather than by the entity handle: the overload that
                    // hands back the hit entity uses WeakGameEntity on 1.4.x, and whether
                    // that type exists on 1.3.15 is unverified (no DLLs on hand). Heights
                    // are available in every version, so all three editions share this code.
                    scene.GetTerrainHeightAndNormal(xy, out float terrainAtProbe, out Vec3 _);
                    isTerrain = !float.IsNaN(terrainAtProbe) && !float.IsInfinity(terrainAtProbe)
                                && Math.Abs(surfaceZ - terrainAtProbe) <= TerrainMatchTolerance;
                    return true;
                }
            }
            catch (Exception ex)
            {
                // If the raycast API misbehaves, report no surface; the caller falls back
                // to terrain exactly as the pre-probe code did.
                Log("Surface probe failed: " + ex);
            }
            return false;
        }

        // Derive a surface normal for non-terrain surfaces (walls, platforms, decks) by
        // probing three points around the spot and taking the plane through them. The
        // engine exposes no normal for mesh raycasts, and the GetGroundHeightAtPosition
        // overload that appears to return one never actually assigns it. Any degenerate
        // or implausible result falls back to world-up, and the caller tilt-guards it.
        private static Vec3 ProbeSurfaceNormal(Scene scene, Vec2 xy, float referenceZ)
        {
            Vec3 up = new Vec3(0f, 0f, 1f);
            try
            {
                Vec3[] pts = new Vec3[3];
                for (int i = 0; i < 3; i++)
                {
                    double angle = i * 2.0 * Math.PI / 3.0;
                    Vec2 offset = new Vec2(
                        xy.x + TripodRadius * (float)Math.Cos(angle),
                        xy.y + TripodRadius * (float)Math.Sin(angle));
                    if (!TryProbeSurface(scene, offset, referenceZ, out float z, out bool _))
                        return up; // overhanging an edge - keep it level
                    pts[i] = new Vec3(offset.x, offset.y, z);
                }

                Vec3 normal = Vec3.CrossProduct(pts[1] - pts[0], pts[2] - pts[0]);
                if (normal.LengthSquared < 0.000001f) return up;
                normal.Normalize();
                if (normal.z < 0f) normal = normal * -1f;
                return normal;
            }
            catch (Exception ex)
            {
                Log("Surface normal probe failed: " + ex);
                return up;
            }
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
                    BasePath.Name, "Modules", "BattleBallista", "BattleBallista.log");
            }
            catch
            {
                _cachedLogPath = "BattleBallista.log";
            }
            return _cachedLogPath;
        }

        private static void Log(string message)
        {
            try
            {
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
