using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore.Combat.Emission;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 3 — 발사 명세(패턴)의 순수 규칙.
    public class PatternEmissionTests
    {
        private static float[] Intervals(params float[] v) => v;

        // ── 스케줄 ───────────────────────────────────────────────────────────

        [Test]
        public void 시작_틱에_첫_발이_나간다()
        {
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, 3, 0);
            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 1f / 60f, Intervals(0f, 0.2f, 0.2f)));
        }

        [Test]
        public void 간격_0_이_이어지면_같은_틱에_전부_나간다()
        {
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, 4, 0);
            Assert.AreEqual(4, EmitterTick.Advance(ref rt, 1f / 60f, Intervals(0f, 0f, 0f, 0f)));
            Assert.IsTrue(EmitterTick.IsComplete(in rt));
        }

        [Test]
        public void 잔여_이월로_드리프트가_0_이다()
        {
            // 간격 0.1초 × 5발을 1/60 틱으로 돌리면 정확히 5발이 나간다.
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, 5, 0);
            int fired = 0;
            for (int t = 0; t < 60 && !EmitterTick.IsComplete(in rt); t++)
                fired += EmitterTick.Advance(ref rt, 1f / 60f, Intervals(0f, .1f, .1f, .1f, .1f));
            Assert.AreEqual(5, fired);
        }

        [Test]
        public void 버스트_길이는_첫_간격을_무시한다()
        {
            // 「다음 트리거는 마지막 탄이 나간 뒤부터 기다린다」의 값이다.
            Assert.AreEqual(0.6f, EmitterTick.TotalDuration(Intervals(9f, .2f, .2f, .2f)), 1e-4f);
        }

        // ── 선정 ─────────────────────────────────────────────────────────────

        private static (float2[] xz, int[] ids) Pool(params (float x, float z, int id)[] rows)
        {
            var xz = new float2[rows.Length];
            var ids = new int[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                xz[i] = new float2(rows[i].x, rows[i].z);
                ids[i] = rows[i].id;
            }
            return (xz, ids);
        }

        [Test]
        public void 순회_선정의_순위축은_SimEntityId_다()
        {
            // 스냅샷 순서에 기대면 같은 인덱스가 틱마다 다른 대상을 가리킨다.
            // 배열 순서를 뒤집어도 같은 순번이 같은 id 를 고른다.
            var a = Pool((0f, 0f, 7), (1f, 0f, 3));
            var b = Pool((1f, 0f, 3), (0f, 0f, 7));

            int pa = PatternTargeting.Select(a.xz, a.ids, 2, PatternSelectionRule.RoundRobin, 0, float2.zero);
            int pb = PatternTargeting.Select(b.xz, b.ids, 2, PatternSelectionRule.RoundRobin, 0, float2.zero);
            Assert.AreEqual(3, a.ids[pa]);
            Assert.AreEqual(3, b.ids[pb]);
        }

        [Test]
        public void 발사_카운터가_0_에_고정되면_같은_순위만_고른다()
        {
            // 그래서 카운터는 durable 소유자(슬롯)가 든다 — 인스턴스는 트리거마다 사라진다.
            var p = Pool((0f, 0f, 1), (1f, 0f, 2), (2f, 0f, 3));
            int first = PatternTargeting.Select(p.xz, p.ids, 3, PatternSelectionRule.RoundRobin, 0, float2.zero);
            int again = PatternTargeting.Select(p.xz, p.ids, 3, PatternSelectionRule.RoundRobin, 0, float2.zero);
            int next = PatternTargeting.Select(p.xz, p.ids, 3, PatternSelectionRule.RoundRobin, 1, float2.zero);
            Assert.AreEqual(first, again);
            Assert.AreNotEqual(first, next);
        }

        [Test]
        public void 최근접_동률은_낮은_id_가_이긴다()
        {
            var p = Pool((1f, 0f, 9), (1f, 0f, 4));
            int pick = PatternTargeting.Select(p.xz, p.ids, 2, PatternSelectionRule.Nearest, 0, float2.zero);
            Assert.AreEqual(4, p.ids[pick]);
        }

        [Test]
        public void 후보가_없으면_발사를_소비하고_건너뛴다()
        {
            var p = Pool();
            Assert.AreEqual(-1, PatternTargeting.Select(p.xz, p.ids, 0,
                                                        PatternSelectionRule.RoundRobin, 0, float2.zero));
        }

        [Test]
        public void 탄막_씨앗은_사수와_발사_카운터에서_나온다()
        {
            Assert.AreEqual(PatternTargeting.ShotSeed(5, 3), PatternTargeting.ShotSeed(5, 3));
            Assert.AreNotEqual(PatternTargeting.ShotSeed(5, 3), PatternTargeting.ShotSeed(5, 4));
            Assert.AreNotEqual(PatternTargeting.ShotSeed(5, 3), PatternTargeting.ShotSeed(6, 3));
        }

        // ── 반경 게이트 ──────────────────────────────────────────────────────

        [Test]
        public void 반경_0_은_전량_통과다()
        {
            var xz = new[] { new float2(99f, 0f) };
            var body = new[] { 0f };
            var outIdx = new int[1];
            Assert.AreEqual(1, PatternScope.FilterByReach(xz, body, 1, float2.zero, 0f, 0.5f, outIdx));
        }

        [Test]
        public void 반경_게이트는_원본_인덱스를_돌려준다()
        {
            // 지역 인덱스를 밖으로 흘리면 잠금 경로가 다른 인덱스 공간을 섞어 엉뚱한 칸을 때린다.
            var xz = new[] { new float2(99f, 0f), new float2(1f, 0f), new float2(98f, 0f) };
            var body = new[] { 0f, 0f, 0f };
            var outIdx = new int[3];
            int n = PatternScope.FilterByReach(xz, body, 3, float2.zero, 3f, 0.5f, outIdx);
            Assert.AreEqual(1, n);
            Assert.AreEqual(1, outIdx[0]);
        }

        // ── 방향·난수 ────────────────────────────────────────────────────────

        [Test]
        public void 방향은_저작_각도_사이를_보간한다()
        {
            var d0 = PatternDirection.Resolve(new float2(1f, 0f), -90f, 90f, 0f);
            var d1 = PatternDirection.Resolve(new float2(1f, 0f), -90f, 90f, 1f);
            Assert.AreEqual(-1f, d0.y, 1e-3f);
            Assert.AreEqual(1f, d1.y, 1e-3f);
        }

        [Test]
        public void 같은_씨앗에는_같은_N발이다()
        {
            var d1 = new float[4]; var i1 = new float[4];
            var d2 = new float[4]; var i2 = new float[4];
            PatternShotRandomizer.Apply(d1, i1, true, 0.1f, 0.3f, 12345u);
            PatternShotRandomizer.Apply(d2, i2, true, 0.1f, 0.3f, 12345u);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(d1[i], d2[i], 1e-6f);
                Assert.AreEqual(i1[i], i2[i], 1e-6f);
            }
            Assert.AreEqual(0f, i1[0], 1e-6f, "첫 발의 간격은 계약상 0 이다");
        }

        [Test]
        public void 난수_저작이_아니면_저작값을_안_건드린다()
        {
            var d = new[] { 0.25f, 0.5f };
            var i = new[] { 0f, 0.7f };
            PatternShotRandomizer.Apply(d, i, false, 0.1f, 0.3f, 1u);
            Assert.AreEqual(0.25f, d[0], 1e-6f);
            Assert.AreEqual(0.7f, i[1], 1e-6f);
        }
    }
}
