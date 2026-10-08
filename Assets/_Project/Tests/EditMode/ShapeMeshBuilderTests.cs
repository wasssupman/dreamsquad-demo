using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Somnia.Battle.Data;
using Somnia.Battle.Presentation;

namespace Somnia.Battle.Tests.EditMode
{
    // directional-attack-shape unit 7 — 참격 자국·배치 가이드가 공유하는 도형 메시 빌더의 **기하**를 못박는다.
    //
    // 검증 질문: 빌더가 만든 메시가 sim 판정 도형(`SkillMath.SectorGate/BandGate` 의 점-대상 코어 — 부채꼴은 반경 r·전체각 A,
    // 띠는 along ∈ [0, L]·|across| ≤ hw)과 같은 자리에 있나. 테는 **안쪽**으로 두른다 — 밖으로 나가면 「표기 = 판정」이 테 폭만큼 깨진다.
    // 마크 메시는 채움/테를 `uv.x` 0/1 로 가른다(정점색은 메시 파티클에서 파티클 색 스트림에 덮인다).
    public class ShapeMeshBuilderTests
    {
        private const float Eps = 1e-4f;

        // 테스트가 만든 Mesh 는 에디터 세션에 남는다(리뷰 L-6) — 케이스마다 회수.
        private readonly List<Mesh> _made = new();
        private Mesh NewMesh() { var m = new Mesh(); _made.Add(m); return m; }
        [TearDown] public void TearDown() { foreach (var m in _made) if (m != null) Object.DestroyImmediate(m); _made.Clear(); }

        private static void AssertAll(IReadOnlyList<Vector3> v, System.Func<Vector3, bool> pred, string what)
        {
            for (int i = 0; i < v.Count; i++)
                Assert.IsTrue(pred(v[i]), $"{what}: 정점 {i} = {v[i]}");
        }

        // ── 띠 ──

        [Test]
        public void BandMark_AllVerticesInsideJudgmentBox_AndRimInward()
        {
            float hw = 0.5f, L = 3f, rim = 0.07f;
            var mesh = NewMesh();
            ShapeMeshBuilder.BuildBandMark(mesh, hw, L, rim);
            var v = mesh.vertices; var uv = mesh.uv;
            Assert.AreEqual(uv.Length, v.Length, "정점마다 uv 하나");
            AssertAll(v, p => p.x >= -hw - Eps && p.x <= hw + Eps && p.y >= -Eps && p.y <= L + Eps && Mathf.Abs(p.z) < Eps, "띠 상자 안");
            // 테 정점(uv.x = 1)은 바깥 경계 위 또는 안쪽 — 절대 밖이 아니다. 안쪽 정점은 정확히 rim 만큼 들어간다.
            bool sawInner = false;
            for (int i = 0; i < v.Length; i++)
            {
                if (uv[i].x < 0.5f) continue;
                if (Mathf.Abs(Mathf.Abs(v[i].x) - (hw - rim)) < Eps) sawInner = true;
            }
            Assert.IsTrue(sawInner, "테의 안쪽 정점이 hw − rim 에 있어야 한다");
            Assert.Greater(mesh.triangles.Length, 6, "채움 2 삼각형 + 테가 붙어 있어야 한다");
        }

        [Test]
        public void BandMark_UvSplitsFillAndRim()
        {
            var mesh = NewMesh();
            ShapeMeshBuilder.BuildBandMark(mesh, 0.5f, 3f, 0.07f);
            int fill = 0, rimN = 0;
            foreach (var t in mesh.uv)
            {
                if (Mathf.Approximately(t.x, 0f)) fill++;
                else if (Mathf.Approximately(t.x, 1f)) rimN++;
                else Assert.Fail($"uv.x 는 0 또는 1 이어야 한다: {t.x}");
            }
            Assert.AreEqual(4, fill, "띠 채움은 정점 4");
            Assert.AreEqual(16, rimN, "띠 테는 변 4 × 정점 4");
        }

        // ── 부채꼴 ──

        // 좁은 각일수록 꼭짓점 마이터(`w/sinθ`)가 길어지고 클램프(`Min(…, r)`)가 걸린다 — 이 unit 이 고친 잠복 결함(법선 오프셋 꼭짓점이
        // 각 밖)도 각이 좁을수록 컸다. 정의역 하한(15°) 아래 10° 와 클램프가 확실히 걸리는 조합까지 훑는다(리뷰 M-4).
        [TestCase(10f, 2f)]
        [TestCase(15f, 0.2f)]   // w/sinθ = 0.54 > r 0.2 → 클램프
        [TestCase(20f, 2f)]
        [TestCase(60f, 2f)]
        [TestCase(180f, 2f)]
        public void SectorMark_AllVerticesInsideRadiusAndHalfAngle(float A, float r)
        {
            float rim = 0.07f;
            var mesh = NewMesh();
            ShapeMeshBuilder.BuildSectorMark(mesh, A, r, rim);
            var v = mesh.vertices;
            float half = A * 0.5f;
            AssertAll(v, p => p.magnitude <= r + Eps, "반경 안");
            // +Y 가 중심 방향. 원점은 각이 정의되지 않으니 제외.
            AssertAll(v, p =>
            {
                if (p.sqrMagnitude < Eps) return true;
                float ang = Mathf.Abs(Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg);
                return ang <= half + 0.01f;
            }, "반각 안");
        }

        [Test]
        public void BandMark_VeryShortBand_RimStaysInside()
        {
            // 길이 < 2w · 반폭 < w 에서 안쪽 정점이 뒤집혀 도형 밖으로 나가지 않는다(리뷰 L-3).
            var mesh = NewMesh();
            ShapeMeshBuilder.BuildBandMark(mesh, 0.05f, 0.1f, 0.07f);
            AssertAll(mesh.vertices, p => Mathf.Abs(p.x) <= 0.05f + Eps && p.y >= -Eps && p.y <= 0.1f + Eps, "짧은 띠 안");
        }

        [Test]
        public void SectorMark_UvCountsFollowSegments()
        {
            var mesh = NewMesh();
            ShapeMeshBuilder.BuildSectorMark(mesh, 60f, 2f, 0.07f);
            int fill = 0, rimN = 0;
            foreach (var t in mesh.uv) { if (t.x < 0.5f) fill++; else rimN++; }
            Assert.AreEqual(ShapeMeshBuilder.Segments + 2, fill, "부채꼴 채움 = 꼭짓점 + 호 정점");
            Assert.AreEqual(2 * (ShapeMeshBuilder.Segments + 1) + 8, rimN, "부채꼴 테 = 호 띠 2열 + 가장자리 띠 2×4");
        }

        [Test]
        public void BuildMark_BandWidthZero_FloorsToRimWidth()
        {
            // 폭 0 저작(축 위 몸 걸침만 히트)도 선으로 보이게 — 가이드와 같은 하한.
            var baked = new AttackShapeBaked { kind = AttackShapeBaked.BandKind, halfWidth = 0f };
            var spec = ShapeMarkSpec.FromBaked(in baked, lengthTiles: 3f, cellSize: 2f);
            var mesh = NewMesh();
            ShapeMeshBuilder.BuildMark(mesh, in spec);
            Assert.AreEqual(ShapeMeshBuilder.DefaultRimWidthTiles * 2f, mesh.bounds.max.x, Eps);
        }

        [Test]
        public void SectorMark_180IsHalfPlane_NoVertexBehindOrigin()
        {
            var mesh = NewMesh();
            ShapeMeshBuilder.BuildSectorMark(mesh, 180f, 1.5f, 0.07f);
            AssertAll(mesh.vertices, p => p.y >= -Eps, "반평면 — 원점 뒤 정점 없음");
        }

        [Test]
        public void SectorMark_RimVerticesLieOnOrInsideArc()
        {
            float r = 2f, rim = 0.07f;
            var mesh = NewMesh();
            ShapeMeshBuilder.BuildSectorMark(mesh, 90f, r, rim);
            var v = mesh.vertices; var uv = mesh.uv;
            bool sawInnerArc = false;
            for (int i = 0; i < v.Length; i++)
            {
                if (uv[i].x < 0.5f) continue;
                Assert.LessOrEqual(v[i].magnitude, r + Eps, $"테 정점 {i} 가 호 밖");
                if (Mathf.Abs(v[i].magnitude - (r - rim)) < Eps) sawInnerArc = true;
            }
            Assert.IsTrue(sawInnerArc, "호 띠의 안쪽 정점이 r − rim 에 있어야 한다");
        }

        // ── 가이드 4함수(unit 6 에서 옮김) — 정점 수·기하 회귀 ──

        [Test]
        public void GuideFan_VertexCountAndArcRadius()
        {
            var mesh = NewMesh();
            ShapeMeshBuilder.BuildFan(mesh, 60f, 2f);
            var v = mesh.vertices;
            Assert.AreEqual(ShapeMeshBuilder.Segments + 2, v.Length);
            Assert.AreEqual(Vector3.zero, v[0]);
            for (int i = 1; i < v.Length; i++) Assert.AreEqual(2f, v[i].magnitude, Eps);
            Assert.AreEqual(ShapeMeshBuilder.Segments * 3, mesh.triangles.Length);
            Assert.AreEqual(0, mesh.uv.Length, "가이드 메시는 UV 없음(머티리얼 색으로 그린다)");
        }

        [Test]
        public void GuideBand_ExactQuad()
        {
            var mesh = NewMesh();
            ShapeMeshBuilder.BuildBand(mesh, 0.5f, 3f);
            var v = mesh.vertices;
            Assert.AreEqual(4, v.Length);
            Assert.AreEqual(new Vector3(-0.5f, 0f, 0f), v[0]);
            Assert.AreEqual(new Vector3(0.5f, 0f, 0f), v[1]);
            Assert.AreEqual(new Vector3(-0.5f, 3f, 0f), v[2]);
            Assert.AreEqual(new Vector3(0.5f, 3f, 0f), v[3]);
            CollectionAssert.AreEqual(new[] { 0, 2, 1, 1, 2, 3 }, mesh.triangles);
        }

        [Test]
        public void GuideOutlines_StayInsideShape()
        {
            var fan = NewMesh(); ShapeMeshBuilder.BuildFanOutline(fan, 60f, 2f, 0.1f);
            AssertAll(fan.vertices, p => p.magnitude <= 2f + Eps && (p.sqrMagnitude < Eps || Mathf.Abs(Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg) <= 30f + 0.01f), "부채꼴 테 안");
            var band = NewMesh(); ShapeMeshBuilder.BuildBandOutline(band, 0.5f, 3f, 0.1f);
            AssertAll(band.vertices, p => Mathf.Abs(p.x) <= 0.5f + Eps && p.y >= -Eps && p.y <= 3f + Eps, "띠 테 안");
        }

        // ── spec → 메시 (브리지가 쓰는 진입점) ──

        [Test]
        public void BuildMark_FromBaked_UsesBakeAngleAndCellSize()
        {
            // bake 60° → sin/cos(30°). cellSize 2 → 반경 (1 + 1) × 2 = 4 월드.
            var baked = new AttackShapeBaked
            {
                kind = AttackShapeBaked.SectorKind, sinHalf = Mathf.Sin(30f * Mathf.Deg2Rad), cosHalf = Mathf.Cos(30f * Mathf.Deg2Rad),
            };
            var spec = ShapeMarkSpec.FromBaked(in baked, lengthTiles: 2f, cellSize: 2f);
            Assert.AreEqual(60f, spec.angleDeg, 1e-3f);
            var mesh = NewMesh();
            ShapeMeshBuilder.BuildMark(mesh, in spec);
            float maxR = 0f;
            foreach (var p in mesh.vertices) maxR = Mathf.Max(maxR, p.magnitude);
            Assert.AreEqual(4f, maxR, Eps);
        }

        [Test]
        public void BuildMark_Band_LengthFollowsRangePlusBody()
        {
            // 이쑤시개: 사거리 2 + 몸 1.0 = 3 → 사거리 4 면 5. 도형 저작(반폭 0.5)은 그대로.
            var baked = new AttackShapeBaked { kind = AttackShapeBaked.BandKind, halfWidth = 0.5f };
            var a = ShapeMarkSpec.FromBaked(in baked, lengthTiles: 3f, cellSize: 1f);
            var b = ShapeMarkSpec.FromBaked(in baked, lengthTiles: 5f, cellSize: 1f);
            var ma = NewMesh(); ShapeMeshBuilder.BuildMark(ma, in a);
            var mb = NewMesh(); ShapeMeshBuilder.BuildMark(mb, in b);
            Assert.AreEqual(3f, ma.bounds.max.y, Eps);
            Assert.AreEqual(5f, mb.bounds.max.y, Eps);
            Assert.AreEqual(ma.bounds.max.x, mb.bounds.max.x, Eps, "폭은 사거리에 무관");
            Assert.AreNotEqual(a, b, "길이가 다르면 캐시 키가 다르다");
            Assert.AreEqual(a, ShapeMarkSpec.FromBaked(in baked, 3f, 1f), "같은 값이면 같은 키");
        }
    }
}
