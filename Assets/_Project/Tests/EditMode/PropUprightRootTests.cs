using NUnit.Framework;
using UnityEngine;

namespace Wassup.Tests.EditMode
{
    // prop-upright-root unit 1 — 프레임 flip 불변식 가드(순수 트랜스폼 수학, Play 불필요).
    public class PropUprightRootTests
    {
        [Test]
        public void CounterRotatedRoot_YieldsUprightWorldBasis()
        {
            var parent = new GameObject("Parent90");   // 타일맵 렌더 프레임(90°X)
            var child = new GameObject("PropsRoot");
            try
            {
                parent.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                child.transform.SetParent(parent.transform, false);
                child.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // flip

                Assert.That(Quaternion.Angle(child.transform.rotation, Quaternion.identity), Is.LessThan(0.01f),
                    $"props root should be world-upright, got {child.transform.eulerAngles}");
            }
            finally
            {
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void BlobMigrationFormula_PreservesWorldPosition()
        {
            // 블롭 월드 위치 보존: 기존(부모 90° * p_old) == flip 후(부모 identity + p_new), p_new=(x,-z,y).
            var samples = new[]
            {
                new Vector3(0f, 0f, -0.20f),   // flower
                new Vector3(0f, 0.38f, 0f),    // tree_1x4
                new Vector3(0f, 0.60f, -0.20f),// barrel
                new Vector3(0f, 0.45f, -0.20f),// log
            };
            foreach (var p in samples)
            {
                var oldWorld = Quaternion.Euler(90f, 0f, 0f) * p;      // 회전 루트 아래 월드 오프셋
                var pNew = new Vector3(p.x, -p.z, p.y);               // upright 루트 아래 로컬 = 월드
                Assert.That(Vector3.Distance(oldWorld, pNew), Is.LessThan(1e-4f),
                    $"blob world offset must be preserved for {p}");
            }
        }

        [Test]
        public void BlobMigrationRotation_PreservesWorldOrientation()
        {
            // 기존: 부모 90° * identity 블롭. flip 후: 부모 identity * Euler(90) 블롭. 월드 회전 동일해야.
            var oldWorld = Quaternion.Euler(90f, 0f, 0f) * Quaternion.identity;
            var newWorld = Quaternion.identity * Quaternion.Euler(90f, 0f, 0f);
            Assert.That(Quaternion.Angle(oldWorld, newWorld), Is.LessThan(0.01f));
        }
    }
}
