using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
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
