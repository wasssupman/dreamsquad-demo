using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.BattleCoreUnity;
using OldReach = Wassup.Battle.Combat.AttackReach;
using NewReach = Wassup.BattleCore.Combat.AttackReach;
using OldAggro = Wassup.Battle.Combat.AggroTargeting;
using NewAggro = Wassup.BattleCore.Combat.AggroTargeting;
using OldShape = Wassup.Data.AttackShapeBaked;
using NewShape = Wassup.BattleCore.Combat.AttackShapeBaked;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild — 옛 전투(`Wassup.Battle.Combat.AttackReach`)와 새 코어
    // (`Wassup.BattleCore.Combat.AttackReach`)의 「닿나」 판정이 **같은 입력에 같은 답**을 내는지 고정한다.
    //
    // 도형은 **저작 → bake → 코어 사상** 경로 전체를 지난다: 옛 전투는 `AttackShapeBake.From` 결과를 그대로
    // 쓰고, 새 층은 그 결과를 `CombatDefinitionBuilder.ToCoreShapeKind` + 필드 복사로 옮긴다(빌더 `Bake`).
    // 그래서 이 테스트가 초록이면 「같은 저작 도형 → 같은 판정」이 bake 사상까지 포함해 성립한다.
    //
    // ⚠ 불일치가 나면 코어를 이 테스트에 맞춰 «고치지» 말 것 — 어느 쪽이 규칙인지는 사용자가 정한다.
    public class AttackReachParityTests
    {
        private const int Seed = 20260924;
        private const int Cases = 20000;

        private static Wassup.Data.AttackShape RandomAuthored(System.Random r)
        {
            switch (r.Next(4))
            {
                case 0: return Wassup.Data.AttackShape.Omni;
                case 1: // 부채꼴 30°~180°
                    return new Wassup.Data.AttackShape
                    {
                        kind = Wassup.Data.AttackShapeKind.Circle,
                        angleDeg = 30f + (float)r.NextDouble() * 150f,
                    };
                case 2: // 띠 폭 0.5~2 (반폭 0.25~1)
                    return new Wassup.Data.AttackShape
                    {
                        kind = Wassup.Data.AttackShapeKind.Rect,
                        width = 0.5f + (float)r.NextDouble() * 1.5f,
                    };
                default: // 경계 저작 — 0(미저작)·360·reflex(fail-open)·정확히 180
                    float[] edge = { 0f, 360f, 270f, 180f };
                    return new Wassup.Data.AttackShape
                    {
                        kind = Wassup.Data.AttackShapeKind.Circle,
                        angleDeg = edge[r.Next(edge.Length)],
                    };
            }
        }

        // 새 층의 사상 — `CombatDefinitionBuilder.Bake` → `CombatPhase.Fill` 과 같은 복사.
        private static NewShape ToCore(in OldShape b) => new NewShape
        {
            kind = (byte)CombatDefinitionBuilder.ToCoreShapeKind(b.kind),
            sinHalf = b.sinHalf,
            cosHalf = b.cosHalf,
            halfWidth = b.halfWidth,
        };

        private static float Range(System.Random r, float lo, float hi) => lo + (float)r.NextDouble() * (hi - lo);

        [Test]
        public void InReach_And_InReachShaped_AgreeOnSeededSweep()
        {
            var r = new System.Random(Seed);
            int reachMismatch = 0, shapedMismatch = 0, cellMismatch = 0, inCount = 0, shapedIn = 0;
            string first = null;
            for (int n = 0; n < Cases; n++)
            {
                float tileSize = r.Next(3) == 0 ? 1f : Range(r, 0.5f, 2f);
                var atk = new float3(Range(r, -10f, 10f), Range(r, -1f, 1f), Range(r, -10f, 10f)) * tileSize;
                // 대상은 공격자 주변 ±8 타일 — 경계 근처가 충분히 나오게.
                var tgt = atk + new float3(Range(r, -8f, 8f), Range(r, -1f, 1f), Range(r, -8f, 8f)) * tileSize;
                float range = Range(r, 0.5f, 6f);
                float self = Range(r, 0.25f, 1.5f);
                float body = r.Next(5) == 0 ? 0f : Range(r, 0f, 1f);
                var dir = new float2(Range(r, -5f, 5f), Range(r, -5f, 5f));
                if (r.Next(20) == 0) dir = float2.zero;   // 같은 자리 — 도형 항 통과 분기

                var oldBaked = Wassup.Data.AttackShapeBake.From(RandomAuthored(r), out _);
                var newBaked = ToCore(in oldBaked);

                bool o = OldReach.InReach(atk, tgt, range, tileSize, self, body);
                bool w = NewReach.InReach(atk, tgt, range, tileSize, self, body);
                if (o != w) { reachMismatch++; first ??= $"InReach atk={atk} tgt={tgt} range={range} ts={tileSize} self={self} body={body} old={o} new={w}"; }
                if (o) inCount++;

                bool os = OldReach.InReachShaped(atk, tgt, range, tileSize, self, body, in oldBaked, dir);
                bool ws = NewReach.InReachShaped(atk, tgt, range, tileSize, self, body, in newBaked, dir);
                if (os != ws) { shapedMismatch++; first ??= $"InReachShaped kind={oldBaked.kind} atk={atk} tgt={tgt} range={range} ts={tileSize} self={self} body={body} dir={dir} old={os} new={ws}"; }
                if (os) shapedIn++;

                var ac = new int2(r.Next(-6, 7), r.Next(-6, 7));
                var tc = new int2(r.Next(-6, 7), r.Next(-6, 7));
                if (OldReach.InCellReach(ac, tc, range, self, body) != NewReach.InCellReach(ac, tc, range, self, body))
                { cellMismatch++; first ??= $"InCellReach atk={ac} tgt={tc} range={range} self={self} body={body}"; }
                if (OldReach.InCellRange(ac, tc, (int)range) != NewReach.InCellRange(ac, tc, (int)range))
                { cellMismatch++; first ??= $"InCellRange atk={ac} tgt={tc} range={(int)range}"; }
            }
            TestContext.WriteLine($"cases={Cases} inReach={inCount} inShaped={shapedIn} mismatches reach={reachMismatch} shaped={shapedMismatch} cell={cellMismatch}");
            // 표본이 한쪽으로 쏠리면(전부 안 / 전부 밖) 동등성이 공허하다.
            Assert.That(inCount, Is.InRange(Cases / 10, Cases * 9 / 10), "표본이 경계를 충분히 가로지르지 않는다");
            Assert.That(shapedIn, Is.GreaterThan(0).And.LessThan(inCount + 1));
            Assert.That(reachMismatch + shapedMismatch + cellMismatch, Is.Zero, first);
        }

        [Test]
        public void ShapeBake_MapsFieldForFieldIntoCore()
        {
            var r = new System.Random(Seed + 1);
            for (int n = 0; n < 2000; n++)
            {
                var b = Wassup.Data.AttackShapeBake.From(RandomAuthored(r), out _);
                var c = ToCore(in b);
                Assert.That(c.kind, Is.EqualTo(b.kind), "도형 종류 번호");
                Assert.That(c.IsOmni, Is.EqualTo(b.IsOmni));
                Assert.That(c.sinHalf, Is.EqualTo(b.sinHalf));
                Assert.That(c.cosHalf, Is.EqualTo(b.cosHalf));
                Assert.That(c.halfWidth, Is.EqualTo(b.halfWidth));
            }
        }

        // 가디언 부가 타격(`AggroTargeting.SelectTargets`) — 도형·몸·자석 규칙이 같은 인덱스를 고르나.
        [Test]
        public void GuardianSelectTargets_AgreeOnSeededSweep()
        {
            var r = new System.Random(Seed + 2);
            int mismatch = 0; string first = null;
            for (int n = 0; n < 2000; n++)
            {
                float tileSize = Range(r, 0.5f, 2f);
                var g = new float3(Range(r, -5f, 5f), 0f, Range(r, -5f, 5f)) * tileSize;
                float range = Range(r, 0.5f, 6f), self = Range(r, 0.25f, 1.5f);
                int count = r.Next(0, 12), cap = r.Next(1, 5), held = r.Next(0, cap + 1), want = r.Next(1, 5);
                var oldBaked = Wassup.Data.AttackShapeBake.From(RandomAuthored(r), out _);
                var newBaked = ToCore(in oldBaked);

                var oc = new NativeArray<Wassup.Battle.Combat.AggroCandidate>(count, Allocator.Temp);
                var nc = new Wassup.BattleCore.Combat.AggroCandidate[count];
                for (int i = 0; i < count; i++)
                {
                    var p = g + new float3(Range(r, -7f, 7f), 0f, Range(r, -7f, 7f)) * tileSize;
                    float body = Range(r, 0f, 1f); bool ag = r.Next(2) == 0;
                    oc[i] = new Wassup.Battle.Combat.AggroCandidate { pos = p, bodyRadius = body, aggroed = ag };
                    nc[i] = new Wassup.BattleCore.Combat.AggroCandidate { Pos = p, BodyRadius = body, Aggroed = ag };
                }
                var oOut = new NativeArray<int>(want, Allocator.Temp);
                var nOut = new int[want];
                int os = OldAggro.SelectTargets(g, range, tileSize, self, held, cap, in oldBaked, oc, oOut);
                int ns = NewAggro.SelectTargets(g, range, tileSize, self, held, cap, in newBaked, nc, count, nOut, want);
                bool same = os == ns;
                for (int k = 0; same && k < os; k++) same = oOut[k] == nOut[k];
                if (!same) { mismatch++; first ??= $"case {n}: old={os} new={ns} kind={oldBaked.kind}"; }
                oc.Dispose(); oOut.Dispose();
            }
            Assert.That(mismatch, Is.Zero, first);
        }
    }
}
