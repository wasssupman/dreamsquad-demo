using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Combat.Projectile;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 3 — 궤적·착탄 수학. 옛 순수 테스트의 후계이고, 단언의 대부분은
    // **실패에서 나온 문장**이다(발사점 뒤로 날아가는 부메랑 · 첫 바퀴 뒤 장식이 되는 궤도 …).
    public class ProjectileMathTests
    {
        // ── 궤적 ─────────────────────────────────────────────────────────────

        [Test]
        public void 포물선은_양_끝에서_융기가_0_이다()
        {
            var a = BallisticArc.ArcPosition(float3.zero, new float3(4f, 0f, 0f), 3f, 0f);
            var b = BallisticArc.ArcPosition(float3.zero, new float3(4f, 0f, 0f), 3f, 1f);
            var mid = BallisticArc.ArcPosition(float3.zero, new float3(4f, 0f, 0f), 3f, 0.5f);
            Assert.AreEqual(0f, a.y, 1e-4f);
            Assert.AreEqual(0f, b.y, 1e-4f);
            Assert.AreEqual(3f, mid.y, 1e-4f);
        }

        [Test]
        public void 비행_시간은_최소값_바닥을_갖는다()
        {
            // 코앞 사격이 첫 틱에 끝나면 궤적이 보이지 않는다.
            float t = BallisticArc.FlightTime(float3.zero, new float3(0.01f, 0f, 0f), 10f, 0.3f);
            Assert.AreEqual(0.3f, t, 1e-4f);
        }

        [Test]
        public void 예고_0_은_첫_틱에_도착이다()
        {
            Assert.IsTrue(SkyFall.Arrived(0f, 0f));
            Assert.AreEqual(1f, SkyFall.Progress(0f, 0f), 1e-4f);
            Assert.IsFalse(SkyFall.Arrived(0.2f, 0.5f));
        }

        [Test]
        public void 부메랑은_발사점_뒤로_가지_않는다()
        {
            // 완료 틱에는 진행량이 음수로 내려간다(오버슛). 접지 않으면 마지막 스윕 선분이
            // 발사점 **뒤**로 뻗어 뒤에 서 있던 대상을 때린다 — 계약에 없는 피해 사건이다.
            var axis = new float2(1f, 0f);
            var pos = Boomerang.Position(float3.zero, axis, maxDistance: 4f, speed: 4f,
                                         elapsed: 3f, out bool returning);
            Assert.IsTrue(returning);
            Assert.AreEqual(0f, pos.x, 1e-4f, "0 으로 접힌다");
        }

        [Test]
        public void 부메랑_완료는_누적_시간으로_본다()
        {
            Assert.IsFalse(Boomerang.IsComplete(4f, 4f, 1.9f));
            Assert.IsTrue(Boomerang.IsComplete(4f, 4f, 2f));
            Assert.AreEqual(2f, Boomerang.TotalTime(4f, 4f), 1e-4f);
        }

        [Test]
        public void 궤도_접선은_부호만_남긴다()
        {
            // 각속도 0(저작 실수로 멈춘 궤도)에서도 단위 벡터가 나와야 한다 — 0 벡터를 남기면
            // 앞선 대상 정렬이 조용히 무의미해진다.
            var t = Orbit.Tangent(0f, 0f);
            Assert.AreEqual(1f, math.length(t), 1e-4f);
            var fwd = Orbit.Tangent(1f, 0f);
            var back = Orbit.Tangent(-1f, 0f);
            Assert.AreEqual(-1f, math.dot(fwd, back), 1e-4f, "역회전은 반대 방향");
        }

        [Test]
        public void 궤도_위상은_균등_배치다()
        {
            Assert.AreEqual(0f, Orbit.PhaseOf(0, 1), 1e-4f);
            Assert.AreEqual(math.PI, Orbit.PhaseOf(1, 2), 1e-4f);
        }

        [Test]
        public void 베지어_퇴화_입력은_직선으로_붕괴한다()
        {
            // 수직축이 정의되지 않으므로 NaN 을 만들지 않는다.
            Bezier3.ControlPoints(float3.zero, float3.zero, 0, 1f, 0.3f, out var c1, out var c2);
            Assert.IsFalse(float.IsNaN(c1.x) || float.IsNaN(c2.x));
        }

        [Test]
        public void 스윕은_길이_0_선분을_점으로_퇴화시킨다()
        {
            Assert.IsTrue(SweepHitMath.SegmentHits(float2.zero, float2.zero, new float2(0.2f, 0f), 0.5f));
            Assert.IsFalse(SweepHitMath.SegmentHits(float2.zero, float2.zero, new float2(2f, 0f), 0.5f));
        }

        [Test]
        public void 스윕은_지나간_선분을_훑는다()
        {
            // 한 틱에 4타일을 날아도 그 사이에 선 대상을 놓치지 않는다(터널링 방지).
            Assert.IsTrue(SweepHitMath.SegmentHits(float2.zero, new float2(4f, 0f),
                                                   new float2(2f, 0.1f), 0.3f));
        }

        // ── 바인딩 분류 ──────────────────────────────────────────────────────

        [Test]
        public void 궤적_분류는_전수다()
        {
            // C# 은 enum switch 의 전수성을 강제하지 못한다. 새 궤적을 더하면 이 단언이
            // 빨개지고 분류를 갱신하게 된다.
            Assert.AreEqual(MovementBinding.KnownKindCount,
                            Enum.GetValues(typeof(MovementKind)).Length);
        }

        [Test]
        public void 하늘낙하는_칸과_적이_다른_바인딩이다()
        {
            // 같은 그림, **다른 조준**이다. 이 차이가 사는 곳이 바로 이 표다 —
            // 셀 낙하탄에 임자를 실어 흉내내면 한 탄에 조준이 둘이 되어 예고 시간만큼 어긋난다.
            Assert.AreEqual(BindingClass.Cell, MovementBinding.Of(MovementKind.SkyFall));
            Assert.AreEqual(BindingClass.Entity, MovementBinding.Of(MovementKind.SkyFallOnEntity));
        }

        [Test]
        public void 왕복과_직선은_방향_바인딩이다()
        {
            Assert.AreEqual(BindingClass.Direction, MovementBinding.Of(MovementKind.BoomerangReturn));
            Assert.AreEqual(BindingClass.Direction, MovementBinding.Of(MovementKind.DirectionalLinear));
        }

        // ── 재타격 창 ────────────────────────────────────────────────────────

        [Test]
        public void 재타격_0_은_피해자당_영구_1회다()
        {
            var rec = new List<PathHitRecord>();
            Assert.IsTrue(PathHits.CanHit(rec, new SimEntityId(1), 0f, 0f, out int i));
            Assert.AreEqual(-1, i);
            rec.Add(new PathHitRecord { Victim = new SimEntityId(1), NextHitAt = 0f });
            Assert.IsFalse(PathHits.CanHit(rec, new SimEntityId(1), 99f, 0f, out _));
        }

        [Test]
        public void 재타격_창은_시간으로_열린다()
        {
            // 「피해자당 영구 1회」는 궤도 탄을 첫 바퀴 뒤 장식으로 만든다 — 같은 적을 매 바퀴
            // 지나면서 딱 한 번만 때린다. 그래서 기록은 **창**이다.
            var rec = new List<PathHitRecord>
            {
                new PathHitRecord { Victim = new SimEntityId(1), NextHitAt = 2f },
            };
            Assert.IsFalse(PathHits.CanHit(rec, new SimEntityId(1), 1.5f, 1f, out int i));
            Assert.AreEqual(0, i, "슬롯을 알려 줘야 호출부가 덮어쓸 수 있다");
            Assert.IsTrue(PathHits.CanHit(rec, new SimEntityId(1), 2f, 1f, out _));
        }

        // ── 튕김·재조준 ──────────────────────────────────────────────────────

        [Test]
        public void 튕김은_제외한_최근접을_고른다()
        {
            var cands = new[]
            {
                new BounceCandidate { Pos = new float3(1f, 0f, 0f), Faction = 2, BodyRadius = 0.25f },
                new BounceCandidate { Pos = new float3(2f, 0f, 0f), Faction = 2, BodyRadius = 0.25f },
            };
            int pick = BounceRetarget.FindNext(float3.zero, excludeIndex: 0, cands, 2,
                                               attackTargetLayers: 0, wantedFactionMask: 2,
                                               tileRange: 5, tileSize: 1f);
            Assert.AreEqual(1, pick);
        }

        [Test]
        public void 튕김은_진영을_본다()
        {
            var cands = new[]
            {
                new BounceCandidate { Pos = new float3(1f, 0f, 0f), Faction = 1, BodyRadius = 0.25f },
            };
            Assert.AreEqual(-1, BounceRetarget.FindNext(float3.zero, -1, cands, 1, 0, 2, 5, 1f));
            Assert.AreEqual(0, BounceRetarget.FindNext(float3.zero, -1, cands, 1, 0, 1, 5, 1f));
        }

        [Test]
        public void 튕김_반경_0_은_선정_없음이다()
        {
            var cands = new[] { new BounceCandidate { Pos = float3.zero, Faction = 2 } };
            Assert.AreEqual(-1, BounceRetarget.FindNext(float3.zero, -1, cands, 1, 0, 2, 0, 1f));
        }
    }
}
