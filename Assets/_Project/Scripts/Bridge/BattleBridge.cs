using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Battle.Units.HealthBar;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI;
// DraftController lives in Wassup.Core above.

namespace Wassup.Bridge
{
    // The ONLY allowed bridge between MonoBehaviour world and ECS world.
    // External MonoBehaviour code must go through this class — no direct EntityManager / World / SystemAPI access.
    public class BattleBridge : MonoBehaviour
    {
        [SerializeField] private AttackDeck deck;
        [SerializeField] private MapData map;
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private float spawnHeight = 0.5f;
        [SerializeField] private ResultScreen resultScreen;
        [SerializeField] private DefenderUnitData[] defenderPool;
        [SerializeField] private DraftController draftController;
        [SerializeField] private SkillRuntime skillRuntime;
        [SerializeField] private PlacementPhaseView _placementPhaseView;
        [SerializeField] private Wassup.Presentation.SpineDefenderPool spineDefenderPool;
        [SerializeField] private float spineDefenderYOffset = 0f;
        [SerializeField] private Wassup.Presentation.VfxSpawner vfxSpawner;
        // Phase 9 P9-07 — tileSize 단일 소스화. Awake 에서 MapView/PlacementInput 으로 주입.
        [SerializeField] private Wassup.Core.MapView mapView;
        [SerializeField] private Wassup.Core.PlacementInput placementInput;

        private World _world;
        private EntityManager _em;
        private EntityQuery _aliveAttackersQuery;
        private bool _aliveAttackersQueryCreated;
        private readonly List<SpawnEntry> _pending = new();
        private readonly Dictionary<AttackUnitData, RenderMeshArray> _renderCache = new();
        private readonly Dictionary<DefenderUnitData, RenderMeshArray> _defenderRenderCache = new();
        private readonly HashSet<Vector2Int> _occupiedTiles = new();
        private readonly Dictionary<Vector2Int, (Entity entity, DefenderUnitData data)> _defenderByTile = new();
        private readonly List<RenderMeshArray> _projectileRenderByIndex = new();
        private readonly Dictionary<(ProjectileData data, Material mat), int> _projectileRenderIndex = new();
        private EntityQuery _projectileSpawnRequestQuery;
        private bool _projectileSpawnRequestQueryCreated;
        private EntityQuery _projectileQuery;
        private bool _projectileQueryCreated;
        private RenderMeshArray _healthBarRenderArray;
        private Material _healthBarMaterial;
        private const float SynergyPerNeighbor = 0.1f;
        private readonly HashSet<Entity> _synergyActivatedEntities = new();
        private int _synergyActivations;
        private int _synergyPeakCount;
        private float _startTime;
        private float _timerDuration;
        private bool _running;
        private bool _placementAllowed;
        private bool _resultShown;
        private int _goalReachedCount;
        private NativeQueue<GoalReachedEvent> _goalEventQueue;
        private NativeQueue<DefenderDeathEvent> _defenderDeathQueue;
        private NativeQueue<Wassup.Battle.Combat.MeteorBurstEvent> _meteorBurstQueue;
        private NativeQueue<Wassup.Battle.Combat.DefenderAttackEvent> _defenderAttackQueue;

        // Phase 9 flow field 싱글톤 entity reference
        private Entity _flowFieldSingleton = Entity.Null;

        // Phase 9 P9-07 — MapView / PlacementInput 의 Start() 가 돌기 전에 tileSize 와
        // map 을 주입한다. Unity 가 Start 순서를 보장하지 않으므로 Awake 단계에서 수행.
        private void Awake()
        {
            if (mapView != null)
                mapView.Initialize(map, tileSize);
            else
                Debug.LogError("[BattleBridge] mapView reference missing — assign in Inspector.", this);

            if (placementInput != null)
                placementInput.Initialize(tileSize);
            else
                Debug.LogError("[BattleBridge] placementInput reference missing — assign in Inspector.", this);
        }

        private void Start()
        {
            if (resultScreen != null)
            {
                resultScreen.RestartRequested += OnRestartRequested;
                resultScreen.RedraftRequested += OnRedraftRequested;
            }
        }

        private void OnRestartRequested()
        {
            if (_world == null)
            {
                _placementPhaseView?.BeginPlacementPhase();
                return;
            }

            var logger = GameManager.Instance?.Logger;
            if (logger != null)
            {
                logger.EndSession();
                logger.StartSession();
                // Phase 7 (Q6=a): Restart keeps the same picked skill loadout,
                // but logger.StartSession() created an empty SkillRecord — so
                // re-populate skill.loadout/pool/seed so the new log file carries
                // the same audit trail the player actually plays with.
                ReLogSkillLoadoutForNewSession(logger);
            }

            TeardownCurrentBattle();
            if (resultScreen != null) resultScreen.Hide();
            _running = false;
            _resultShown = false;
            _placementPhaseView?.BeginPlacementPhase();
        }

        private void ReLogSkillLoadoutForNewSession(Logging.BattleLogger logger)
        {
            if (logger == null) return;
            var ctl = GameManager.Instance?.SkillLoadout;
            if (ctl == null || ctl.Picked.Count == 0) return;
            var pickedIds = new System.Collections.Generic.List<string>();
            foreach (var s in ctl.Picked) if (s != null) pickedIds.Add(s.id);
            logger.SetSkillLoadout(pickedIds);
            var poolIds = new System.Collections.Generic.List<string>();
            foreach (var s in ctl.Pool) if (s != null) poolIds.Add(s.id);
            logger.SetSkillPool(poolIds, ctl.Seed);
        }

        private void OnRedraftRequested()
        {
            if (draftController == null)
            {
                Debug.LogWarning("[BattleBridge] RedraftRequested but draftController unset; falling back to RestartBattle.");
                RestartBattle();
                return;
            }
            // Re-opening the draft invalidates the previous session — roll the log,
            // tear down ECS state, hide the result panel, then let DraftController
            // show the pick UI. StartBattle will fire again inside TryConfirm.
            var logger = GameManager.Instance?.Logger;
            if (logger != null)
            {
                logger.EndSession();
                logger.StartSession();
            }
            if (_world != null) TeardownCurrentBattle();
            if (resultScreen != null) resultScreen.Hide();
            _running = false;
            _resultShown = false;
            draftController.BeginDraft();
        }

        // External MB entry point — tears down current ECS state and starts a fresh session on the same deck/map.
        public void RestartBattle()
        {
            if (_world == null)
            {
                // No battle started yet — delegate to StartBattle.
                StartBattle();
                return;
            }
            // Roll the log over: close current session, start a fresh one so each battle has its own JSON file.
            var logger = GameManager.Instance?.Logger;
            if (logger != null)
            {
                logger.EndSession();
                logger.StartSession();
            }
            TeardownCurrentBattle();
            if (resultScreen != null) resultScreen.Hide();
            StartBattle();
        }

        private void TeardownCurrentBattle()
        {
            _running = false;
            _placementAllowed = false;
            if (skillRuntime != null) skillRuntime.ResetAll();
            if (GameManager.Instance != null && GameManager.Instance.CostRuntime != null)
                GameManager.Instance.CostRuntime.StopRegen();
            if (spineDefenderPool != null) spineDefenderPool.DisposeAll();

            // Destroy all battle-related entities so the next StartBattle has a clean slate.
            var attackers = _em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
            _em.DestroyEntity(attackers);
            attackers.Dispose();

            var defenders = _em.CreateEntityQuery(ComponentType.ReadOnly<DefenderUnitTag>());
            _em.DestroyEntity(defenders);
            defenders.Dispose();

            var projectiles = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileTag>());
            _em.DestroyEntity(projectiles);
            projectiles.Dispose();

            var healthBars = _em.CreateEntityQuery(ComponentType.ReadOnly<HealthBarTag>());
            _em.DestroyEntity(healthBars);
            healthBars.Dispose();

            var singletons = _em.CreateEntityQuery(ComponentType.ReadOnly<GoalReachedEventsSingleton>());
            _em.DestroyEntity(singletons);
            singletons.Dispose();

            var defenderDeathSingletons = _em.CreateEntityQuery(ComponentType.ReadOnly<DefenderDeathEventsSingleton>());
            _em.DestroyEntity(defenderDeathSingletons);
            defenderDeathSingletons.Dispose();

            var meteorBurstSingletons = _em.CreateEntityQuery(ComponentType.ReadOnly<Wassup.Battle.Combat.MeteorBurstEventsSingleton>());
            _em.DestroyEntity(meteorBurstSingletons);
            meteorBurstSingletons.Dispose();

            var defenderAttackSingletons = _em.CreateEntityQuery(ComponentType.ReadOnly<Wassup.Battle.Combat.DefenderAttackEventsSingleton>());
            _em.DestroyEntity(defenderAttackSingletons);
            defenderAttackSingletons.Dispose();

            // Dispose the queues; StartBattle will create fresh ones.
            if (_goalEventQueue.IsCreated) _goalEventQueue.Dispose();
            if (_defenderDeathQueue.IsCreated) _defenderDeathQueue.Dispose();
            if (_meteorBurstQueue.IsCreated) _meteorBurstQueue.Dispose();
            if (_defenderAttackQueue.IsCreated) _defenderAttackQueue.Dispose();

            // Phase 9: dispose the flow field singleton arrays + destroy the entity.
            TeardownFlowField();
        }

        // Idempotent: 재호출(판 재시작/redraft) 시 기존 Persistent arrays dispose 후 재생성.
        // CRITICAL #1 (Codex 2차 리뷰): AddComponentData 는 component 존재 시 throw,
        // 그리고 기존 arrays 가 dispose 없이 덮어써지면 누수. TeardownFlowField 선행으로 해결.
        private void BuildFlowField()
        {
            if (map == null || _em == null) return;

            // 기존 싱글톤 있으면 arrays dispose + entity destroy (멱등성 보장)
            TeardownFlowField();

            int w = MapData.Width;
            int h = MapData.Height;
            int n = w * h;

            var walk = new NativeArray<byte>(n, Allocator.Temp);
            try
            {
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var t = map.GetTile(x, y);
                    walk[y * w + x] = (byte)(t == TileType.Obstacle ? 0 : 1);
                    // Phase 9: Buildable / Path 둘 다 walkable.
                    // Phase 10 enum 재분류 후 Walkable 단일 플래그로 명료화.
                }

                var flow = new NativeArray<float2>(n, Allocator.Persistent);
                var dist = new NativeArray<int>(n, Allocator.Persistent);
                try
                {
                    var gridSize = new int2(w, h);
                    var goal = new int2(map.GoalCell.x, map.GoalCell.y);

                    FlowFieldBuilder.Build(walk, gridSize, goal, flow, dist);

                    var data = new FlowFieldSingleton
                    {
                        flow = flow,
                        dist = dist,
                        gridSize = gridSize,
                        goalCell = goal,
                        tileSize = tileSize,
                        version = 1,
                    };

                    _flowFieldSingleton = _em.CreateEntity();
                    _em.AddComponentData(_flowFieldSingleton, data);
                }
                catch
                {
                    if (flow.IsCreated) flow.Dispose();
                    if (dist.IsCreated) dist.Dispose();
                    throw;
                }
            }
            finally
            {
                if (walk.IsCreated) walk.Dispose();
            }
        }

        private void TeardownFlowField()
        {
            if (_flowFieldSingleton != Entity.Null && _em != null && _em.Exists(_flowFieldSingleton))
            {
                if (_em.HasComponent<FlowFieldSingleton>(_flowFieldSingleton))
                {
                    var data = _em.GetComponentData<FlowFieldSingleton>(_flowFieldSingleton);
                    data.Dispose();
                }
                _em.DestroyEntity(_flowFieldSingleton);
            }
            _flowFieldSingleton = Entity.Null;
        }

        // Phase 6: placement phase enters this path — ECS state is initialized so
        // PlaceDefenderAs works immediately, but spawns / timer stay dormant.
        public void BeginPlacement()
        {
            if (deck == null || map == null)
            {
                Debug.LogError("[BattleBridge] deck or map reference missing.", this);
                return;
            }
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {
                Debug.LogWarning("[BattleBridge] Default World not ready at BeginPlacement; will retry.");
                return;
            }
            _em = _world.EntityManager;
            _pending.Clear();
            _occupiedTiles.Clear();
            _defenderByTile.Clear();
            _synergyActivatedEntities.Clear();
            _synergyActivations = 0;
            _synergyPeakCount = 0;
            _goalReachedCount = 0;
            _running = false;
            _placementAllowed = true;
            _resultShown = false;
            if (skillRuntime != null) skillRuntime.ResetAll();

            EnsureQueriesAndQueues();

            GameManager.Instance?.Logger?.SetAttackDeckId(deck.deckId);
            Debug.Log("[BattleBridge] Placement phase ready.");
        }


        public void StartBattle()
        {
            if (deck == null || map == null)
            {
                Debug.LogError("[BattleBridge] deck or map reference missing.", this);
                return;
            }
            if (!_placementAllowed) BeginPlacement();
            if (_world == null) return;
            _pending.AddRange(deck.spawns);
            _startTime = Time.time;
            _timerDuration = deck.timerDurationSec;
            _running = true;
            Debug.Log($"[BattleBridge] Battle started with deck '{deck.deckId}' ({deck.spawns.Count} spawns queued).");
        }

        private void EnsureQueriesAndQueues()
        {
            if (!_aliveAttackersQueryCreated)
            {
                _aliveAttackersQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
                _aliveAttackersQueryCreated = true;
            }

            if (!_projectileSpawnRequestQueryCreated)
            {
                _projectileSpawnRequestQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSpawnRequest>());
                _projectileSpawnRequestQueryCreated = true;
            }

            if (!_projectileQueryCreated)
            {
                _projectileQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileTag>());
                _projectileQueryCreated = true;
            }

            // Create the shared queue and inject the singleton so ECS systems can enqueue events.
            if (_goalEventQueue.IsCreated) _goalEventQueue.Dispose();
            _goalEventQueue = new NativeQueue<GoalReachedEvent>(Allocator.Persistent);
            var singletonEntity = _em.CreateEntity();
            _em.AddComponentData(singletonEntity, new GoalReachedEventsSingleton { queue = _goalEventQueue });

            // Phase 4 defender-death event channel.
            if (_defenderDeathQueue.IsCreated) _defenderDeathQueue.Dispose();
            _defenderDeathQueue = new NativeQueue<DefenderDeathEvent>(Allocator.Persistent);
            var deathSingleton = _em.CreateEntity();
            _em.AddComponentData(deathSingleton, new DefenderDeathEventsSingleton { queue = _defenderDeathQueue });

            // Phase 8 §12 Meteor burst event channel for VFX timing.
            if (_meteorBurstQueue.IsCreated) _meteorBurstQueue.Dispose();
            _meteorBurstQueue = new NativeQueue<Wassup.Battle.Combat.MeteorBurstEvent>(Allocator.Persistent);
            var meteorBurstSingleton = _em.CreateEntity();
            _em.AddComponentData(meteorBurstSingleton, new Wassup.Battle.Combat.MeteorBurstEventsSingleton { queue = _meteorBurstQueue });

            // Phase 8 §13 follow-up — defender attack trigger event channel.
            // AttackSystem enqueues for both projectile and melee branches so
            // Spine attack anim fires consistently.
            if (_defenderAttackQueue.IsCreated) _defenderAttackQueue.Dispose();
            _defenderAttackQueue = new NativeQueue<Wassup.Battle.Combat.DefenderAttackEvent>(Allocator.Persistent);
            var attackSingleton = _em.CreateEntity();
            _em.AddComponentData(attackSingleton, new Wassup.Battle.Combat.DefenderAttackEventsSingleton { queue = _defenderAttackQueue });

            // Phase 9 P9-03 wiring — build the flow field singleton once the map
            // and EntityManager are confirmed ready. Idempotent: re-entry from
            // RestartBattle / Redraft first calls TeardownFlowField().
            BuildFlowField();
        }

        public void StopBattle()
        {
            _running = false;
            _placementAllowed = false;
            // Phase 0: entities persist until play mode exit. P0-09 will add full teardown.
        }

        // Replaces the defender pool used by random placement selection. Called by
        // DraftController once picks are confirmed. A null or empty array resets
        // the pool to the inspector-assigned fallback (if any) so the caller can
        // "un-confirm" a draft during tests.
        public void SetDefenderPool(DefenderUnitData[] pool)
        {
            defenderPool = pool;
        }

        public DefenderUnitData[] DefenderPool => defenderPool;

        private SkillData[] _skillLoadout;
        public SkillData[] SkillLoadout => _skillLoadout;

        public void SetSkillLoadout(SkillData[] loadout)
        {
            _skillLoadout = loadout;
        }

        // ============== Skill casting (Phase 2) ==============

        // Cast a TilePoint-targeted skill (e.g. Slow Field). Returns true on success.
        // `affectedCount` reports how many ECS entities received the effect — used
        // by the logger and the UI for feedback. Returns false if the battle is
        // not running, the skill type does not match, or the cooldown gate rejects.
        public bool CastSkillAtTile(SkillData skill, Vector2Int tile, out int affectedCount)
        {
            affectedCount = 0;
            if (!_running || skill == null) return false;
            if (skill.target != SkillTargetType.TilePoint) return false;
            if (skillRuntime != null && !skillRuntime.IsReady(skill)) return false;

            Vector2Int secondaryTile = new(-1, -1);
            switch (skill.effect)
            {
                case SkillEffectType.SlowField:
                    affectedCount = ApplySlowField(tile, skill);
                    break;
                case SkillEffectType.Tornado:
                    affectedCount = ApplyTornado(tile, skill);
                    break;
                case SkillEffectType.Meteor:
                    affectedCount = ApplyMeteor(tile, skill);
                    break;
                default:
                    Debug.LogWarning($"[BattleBridge] TilePoint skill '{skill.id}' has unsupported effect {skill.effect}.");
                    return false;
            }

            skillRuntime?.Consume(skill);
            GameManager.Instance?.Logger?.RecordSkillUsage(new Logging.SkillUsageLog
            {
                skill_id = skill.id,
                time = Time.time - _startTime,
                target_tile = tile,
                target_tile_b = secondaryTile,
                affected_count = affectedCount,
                cost_spent = skill.cost,
            });
            Debug.Log($"[BattleBridge] CastSkillAtTile {skill.id} @ {tile} → {affectedCount} affected, cd={skill.cooldownSec}s");
            return true;
        }

        // Phase 7 — Portal two-tap cast. Unlike CastSkillAtTile, this takes two
        // tiles (entry/exit) in one call. SkillBar captures both taps before
        // invoking. Returns false if the skill is not a Portal or the cooldown
        // gate rejects.
        public bool CastPortal(SkillData skill, Vector2Int entryTile, Vector2Int exitTile, out int affectedCount)
        {
            affectedCount = 0;
            if (!_running || skill == null) return false;
            if (skill.effect != SkillEffectType.Portal) return false;
            if (skillRuntime != null && !skillRuntime.IsReady(skill)) return false;

            affectedCount = ApplyPortal(entryTile, exitTile, skill);
            skillRuntime?.Consume(skill);
            GameManager.Instance?.Logger?.RecordSkillUsage(new Logging.SkillUsageLog
            {
                skill_id = skill.id,
                time = Time.time - _startTime,
                target_tile = entryTile,
                target_tile_b = exitTile,
                affected_count = affectedCount,
                cost_spent = skill.cost,
            });
            Debug.Log($"[BattleBridge] CastPortal {skill.id} {entryTile}→{exitTile} for {skill.durationSec}s");
            return true;
        }

        // Cast a DefenderUnit-targeted skill (Power Surge, Rapid Fire). `tile` is
        // the defender's cell coordinate as recorded during PlaceDefender.
        public bool CastSkillOnDefender(SkillData skill, Vector2Int tile, out int affectedCount)
        {
            affectedCount = 0;
            if (!_running || skill == null) return false;
            if (skill.target != SkillTargetType.DefenderUnit) return false;
            if (!_defenderByTile.TryGetValue(tile, out var defender) || !_em.Exists(defender.entity)) return false;
            var entity = defender.entity;
            if (skillRuntime != null && !skillRuntime.IsReady(skill)) return false;

            switch (skill.effect)
            {
                case SkillEffectType.PowerSurge:
                    EffectSpawner.ApplyDamageBoost(_em, entity, skill.durationSec, skill.magnitude);
                    affectedCount = 1;
                    break;
                case SkillEffectType.RapidFire:
                    EffectSpawner.ApplyCooldownReduction(_em, entity, skill.durationSec, skill.magnitude);
                    affectedCount = 1;
                    break;
                default:
                    Debug.LogWarning($"[BattleBridge] DefenderUnit skill '{skill.id}' has unsupported effect {skill.effect}.");
                    return false;
            }

            skillRuntime?.Consume(skill);
            GameManager.Instance?.Logger?.RecordSkillUsage(new Logging.SkillUsageLog
            {
                skill_id = skill.id,
                time = Time.time - _startTime,
                target_tile = tile,
                affected_count = affectedCount,
                cost_spent = skill.cost,
            });
            Debug.Log($"[BattleBridge] CastSkillOnDefender {skill.id} on defender@{tile} (entity {entity.Index}) cd={skill.cooldownSec}s");
            return true;
        }

        // Phase 9 — 모든 스킬 대상 타일 → world center 계산의 단일 소스.
        // Phase 10 에서 tileSize 가 theme 파라미터로 승격될 때 이 helper 만 바꾸면 됨.
        private float3 GridToWorldCenter(Vector2Int cell, float y = 0f)
            => new float3(cell.x * tileSize, y, cell.y * tileSize);

        private int ApplySlowField(Vector2Int tile, SkillData skill)
        {
            // Collect all currently-alive attack unit entities; filter by XZ distance
            // to the target world point; apply SlowEffect through EffectSpawner so
            // the Effects context remains the sole writer.
            if (!_aliveAttackersQueryCreated) return 0;
            var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);

            float3 targetWorld = GridToWorldCenter(tile);
            float rangeWorld = skill.range * tileSize;
            float rangeSq = rangeWorld * rangeWorld;
            int affected = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (!_em.HasComponent<LocalTransform>(e)) continue;
                var pos = _em.GetComponentData<LocalTransform>(e).Position;
                float dx = pos.x - targetWorld.x;
                float dz = pos.z - targetWorld.z;
                if (dx * dx + dz * dz > rangeSq) continue;
                EffectSpawner.ApplySlow(_em, e, skill.durationSec, skill.magnitude);
                affected++;
            }

            entities.Dispose();
            return affected;
        }

        // Phase 7 — Tornado. Pulls in-range attackers toward the target tile for
        // `durationSec`. `skill.magnitude` is the pull speed (world units/sec).
        private int ApplyTornado(Vector2Int tile, SkillData skill)
        {
            float3 targetWorld = GridToWorldCenter(tile);
            float rangeWorld = skill.range * tileSize;

            // Phase 8 §17 — continuous field (replaces Phase 7 per-attacker
            // snapshot). MovementSystem queries live TornadoField entities each
            // frame, so enemies that enter the radius mid-duration are also
            // pulled. Re-cast creates an independent field; multiple fields can
            // coexist and the attacker is pulled by the first one that contains
            // it.
            EffectSpawner.SpawnTornadoField(_em, targetWorld, rangeWorld, skill.magnitude, skill.durationSec);

            // Phase 8 §12: swirling particle ring over the Tornado center.
            if (vfxSpawner != null)
                vfxSpawner.SpawnTornado(new Vector3(targetWorld.x, 0f, targetWorld.z), rangeWorld, skill.durationSec);

            // Affected count is reported async as attackers enter / get pulled;
            // at cast time we conservatively pre-count overlaps so the log has
            // a baseline without waiting for the field to expire.
            if (!_aliveAttackersQueryCreated) return 0;
            var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
            float rSq = rangeWorld * rangeWorld;
            int preview = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (!_em.HasComponent<LocalTransform>(e)) continue;
                var p = _em.GetComponentData<LocalTransform>(e).Position;
                float dx = p.x - targetWorld.x;
                float dz = p.z - targetWorld.z;
                if (dx * dx + dz * dz <= rSq) preview++;
            }
            entities.Dispose();
            return preview;
        }

        // Phase 7 — Meteor. Spawns a carrier entity + a MonoBehaviour-side warning
        // ring for `skill.warningSec`; MeteorResolutionSystem applies the AoE damage
        // when the warning expires.
        private int ApplyMeteor(Vector2Int tile, SkillData skill)
        {
            float3 centerWorld = GridToWorldCenter(tile);
            float radiusWorld = skill.range * tileSize;
            float warn = skill.warningSec > 0f ? skill.warningSec : 0f;
            EffectSpawner.SpawnMeteor(_em, centerWorld, radiusWorld, skill.magnitude, warn);
            SpawnMeteorWarningVisual(centerWorld, radiusWorld, warn);
            // Phase 8 §13: falling streak during the warning window. Silent
            // no-op when meteorFallPrefab slot empty — Meteor still plays
            // without the falling visual.
            if (vfxSpawner != null && warn > 0f)
                vfxSpawner.SpawnMeteorFall(new Vector3(centerWorld.x, 0f, centerWorld.z), warn);
            // Actual damage count is reported async by MeteorResolutionSystem; at
            // cast time we conservatively pre-count current overlaps so the log is
            // informative without waiting for the burst.
            if (!_aliveAttackersQueryCreated) return 0;
            var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
            float rSq = radiusWorld * radiusWorld;
            int preview = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                if (!_em.HasComponent<LocalTransform>(entities[i])) continue;
                var p = _em.GetComponentData<LocalTransform>(entities[i]).Position;
                float dx = p.x - centerWorld.x;
                float dz = p.z - centerWorld.z;
                if (dx * dx + dz * dz <= rSq) preview++;
            }
            entities.Dispose();
            return preview;
        }

        // Phase 7 — Portal. Spawns a PortalLink carrier with two endpoints. On
        // teleport, MovementSystem advances the attacker's waypoint index to the
        // first waypoint whose cell matches (or follows) the exit tile so they
        // keep heading toward the goal from the exit.
        private int ApplyPortal(Vector2Int entryTile, Vector2Int exitTile, SkillData skill)
        {
            float3 entryWorld = GridToWorldCenter(entryTile);
            float3 exitWorld = GridToWorldCenter(exitTile);
            float entryRadius = tileSize * 0.5f; // half-tile catch radius
            EffectSpawner.SpawnPortal(_em, entryWorld, exitWorld, entryRadius, skill.durationSec);

            // Phase 8 §12: two swirls + connecting beam for the portal's lifetime.
            if (vfxSpawner != null)
            {
                vfxSpawner.SpawnPortal(
                    new Vector3(entryWorld.x, 0f, entryWorld.z),
                    new Vector3(exitWorld.x, 0f, exitWorld.z),
                    skill.durationSec);
            }

            return 1;
        }

        // Meteor telegraph: a flat translucent red quad on the ground that
        // auto-destroys after warningSec. Deliberately MonoBehaviour-only — ECS
        // holds the resolution timer, but the *visual* is a gameplay-layer object.
        private void SpawnMeteorWarningVisual(float3 centerWorld, float radiusWorld, float warningSec)
        {
            if (warningSec <= 0f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "MeteorWarning";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.position = new Vector3(centerWorld.x, 0.02f, centerWorld.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            float d = radiusWorld * 2f;
            go.transform.localScale = new Vector3(d, d, 1f);
            var rend = go.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                mat.color = new Color(1f, 0.15f, 0.15f, 0.55f);
                rend.sharedMaterial = mat;
            }
            Destroy(go, warningSec);
        }

        private void Update()
        {
            if (!_running) return;

            float t = Time.time - _startTime;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (t >= _pending[i].triggerTimeSec)
                {
                    SpawnUnit(_pending[i]);
                    _pending.RemoveAt(i);
                }
            }

            DrainProjectileSpawnRequests();
            DrainDefenderDeathEvents();
            DrainMeteorBurstEvents();
            DrainDefenderAttackEvents();
            DrainGoalEvents();
            CheckTimer();
            CheckVictory();
        }

        private void DrainDefenderDeathEvents()
        {
            if (!_defenderDeathQueue.IsCreated) return;
            while (_defenderDeathQueue.TryDequeue(out var evt))
            {
                var cell = new Vector2Int(evt.cell.x, evt.cell.y);
                if (spineDefenderPool != null && _defenderByTile.TryGetValue(cell, out var binding))
                {
                    spineDefenderPool.NotifyDeath(binding.entity);
                }
                _defenderByTile.Remove(cell);
                _occupiedTiles.Remove(cell);
                RecomputeSynergyFor(cell);
                Debug.Log($"[BattleBridge] Defender died @ {cell}; tile freed, synergy recomputed.");
            }
        }

        // Phase 8 §12 — when MeteorResolutionSystem burns its AoE, it enqueues a
        // burst event so the VFX layer can fire a particle burst on the same
        // frame without any ECS references on the MonoBehaviour side.
        private void DrainMeteorBurstEvents()
        {
            if (!_meteorBurstQueue.IsCreated) return;
            while (_meteorBurstQueue.TryDequeue(out var evt))
            {
                if (vfxSpawner == null) continue;
                vfxSpawner.SpawnMeteorBurst(new Vector3(evt.center.x, 0f, evt.center.z), evt.radius);
            }
        }

        // Phase 8 §13 follow-up — unified attack trigger drain. AttackSystem
        // enqueues for both projectile and melee defender branches so Spine
        // attack animation + facing flip fires the same way regardless of
        // whether the defender has a ProjectileRef. Previously melee defenders
        // (Bastion/Bruiser) missed the trigger because it was only wired inside
        // DrainProjectileSpawnRequests.
        private void DrainDefenderAttackEvents()
        {
            if (!_defenderAttackQueue.IsCreated) return;
            while (_defenderAttackQueue.TryDequeue(out var evt))
            {
                if (spineDefenderPool == null) continue;
                var targetWorld = new Vector3(evt.targetWorld.x, evt.targetWorld.y, evt.targetWorld.z);
                spineDefenderPool.NotifyAttack(evt.defender, targetWorld);
            }
        }

        // Converts every staged ProjectileSpawnRequest into a live projectile entity.
        // Done here (MonoBehaviour) rather than inside AttackSystem because creating
        // RenderMesh components requires managed RenderMeshArray references that are
        // not accessible from a Burst-compiled ISystem.
        private void DrainProjectileSpawnRequests()
        {
            if (!_projectileSpawnRequestQueryCreated) return;
            if (_projectileSpawnRequestQuery.IsEmpty) return;

            var requestEntities = _projectileSpawnRequestQuery.ToEntityArray(Allocator.Temp);
            var requestData = _projectileSpawnRequestQuery.ToComponentDataArray<ProjectileSpawnRequest>(Allocator.Temp);
            for (int i = 0; i < requestEntities.Length; i++)
            {
                var req = requestData[i];
                // Spine attack trigger moved to DrainDefenderAttackEvents
                // so both projectile and melee defenders share the same hook.
                SpawnProjectile(req);
                _em.RemoveComponent<ProjectileSpawnRequest>(requestEntities[i]);
            }
            requestEntities.Dispose();
            requestData.Dispose();
        }

        private void SpawnProjectile(ProjectileSpawnRequest req)
        {
            if (req.assetIndex < 0 || req.assetIndex >= _projectileRenderByIndex.Count)
            {
                Debug.LogWarning($"[BattleBridge] ProjectileSpawnRequest assetIndex {req.assetIndex} out of range; dropping.");
                return;
            }

            var entity = _em.CreateEntity();
#if UNITY_EDITOR
            _em.SetName(entity, $"Projectile_{req.assetIndex}");
#endif
            var spawnPos = new float3(req.origin.x, spawnHeight, req.origin.z);
            _em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, req.visualScale));
            _em.AddComponent<ProjectileTag>(entity);
            _em.AddComponentData(entity, new ProjectileState
            {
                target = req.target,
                speed = req.speed,
                damage = req.damage,
                hitThreshold = req.hitThreshold,
                onHitEffect = req.onHitEffect,
                splashRadius = req.splashRadius,
                splashDamageMul = req.splashDamageMul,
            });

            var desc = new RenderMeshDescription(
                shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows: false);
            RenderMeshUtility.AddComponents(entity, _em, desc, _projectileRenderByIndex[req.assetIndex],
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        }

        // Fires the defender's on-place effect on surrounding entities. Returns
        // the count of entities affected so the logger can record magnitude.
        // Writes to Effects components go through EffectSpawner so the Effects-
        // context write gateway (Phase 2 decision) stays the sole path.
        private int ApplyOnPlaceEffect(DefenderUnitData unitData, Vector2Int placedCell, Entity placedEntity)
        {
            if (unitData.onPlaceEffect == OnPlaceEffectType.None) return 0;
            if (unitData.onPlaceRange <= 0f) return 0;

            float3 center = GridToWorldCenter(placedCell);
            float rangeWorld = unitData.onPlaceRange * tileSize;
            float rangeSq = rangeWorld * rangeWorld;
            int affected = 0;

            if (unitData.onPlaceEffect == OnPlaceEffectType.SlowPulse)
            {
                if (!_aliveAttackersQueryCreated) return 0;
                var entities = _aliveAttackersQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    if (!_em.HasComponent<LocalTransform>(e)) continue;
                    var pos = _em.GetComponentData<LocalTransform>(e).Position;
                    float dx = pos.x - center.x;
                    float dz = pos.z - center.z;
                    if (dx * dx + dz * dz > rangeSq) continue;
                    EffectSpawner.ApplySlow(_em, e, unitData.onPlaceDuration, unitData.onPlaceMagnitude);
                    affected++;
                }
                entities.Dispose();
            }
            else if (unitData.onPlaceEffect == OnPlaceEffectType.BoostNearbyDefenders)
            {
                // Reuse the tuple-based tile map — no ECS query needed since placement
                // grid already gives us every defender. Self-inclusion is allowed
                // (PHASE4.md §4 autonomy, chosen for simplicity + stronger feedback).
                foreach (var kv in _defenderByTile)
                {
                    var d = kv.Value;
                    if (!_em.Exists(d.entity)) continue;
                    var tileCell = kv.Key;
                    float dx = tileCell.x - placedCell.x;
                    float dz = tileCell.y - placedCell.y;
                    if (dx * dx + dz * dz > unitData.onPlaceRange * unitData.onPlaceRange) continue;
                    EffectSpawner.ApplyDamageBoost(_em, d.entity, unitData.onPlaceDuration, unitData.onPlaceMagnitude);
                    affected++;
                }
            }

            return affected;
        }

        // Recomputes adjacency synergy for `cell` and its four neighbors. Same-type
        // defender adjacency grants a damage multiplier of (1 + 0.1 × neighborCount).
        // Writes to SynergyBuff go through EffectSpawner so the Effects-context
        // write gateway stays a single code path (Phase 2 decision #9).
        private void RecomputeSynergyFor(Vector2Int cell)
        {
            var cells = new Vector2Int[]
            {
                cell,
                cell + new Vector2Int(1, 0),
                cell + new Vector2Int(-1, 0),
                cell + new Vector2Int(0, 1),
                cell + new Vector2Int(0, -1),
            };

            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i];
                if (!_defenderByTile.TryGetValue(c, out var here)) continue;
                int neighbors = 0;
                if (_defenderByTile.TryGetValue(c + new Vector2Int(1, 0), out var n1) && n1.data == here.data) neighbors++;
                if (_defenderByTile.TryGetValue(c + new Vector2Int(-1, 0), out var n2) && n2.data == here.data) neighbors++;
                if (_defenderByTile.TryGetValue(c + new Vector2Int(0, 1), out var n3) && n3.data == here.data) neighbors++;
                if (_defenderByTile.TryGetValue(c + new Vector2Int(0, -1), out var n4) && n4.data == here.data) neighbors++;

                if (neighbors == 0)
                {
                    EffectSpawner.RemoveSynergy(_em, here.entity);
                }
                else
                {
                    bool wasPresent = _em.HasComponent<SynergyBuff>(here.entity);
                    EffectSpawner.SetSynergy(_em, here.entity, 1f + SynergyPerNeighbor * neighbors);
                    if (!wasPresent && _synergyActivatedEntities.Add(here.entity))
                    {
                        _synergyActivations++;
                    }
                }
            }

            using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<SynergyBuff>());
            int currentCount = q.CalculateEntityCount();
            if (currentCount > _synergyPeakCount) _synergyPeakCount = currentCount;
        }

        // Shared mesh/material for every health bar so adding more units does not
        // allocate per-entity assets. Lazily created and disposed on scene teardown.
        private RenderMeshArray GetOrCreateHealthBarRenderArray()
        {
            if (_healthBarRenderArray.MaterialReferences != null &&
                _healthBarRenderArray.MaterialReferences.Length > 0)
                return _healthBarRenderArray;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            _healthBarMaterial = new Material(shader) { color = new Color(0.2f, 0.95f, 0.2f, 1f) };
            _healthBarMaterial.SetColor("_BaseColor", new Color(0.2f, 0.95f, 0.2f, 1f));
            var mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            _healthBarRenderArray = new RenderMeshArray(new[] { _healthBarMaterial }, new[] { mesh });
            return _healthBarRenderArray;
        }

        private void CreateHealthBar(Entity owner, float yOffset, float baseScale)
        {
            var arr = GetOrCreateHealthBarRenderArray();
            var entity = _em.CreateEntity();
#if UNITY_EDITOR
            _em.SetName(entity, "HealthBar");
#endif
            _em.AddComponent<HealthBarTag>(entity);
            _em.AddComponentData(entity, new HealthBarState
            {
                owner = owner,
                yOffset = yOffset,
                baseScale = baseScale,
            });
            _em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(
                new float3(0f, 0f, 0f), quaternion.identity, baseScale));
            var desc = new RenderMeshDescription(
                shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows: false);
            RenderMeshUtility.AddComponents(entity, _em, desc, arr,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        }

        private int GetOrCreateProjectileAssetIndex(ProjectileData projectile, Material fallbackMaterial)
        {
            var material = projectile.visualMaterial != null ? projectile.visualMaterial : fallbackMaterial;
            var key = (projectile, material);
            if (_projectileRenderIndex.TryGetValue(key, out var idx)) return idx;

            var mesh = projectile.visualMesh != null
                ? projectile.visualMesh
                : Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var arr = new RenderMeshArray(new[] { material }, new[] { mesh });
            idx = _projectileRenderByIndex.Count;
            _projectileRenderByIndex.Add(arr);
            _projectileRenderIndex[key] = idx;
            return idx;
        }

        private void DrainGoalEvents()
        {
            if (!_goalEventQueue.IsCreated) return;
            while (_goalEventQueue.TryDequeue(out _))
            {
                _goalReachedCount++;
                Debug.Log($"[BattleBridge] Goal reached! Count: {_goalReachedCount}/{deck.defeatGoalReachedCount}");
                if (!_resultShown && _goalReachedCount >= deck.defeatGoalReachedCount)
                {
                    _resultShown = true;
                    _running = false;
                    int playerScore = CalculatePlayerScore();
                    GameManager.Instance?.Logger?.SetResult("defeat", _goalReachedCount);
                    WriteLoggedScore(playerScore);
                    resultScreen?.ShowDefeat(playerScore);
                    Debug.Log("[BattleBridge] DEFEAT triggered.");
                    return;
                }
            }
        }

        public float TimerRemaining => _running ? Mathf.Max(0f, _timerDuration - (Time.time - _startTime)) : 0f;

        private void CheckTimer()
        {
            if (_resultShown) return;
            if (_timerDuration <= 0f) return;
            if (Time.time - _startTime < _timerDuration) return;

            _resultShown = true;
            _running = false;
            int playerScore = CalculatePlayerScore();
            GameManager.Instance?.Logger?.SetResult("victory_timeout", _goalReachedCount);
            WriteLoggedScore(playerScore);
            resultScreen?.ShowVictory(playerScore);
            Debug.Log("[BattleBridge] VICTORY — timer expired, player survived.");
        }

        // Victory = every spawn in the deck has been processed AND no attack unit entities remain alive.
        private void CheckVictory()
        {
            if (_resultShown) return;
            if (_pending.Count > 0) return;
            if (!_aliveAttackersQueryCreated) return;
            if (_aliveAttackersQuery.CalculateEntityCount() > 0) return;

            _resultShown = true;
            _running = false;
            int playerScore = CalculatePlayerScore();
            GameManager.Instance?.Logger?.SetResult("victory", _goalReachedCount);
            WriteLoggedScore(playerScore);
            resultScreen?.ShowVictory(playerScore);
            Debug.Log("[BattleBridge] VICTORY — all attack units defeated.");
        }

        private int CalculatePlayerScore()
        {
            float durationSec = Mathf.Max(0f, Time.time - _startTime);
            return Math.Max(0, (int)(durationSec * 10f - _goalReachedCount * 50));
        }

        private void WriteLoggedScore(int playerScore)
        {
            var logger = GameManager.Instance?.Logger;
            if (logger == null) return;

            var currentEntryField = logger.GetType().GetField("currentEntry",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var currentEntry = currentEntryField?.GetValue(logger);
            if (currentEntry == null) return;

            var resultField = currentEntry.GetType().GetField("result",
                BindingFlags.Instance | BindingFlags.Public);
            var battleResult = resultField?.GetValue(currentEntry);
            if (battleResult == null) return;

            var scoreField = battleResult.GetType().GetField("score",
                BindingFlags.Instance | BindingFlags.Public);
            scoreField?.SetValue(battleResult, playerScore);
        }

        // Random-pick legacy entry (Phase 0-3 behavior). Phase 4 prefers
        // PlaceDefenderAs with an explicit type, but this path stays for tests
        // and the no-selector fallback.
        public bool PlaceDefender(int tileX, int tileY)
        {
            if (defenderPool == null || defenderPool.Length == 0)
            {
                Debug.LogWarning("[BattleBridge] defenderPool is empty — cannot place defender.");
                return false;
            }
            var unitData = defenderPool[UnityEngine.Random.Range(0, defenderPool.Length)];
            return PlaceDefenderAs(tileX, tileY, unitData);
        }

        // Explicit-type placement (Phase 4). Used by DefenderSelector after the
        // player chooses which picked defender they want on the tile.
        public bool PlaceDefenderAs(int tileX, int tileY, DefenderUnitData unitData)
        {
            // Phase 6: placement is permitted during the pre-battle phase or during battle.
            if (!_running && !_placementAllowed) return false;
            if (map == null) return false;
            if (tileX < 0 || tileX >= MapData.Width || tileY < 0 || tileY >= MapData.Height) return false;
            if (map.GetTile(tileX, tileY) != TileType.Buildable) return false;

            var cell = new Vector2Int(tileX, tileY);
            if (_occupiedTiles.Contains(cell)) return false;

            if (unitData == null || unitData.visualMaterial == null)
            {
                Debug.LogWarning("[BattleBridge] PlaceDefenderAs: invalid unitData or missing material.");
                return false;
            }
            // Reject types that are not in the picked pool — keeps Phase 1 draft semantics.
            if (defenderPool != null && defenderPool.Length > 0 && System.Array.IndexOf(defenderPool, unitData) < 0)
            {
                Debug.LogWarning($"[BattleBridge] PlaceDefenderAs rejected {unitData.displayName}: not in picked pool.");
                return false;
            }

            _occupiedTiles.Add(cell);
            GameManager.Instance?.Logger?.RecordPlacement(unitData.displayName, cell, Time.time - _startTime, unitData.cost);

            var entity = _em.CreateEntity();
            // Phase 4: defenders can now take damage from enemy attackers, so
            // they need an IncomingDamage buffer just like attack units have.
            _em.AddBuffer<IncomingDamage>(entity);
            _defenderByTile[cell] = (entity, unitData);
            _em.AddComponentData(entity, new DefenderTile { cell = new int2(tileX, tileY) });
#if UNITY_EDITOR
            _em.SetName(entity, $"Defender_{unitData.displayName}_{tileX}_{tileY}");
#endif
            var pos = new float3(tileX * tileSize, spawnHeight, tileY * tileSize);
            _em.AddComponentData(entity, LocalTransform.FromPosition(pos));
            _em.AddComponent<DefenderUnitTag>(entity);
            _em.AddComponentData(entity, new Health { value = unitData.health, max = unitData.health });
            _em.AddComponentData(entity, new AttackState
            {
                damage = unitData.attackDamage,
                range = unitData.attackRange,
                cooldownDuration = unitData.attackCooldown,
                cooldownRemaining = 0f,
                attackTargetCount = unitData.attackTargetCount,
            });

            // Phase 8 §12: placement pulse VFX (procedural particle ring).
            if (vfxSpawner != null)
                vfxSpawner.SpawnPlacementRing(new Vector3(pos.x, pos.y, pos.z));

            // Phase 8: if the unit has a Spine skeleton configured, spawn a
            // SkeletonAnimation GameObject instead of the billboard RenderMesh.
            // When no skin/skel is set we fall through to the Phase 5 billboard,
            // so Spine rollout is per-unit and doesn't block unconfigured types.
            bool spineSpawned = false;
            if (spineDefenderPool != null)
            {
                var spineWorld = new Vector3(pos.x, pos.y + spineDefenderYOffset, pos.z);
                spineSpawned = spineDefenderPool.TrySpawn(unitData, entity, spineWorld, out _);
            }
            if (!spineSpawned)
            {
                var renderArray = GetOrCreateDefenderRenderMeshArray(unitData);
                var desc = new RenderMeshDescription(
                    shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows: false);
                RenderMeshUtility.AddComponents(entity, _em, desc, renderArray,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            }

            if (unitData.projectile != null)
            {
                var assetIndex = GetOrCreateProjectileAssetIndex(unitData.projectile, unitData.visualMaterial);
                _em.AddComponentData(entity, new ProjectileRef
                {
                    assetIndex = assetIndex,
                    speed = unitData.projectile.speed,
                    hitThreshold = unitData.projectile.hitThreshold,
                    visualScale = unitData.projectile.visualScale,
                });
            }

            CreateHealthBar(entity, yOffset: 0.9f, baseScale: 0.35f);

            // Fixed order: onPlace → synergy recompute → log (PHASE4 §2.5 P4-05).
            // onPlace is a standalone snapshot effect and must fire before the
            // new defender's SynergyBuff is computed, so it never affects itself.
            int onPlaceAffected = ApplyOnPlaceEffect(unitData, cell, entity);
            RecomputeSynergyFor(cell);

            var logger = GameManager.Instance?.Logger;
            if (logger != null)
            {
                if (unitData.onPlaceEffect != OnPlaceEffectType.None)
                {
                    logger.RecordOnPlace(new Logging.OnPlaceUsageLog
                    {
                        unit_type = unitData.displayName,
                        effect = unitData.onPlaceEffect.ToString(),
                        tile = cell,
                        time = Time.time - _startTime,
                        affected_count = onPlaceAffected,
                    });
                }
                logger.SetSynergyStats(_synergyActivations, _synergyPeakCount);
            }

            Debug.Log($"[BattleBridge] Placed {unitData.displayName} at ({tileX},{tileY}).");
            return true;
        }

        private RenderMeshArray GetOrCreateDefenderRenderMeshArray(DefenderUnitData unit)
        {
            if (_defenderRenderCache.TryGetValue(unit, out var cached)) return cached;
            var mesh = unit.visualMesh != null
                ? unit.visualMesh
                : Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var arr = new RenderMeshArray(new[] { unit.visualMaterial }, new[] { mesh });
            _defenderRenderCache[unit] = arr;
            return arr;
        }

        private void OnDestroy()
        {
            if (resultScreen != null)
            {
                resultScreen.RestartRequested -= OnRestartRequested;
                resultScreen.RedraftRequested -= OnRedraftRequested;
            }
            if (_goalEventQueue.IsCreated) _goalEventQueue.Dispose();
            if (_defenderDeathQueue.IsCreated) _defenderDeathQueue.Dispose();
            if (_meteorBurstQueue.IsCreated) _meteorBurstQueue.Dispose();
            if (_defenderAttackQueue.IsCreated) _defenderAttackQueue.Dispose();
            // Phase 9 — guard against editor shutdown / play stop leaking Persistent arrays.
            TeardownFlowField();
            if (_healthBarMaterial != null) Destroy(_healthBarMaterial);
        }

        private void SpawnUnit(SpawnEntry entry)
        {
            if (entry.unitType == null)
            {
                Debug.LogWarning("[BattleBridge] SpawnEntry missing unitType, skipping.");
                return;
            }

            // Phase 9: AttackDeck.SpawnEntry.pathId 는 남아있지만 무시. MapData.SpawnCells[0] 사용.
            // Phase 10 에서 pathId → spawnTileIndex migration + multi-spawn 지원.
            if (map.SpawnCells == null || map.SpawnCells.Count == 0)
            {
                Debug.LogWarning("[BattleBridge] MapData.SpawnCells empty — cannot spawn attacker");
                return;
            }

            if (entry.unitType.visualMaterial == null)
            {
                Debug.LogWarning("[BattleBridge] visualMaterial null — entity will not render.");
                return;
            }

            var entity = _em.CreateEntity();
#if UNITY_EDITOR
            _em.SetName(entity, $"Enemy_{entry.unitType.displayName}");
#endif

            var spawnCell = map.SpawnCells[0];
            var spawnWorldPos = GridToWorldCenter(spawnCell, spawnHeight);
            _em.AddComponentData(entity, LocalTransform.FromPosition(spawnWorldPos));

            _em.AddComponent<AttackUnitTag>(entity);
            _em.AddComponentData(entity, new Health { value = entry.unitType.health, max = entry.unitType.health });
            // Pre-attach an empty IncomingDamage buffer so the Combat system can append without needing
            // to create the buffer on first hit (simplifies ECB usage and keeps archetype stable).
            _em.AddBuffer<IncomingDamage>(entity);

            // Phase 4: when the attack unit has a positive damage, give it an
            // AttackState so AttackSystem's new attacker loop picks it up.
            if (entry.unitType.attackDamage > 0f)
            {
                _em.AddComponentData(entity, new AttackState
                {
                    damage = entry.unitType.attackDamage,
                    range = entry.unitType.attackRange,
                    cooldownDuration = entry.unitType.attackCooldown,
                    cooldownRemaining = 0f,
                });
            }

            _em.AddComponentData(entity, new PathFollowState
            {
                speed = entry.unitType.moveSpeed,
            });

            var renderArray = GetOrCreateRenderMeshArray(entry.unitType);
            var desc = new RenderMeshDescription(
                shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows: false);
            RenderMeshUtility.AddComponents(
                entity, _em, desc, renderArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));

            CreateHealthBar(entity, yOffset: 0.9f, baseScale: 0.35f);
        }

        // Caches one RenderMeshArray per AttackUnitData asset so repeated spawns of the
        // same unit type do not allocate a fresh mesh/material array each time.
        private RenderMeshArray GetOrCreateRenderMeshArray(AttackUnitData unit)
        {
            if (_renderCache.TryGetValue(unit, out var cached))
            {
                return cached;
            }
            var mesh = unit.visualMesh != null
                ? unit.visualMesh
                : Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var arr = new RenderMeshArray(new[] { unit.visualMaterial }, new[] { mesh });
            _renderCache[unit] = arr;
            return arr;
        }
    }
}
