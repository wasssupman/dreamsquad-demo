using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;
using Wassup.UI;

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

        private World _world;
        private EntityManager _em;
        private readonly List<SpawnEntry> _pending = new();
        private readonly Dictionary<AttackUnitData, RenderMeshArray> _renderCache = new();
        private readonly Dictionary<DefenderUnitData, RenderMeshArray> _defenderRenderCache = new();
        private readonly HashSet<Vector2Int> _occupiedTiles = new();
        private float _startTime;
        private bool _running;
        private int _goalReachedCount;
        private NativeQueue<GoalReachedEvent> _goalEventQueue;

        public void StartBattle()
        {
            if (deck == null || map == null)
            {
                Debug.LogError("[BattleBridge] deck or map reference missing.", this);
                return;
            }
            // Re-acquire the world reference on every StartBattle so that Play-Stop-Play
            // cycles (which recreate World.DefaultGameObjectInjectionWorld) are safe.
            _world = World.DefaultGameObjectInjectionWorld;
            _em = _world.EntityManager;
            _pending.Clear();
            _pending.AddRange(deck.spawns);
            _occupiedTiles.Clear();
            _startTime = Time.time;
            _goalReachedCount = 0;
            _running = true;

            // Create the shared queue and inject the singleton so ECS systems can enqueue events.
            if (_goalEventQueue.IsCreated) _goalEventQueue.Dispose();
            _goalEventQueue = new NativeQueue<GoalReachedEvent>(Allocator.Persistent);
            var singletonEntity = _em.CreateEntity();
            _em.AddComponentData(singletonEntity, new GoalReachedEventsSingleton { queue = _goalEventQueue });

            Debug.Log($"[BattleBridge] Battle started with deck '{deck.deckId}' ({deck.spawns.Count} spawns queued).");
        }

        public void StopBattle()
        {
            _running = false;
            // Phase 0: entities persist until play mode exit. P0-09 will add full teardown.
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

            DrainGoalEvents();
        }

        private void DrainGoalEvents()
        {
            if (!_goalEventQueue.IsCreated) return;
            while (_goalEventQueue.TryDequeue(out _))
            {
                _goalReachedCount++;
                Debug.Log($"[BattleBridge] Goal reached! Count: {_goalReachedCount}/{deck.defeatGoalReachedCount}");
                if (_goalReachedCount >= deck.defeatGoalReachedCount)
                {
                    _running = false;
                    resultScreen?.ShowDefeat();
                    Debug.Log("[BattleBridge] DEFEAT triggered.");
                    return;
                }
            }
        }

        // Returns true if a defender was placed, false if tile is occupied or invalid.
        public bool PlaceDefender(int tileX, int tileY)
        {
            if (!_running) return false;
            if (map == null || map.GetTile(tileX, tileY) != TileType.Buildable) return false;

            var cell = new Vector2Int(tileX, tileY);
            if (_occupiedTiles.Contains(cell)) return false;

            if (defenderPool == null || defenderPool.Length == 0)
            {
                Debug.LogWarning("[BattleBridge] defenderPool is empty — cannot place defender.");
                return false;
            }

            var unitData = defenderPool[UnityEngine.Random.Range(0, defenderPool.Length)];
            if (unitData == null || unitData.visualMaterial == null)
            {
                Debug.LogWarning("[BattleBridge] Selected defender has no visualMaterial.");
                return false;
            }

            _occupiedTiles.Add(cell);

            var entity = _em.CreateEntity();
#if UNITY_EDITOR
            _em.SetName(entity, $"Defender_{unitData.displayName}_{tileX}_{tileY}");
#endif
            var pos = new float3(tileX * tileSize, spawnHeight, tileY * tileSize);
            _em.AddComponentData(entity, LocalTransform.FromPosition(pos));
            _em.AddComponent<DefenderUnitTag>(entity);

            var renderArray = GetOrCreateDefenderRenderMeshArray(unitData);
            var desc = new RenderMeshDescription(
                shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows: false);
            RenderMeshUtility.AddComponents(entity, _em, desc, renderArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));

            Debug.Log($"[BattleBridge] Placed {unitData.displayName} at ({tileX},{tileY}).");
            return true;
        }

        private RenderMeshArray GetOrCreateDefenderRenderMeshArray(DefenderUnitData unit)
        {
            if (_defenderRenderCache.TryGetValue(unit, out var cached)) return cached;
            var mesh = unit.visualMesh != null
                ? unit.visualMesh
                : Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var arr = new RenderMeshArray(new[] { unit.visualMaterial }, new[] { mesh });
            _defenderRenderCache[unit] = arr;
            return arr;
        }

        private void OnDestroy()
        {
            if (_goalEventQueue.IsCreated) _goalEventQueue.Dispose();
        }

        private void SpawnUnit(SpawnEntry entry)
        {
            if (entry.unitType == null)
            {
                Debug.LogWarning("[BattleBridge] SpawnEntry missing unitType, skipping.");
                return;
            }

            PathDefinition path = null;
            foreach (var p in map.Paths)
            {
                if (p.id == entry.pathId) { path = p; break; }
            }
            if (path == null || path.waypoints.Count == 0)
            {
                Debug.LogWarning($"[BattleBridge] Path '{entry.pathId}' not found or empty in MapData.");
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

            var startCell = path.waypoints[0];
            var pos = new float3(startCell.x * tileSize, spawnHeight, startCell.y * tileSize);
            _em.AddComponentData(entity, LocalTransform.FromPosition(pos));

            _em.AddComponent<AttackUnitTag>(entity);
            _em.AddComponentData(entity, new Health { value = entry.unitType.health, max = entry.unitType.health });

            _em.AddComponentData(entity, new PathFollowState
            {
                currentWaypointIndex = 1,
                speed = entry.unitType.moveSpeed,
                tileSize = tileSize,
            });
            var buffer = _em.AddBuffer<PathWaypoint>(entity);
            foreach (var wp in path.waypoints)
            {
                buffer.Add(new PathWaypoint { cell = new int2(wp.x, wp.y) });
            }

            var renderArray = GetOrCreateRenderMeshArray(entry.unitType);
            var desc = new RenderMeshDescription(
                shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows: false);
            RenderMeshUtility.AddComponents(
                entity, _em, desc, renderArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
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
                : Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var arr = new RenderMeshArray(new[] { unit.visualMaterial }, new[] { mesh });
            _renderCache[unit] = arr;
            return arr;
        }
    }
}
