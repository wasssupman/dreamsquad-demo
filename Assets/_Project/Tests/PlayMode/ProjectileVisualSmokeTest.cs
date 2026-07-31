using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Combat.Projectile;
using Wassup.Core;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Tests.PlayMode
{
    public class ProjectileVisualSmokeTest
    {
        private GameObject _poolGo;
        private ProjectileViewPool _pool;

        [SetUp]
        public void SetUp()
        {
            _poolGo = new GameObject("TestProjectileViewPool");
            _pool = _poolGo.AddComponent<ProjectileViewPool>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_poolGo != null) Object.DestroyImmediate(_poolGo);
        }

        [UnityTest]
        public IEnumerator HitPlayback_ReturnsToPool()
        {
            var hitPrefab = MakeDummyParticlePrefab(lifetime: 0.15f);

            Assert.AreEqual(0, _pool.ActiveCount);
            _pool.PlayHit(hitPrefab, float3.zero);

            // Wait longer than particle lifetime + small buffer
            yield return new WaitForSeconds(0.4f);

            // After lifetime the coroutine should have fired and returned the view
            Assert.AreEqual(0, _pool.ActiveCount);

            Object.DestroyImmediate(hitPrefab);
        }

        [Test]
        public void LaunchAnchor_IsKeptForSpawnAndFirstSync_ThenFollowsSimPath()
        {
            var prefab = new GameObject("DummyProjectile");
            var data = ScriptableObject.CreateInstance<ProjectileData>();
            data.projectilePrefab = prefab;
            data.visualScale = 1f;
            data.visualHeightOffset = 0f;
            var anchor = new Vector3(7f, 3f, -2f);
            var entity = new Entity { Index = 100, Version = 1 };

            _pool.Spawn(entity, data, float3.zero, 0f, true, anchor);
            var view = _poolGo.transform.GetChild(0);
            Assert.AreEqual(anchor, view.position, "trail/particle reset은 weapon/body anchor에서 시작");

            var frame = new ProjectileViewFrame
            {
                simPosition = new float3(2f, 0f, 3f),
                hasState = true,
                movement = MovementKind.DirectionalLinear,
            };
            _pool.SyncTransform(entity, frame);
            Assert.AreEqual(anchor, view.position,
                "spawn과 같은 Bridge frame의 sync가 launch anchor를 덮으면 안 된다");

            _pool.SyncTransform(entity, frame);
            Assert.AreEqual((Vector3)BoardSpace.ToView(frame.simPosition), view.position,
                "다음 sync부터 기존 sim→view 궤적을 따라야 한다");

            Object.DestroyImmediate(data);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void SpawnWithoutAnchor_UsesExistingProjectedPositionImmediately()
        {
            var prefab = new GameObject("DummyProjectile_NoAnchor");
            var data = ScriptableObject.CreateInstance<ProjectileData>();
            data.projectilePrefab = prefab;
            data.visualScale = 1f;
            data.visualHeightOffset = 0f;
            var simPosition = new float3(4f, 0f, 6f);
            var entity = new Entity { Index = 101, Version = 1 };

            _pool.Spawn(entity, data, simPosition);
            var view = _poolGo.transform.GetChild(0);

            Assert.AreEqual((Vector3)BoardSpace.ToView(simPosition), view.position);

            Object.DestroyImmediate(data);
            Object.DestroyImmediate(prefab);
        }

        private static GameObject MakeDummyParticlePrefab(float lifetime)
        {
            var go = new GameObject("DummyParticle");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = lifetime;
            main.loop = false;
            main.startLifetime = lifetime;
            return go;
        }
    }
}
