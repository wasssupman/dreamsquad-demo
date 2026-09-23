using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 3 — 옛 순수 테스트의 후계.
    //
    // 적응한 것: `NativeArray` → 배열 + count · `Entity` → `SimEntityId` ·
    // `DynamicBuffer<ShieldSlot>` → `List<ShieldSlot>`. **단언의 뜻은 그대로다** —
    // 옛 테스트가 고정하던 것은 자료구조가 아니라 규칙이었다.
    public class CombatPureMathTests
    {
        // ── 타겟 랭킹 ────────────────────────────────────────────────────────

        [Test]
        public void 최전방은_골까지_남은_거리로_정렬한다()
        {
            var cands = new[]
            {
                new FrontmostTargeting.Candidate { FlowDist = 9, SqDist = 1f, SimId = 1 },
                new FrontmostTargeting.Candidate { FlowDist = 3, SqDist = 99f, SimId = 2 },
            };
            // 멀리 있어도 골에 더 가까우면 그쪽이 「앞」이다.
            Assert.AreEqual(1, FrontmostTargeting.SelectFrontmost(cands, 2));
        }

        [Test]
        public void 최전방은_도달_불가를_건너뛴다()
        {
            var cands = new[]
            {
                new FrontmostTargeting.Candidate
                { FlowDist = FrontmostTargeting.UnreachableDist, SqDist = 0f, SimId = 1 },
                new FrontmostTargeting.Candidate { FlowDist = 100, SqDist = 50f, SimId = 2 },
            };
            Assert.AreEqual(1, FrontmostTargeting.SelectFrontmost(cands, 2));
            Assert.AreEqual(-1, FrontmostTargeting.SelectFrontmost(cands, 1), "전부 도달 불가면 -1");
        }

        [Test]
        public void 동률은_먼저_스폰된_쪽이_이긴다()
        {
            var a = new FrontmostTargeting.Candidate { FlowDist = 5, SqDist = 2f, SimId = 7 };
            var b = new FrontmostTargeting.Candidate { FlowDist = 5, SqDist = 2f, SimId = 3 };
            Assert.IsFalse(FrontmostTargeting.RanksBefore(in a, in b));
            Assert.IsTrue(FrontmostTargeting.RanksBefore(in b, in a));
        }

        [Test]
        public void 힐러는_가장_다친_아군을_고른다()
        {
            var cands = new[]
            {
                new LowestHealthTargeting.Candidate { HpRatio = 1f, SqDist = 0f, SimId = 1 },
                new LowestHealthTargeting.Candidate { HpRatio = 0.2f, SqDist = 99f, SimId = 2 },
            };
            Assert.AreEqual(1, LowestHealthTargeting.SelectLowest(cands, 2));
        }

        [Test]
        public void 폭탄맨의_사각_자는_반경_0_이면_고르지_않는다()
        {
            // C20 — 이름은 폴백인데 그 아키타입의 유일 경로다. 「0 = 선정 없음」이 이 함수의
            // 계약이고, 그 해석이 호출처마다 갈리면 조용히 엉뚱한 대상이 뽑힌다.
            var cands = new[]
            {
                new NearestTargeting.Candidate { Eligible = true, TileDist = 0, SqDist = 0f, SimId = 1 },
            };
            Assert.AreEqual(-1, NearestTargeting.SelectNearest(cands, 1, 0));
            Assert.AreEqual(0, NearestTargeting.SelectNearest(cands, 1, 1));
        }

        [Test]
        public void 히스테리시스는_유지만_넓힌다()
        {
            // 획득 `gap ≤ N`, 유지 `gap ≤ N + h`. 이동 정지에 유지 임계를 쓰면
            // **적이 사거리 밖에서 멈춘다** — 그래서 두 술어를 가른다.
            var atk = new float3(0f, 0f, 0f);
            var tgt = new float3(2.05f, 0f, 0f);   // 사거리 2 · 몸 0 → 획득 밖, 유지 안
            Assert.IsFalse(AttackReach.InReach(atk, tgt, 2f, 1f, 0f, 0f));
            Assert.IsTrue(TargetPersistence.KeepsLock(true, atk, tgt, 2f, 1f, 0f, 0f));
        }

        [Test]
        public void 자는_하나다()
        {
            // unit 2 의 감지 유지 폭이 unit 3 의 공격 락과 **같은 상수**여야 한다.
            // 같은 종류의 진동을 막는 데 두 개의 자를 두지 않는다.
            Assert.AreEqual(TargetPersistence.HysteresisTiles, AiMovePhase.HysteresisTiles);
        }

        // ── 킬 귀속 ──────────────────────────────────────────────────────────

        [Test]
        public void 킬러는_그_틱_최대_피해_출처다()
        {
            SimEntityId best = SimEntityId.None;
            float amount = 0f;
            KillAttribution.Consider(10f, new SimEntityId(1), ref best, ref amount);
            KillAttribution.Consider(30f, new SimEntityId(2), ref best, ref amount);
            KillAttribution.Consider(20f, new SimEntityId(3), ref best, ref amount);
            Assert.AreEqual(2, best.Value);
        }

        [Test]
        public void 동점이면_먼저_접힌_쪽이_유지된다()
        {
            SimEntityId best = SimEntityId.None;
            float amount = 0f;
            KillAttribution.Consider(30f, new SimEntityId(5), ref best, ref amount);
            KillAttribution.Consider(30f, new SimEntityId(9), ref best, ref amount);
            Assert.AreEqual(5, best.Value, "strict > 라 버퍼 순서 앞이 이긴다");
        }

        [Test]
        public void 출처_없는_피해는_미귀속이다()
        {
            // 지속 피해·배치 스킬·환경·자해는 처치 보상을 내지 않는다(의도).
            SimEntityId best = SimEntityId.None;
            float amount = 0f;
            KillAttribution.Consider(999f, SimEntityId.None, ref best, ref amount);
            Assert.IsTrue(best.IsNone);
        }

        // ── 실드 ─────────────────────────────────────────────────────────────

        [Test]
        public void 같은_출처는_max_다른_출처는_합이다()
        {
            var slots = new List<ShieldSlot>();
            ShieldMath.Merge(slots, new SimEntityId(1), 30f);
            ShieldMath.Merge(slots, new SimEntityId(1), 10f);   // 중첩 불가 — max
            ShieldMath.Merge(slots, new SimEntityId(2), 20f);
            Assert.AreEqual(2, slots.Count);
            Assert.AreEqual(50f, ShieldMath.Sum(slots), 1e-4f);
            Assert.AreEqual(30f, ShieldMath.ValueFromSource(slots, new SimEntityId(1)), 1e-4f);
        }

        [Test]
        public void 흡수는_오래된_슬롯부터다()
        {
            var slots = new List<ShieldSlot>();
            ShieldMath.Merge(slots, new SimEntityId(1), 10f);
            ShieldMath.Merge(slots, new SimEntityId(2), 10f);
            float pierce = ShieldMath.Absorb(slots, 15f);
            Assert.AreEqual(0f, pierce, 1e-4f, "합보다 작으면 관통 0");
            Assert.AreEqual(1, slots.Count, "앞 슬롯이 소진돼 제거된다");
            Assert.AreEqual(2, slots[0].Source.Value);
            Assert.AreEqual(5f, slots[0].Value, 1e-4f);
        }

        [Test]
        public void 완전_흡수는_피격이_아니다()
        {
            var slots = new List<ShieldSlot>();
            ShieldMath.Merge(slots, new SimEntityId(1), 100f);
            Assert.AreEqual(0f, ShieldMath.Absorb(slots, 40f), 1e-4f);
            Assert.AreEqual(60f, ShieldMath.Sum(slots), 1e-4f);
        }

        // ── 광역 ─────────────────────────────────────────────────────────────

        [Test]
        public void 반경_1_광역은_대각을_잃지_않는다()
        {
            // 순수 원(`dx²+dy² ≤ r²`)으로 쓰면 대각이 통째로 빠져 십자가 된다(1.41 > 1).
            var center = new int2(5, 5);
            Assert.IsTrue(TileAoe.IsInRadius(new int2(6, 6), center, 1));
            Assert.IsTrue(TileAoe.IsInRadius(new int2(4, 4), center, 1));
            Assert.IsFalse(TileAoe.IsInRadius(new int2(7, 5), center, 1));
        }

        [Test]
        public void 큰_몸은_폭발에_더_잘_걸린다()
        {
            var center = new int2(0, 0);
            Assert.IsFalse(TileAoe.IsInRadius(new int2(2, 0), center, 1));
            Assert.IsTrue(TileAoe.IsInRadius(new int2(2, 0), center, 1, 0.5f));
        }

        [Test]
        public void 광역_상한은_가까운_순이고_동률은_앞_인덱스다()
        {
            var dist = new[] { 9f, 1f, 1f, 4f };
            var into = new int[4];
            int n = AoeTargetCap.SelectNearest(dist, 4, 2, into);
            Assert.AreEqual(2, n);
            Assert.AreEqual(1, into[0]);
            Assert.AreEqual(2, into[1], "동률은 앞 인덱스가 이긴다(결정론)");

            n = AoeTargetCap.SelectNearest(dist, 4, 0, into);
            Assert.AreEqual(4, n, "cap 0 = 무제한");
        }

        // ── 어그로 선정 ──────────────────────────────────────────────────────

        [Test]
        public void 가디언은_아직_안_물린_적부터_때린다()
        {
            var cands = new[]
            {
                new AggroCandidate { Pos = new float3(1f, 0f, 0f), BodyRadius = 0.25f, Aggroed = true },
                new AggroCandidate { Pos = new float3(2f, 0f, 0f), BodyRadius = 0.25f, Aggroed = false },
            };
            var outIdx = new int[1];
            int n = AggroTargeting.SelectTargets(float3.zero, 3f, 1f, 0.5f,
                                                 held: 0, capacity: 2, shape: default,
                                                 cands, 2, outIdx, 1);
            Assert.AreEqual(1, n);
            Assert.AreEqual(1, outIdx[0], "여유가 있으면 더 멀어도 안 물린 쪽을 먼저 문다");
        }

        [Test]
        public void 상한이_차면_겹친_팩을_정리한다()
        {
            var cands = new[]
            {
                new AggroCandidate { Pos = new float3(1f, 0f, 0f), BodyRadius = 0.25f, Aggroed = true },
                new AggroCandidate { Pos = new float3(2f, 0f, 0f), BodyRadius = 0.25f, Aggroed = false },
            };
            var outIdx = new int[1];
            int n = AggroTargeting.SelectTargets(float3.zero, 3f, 1f, 0.5f,
                                                 held: 2, capacity: 2, shape: default,
                                                 cands, 2, outIdx, 1);
            Assert.AreEqual(1, n);
            Assert.AreEqual(0, outIdx[0], "상한이 차면 일반 최근접");
        }

        [Test]
        public void 가디언_선정은_자기_몸을_센다()
        {
            // unit 22 의 회귀: 선정이 「공격자는 한 칸(0.5)」을 상수로 박으면 몸 큰 가디언이
            // **휘두르는데 피해 0** 이 된다. 게이트와 같은 본체를 지나야 한다.
            var cands = new[]
            {
                new AggroCandidate { Pos = new float3(2.6f, 0f, 0f), BodyRadius = 0.25f, Aggroed = false },
            };
            var outIdx = new int[1];
            int wide = AggroTargeting.SelectTargets(float3.zero, 1f, 1f, selfBodyRadius: 1.5f,
                                                    held: 0, capacity: 1, shape: default,
                                                    cands, 1, outIdx, 1);
            int narrow = AggroTargeting.SelectTargets(float3.zero, 1f, 1f, selfBodyRadius: 0.5f,
                                                      held: 0, capacity: 1, shape: default,
                                                      cands, 1, outIdx, 1);
            Assert.AreEqual(1, wide, "사거리 1 + 내 몸 1.5 + 상대 0.25 = 2.75 → 닿는다");
            Assert.AreEqual(0, narrow, "몸이 작으면 같은 후보가 밖이다");
        }
    }
}
