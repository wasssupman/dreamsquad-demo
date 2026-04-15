using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

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

        private World _world;
        private EntityManager _em;
        private readonly List<SpawnEntry> _pending = new();
        private float _startTime;
        private bool _running;

        public void StartBattle()
        {
            if (deck == null || map == null)
            {
                Debug.LogError("[BattleBridge] deck or map reference missing.", this);
                return;
            }
            _world = World.DefaultGameObjectInjectionWorld;
            _em = _world.EntityManager;
            _pending.Clear();
            _pending.AddRange(deck.spawns);
            _startTime = Time.time;
            _running = true;
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

            var mesh = entry.unitType.visualMesh != null
                ? entry.unitType.visualMesh
                : Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var material = entry.unitType.visualMaterial;
            if (material == null)
            {
                Debug.LogWarning("[BattleBridge] visualMaterial null — entity will not render.");
                return;
            }
            var desc = new RenderMeshDescription(
                shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows: false);
            var matMeshInfo = new RenderMeshArray(new[] { material }, new[] { mesh });
            RenderMeshUtility.AddComponents(
                entity, _em, desc, matMeshInfo,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        }
    }
}
