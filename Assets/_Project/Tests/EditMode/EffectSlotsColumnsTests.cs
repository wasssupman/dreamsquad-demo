using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Wassup.BattleCore.Trigger;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // skill-data-table unit 8 — **종류별 사용 칸 표**(`EffectSlots.UsedColumns` — 정본 `tables.md` §3 · §9). unit 9 export 가 「그 종류가 쓰는
    // 칸만」 쓰는 데 쓴다. 표가 종류를 빠뜨리거나(append 한 종류) 칸을 빠뜨리면(늘린 칸) 시트가 조용히 값을 잃는다 — 여기서 닫는다.
    public class EffectSlotsColumnsTests
    {
        private static readonly EffectKind[] NotInTable =
            { EffectKind.None, EffectKind.AreaBarrage, EffectKind.SelfWarmupBuff, EffectKind.SplitOnDeath };

        [Test]
        public void 모든_효과_종류가_사용_칸_표에_있다_표_밖은_넷뿐이다()
        {
            foreach (EffectKind k in Enum.GetValues(typeof(EffectKind)))
            {
                bool inTable = EffectSlots.UsedColumns(k, out var cols);
                bool expected = Array.IndexOf(NotInTable, k) < 0;
                Assert.AreEqual(expected, inTable, k + " — 표 안/밖");
                Assert.AreEqual(inTable, EffectSlots.Of(k, out _, out _, out _), k + " — 겸직 칸 표(`Of`)와 표 안/밖이 같다");
                if (!inTable) Assert.AreEqual(EffectColumns.None, cols, k.ToString());
            }
        }

        [Test]
        public void 겸직_칸_표가_읽는_칸은_사용_칸이다()
        {
            // 굽기는 `Of` 로 겸직 칸을 채운다 — 거기서 읽는 뜻 칸이 사용 칸 표에 없으면 export 가 그 값을 빼 버린다.
            foreach (EffectKind k in Enum.GetValues(typeof(EffectKind)))
            {
                if (!EffectSlots.Of(k, out var m, out var t, out var d)) continue;
                EffectSlots.UsedColumns(k, out var cols);
                foreach (var s in new[] { m, t, d })
                {
                    if (s == EffectSlot.None) continue;
                    var col = (EffectColumns)Enum.Parse(typeof(EffectColumns), s.ToString());
                    Assert.IsTrue((cols & col) != 0, $"{k}: 겸직 칸 {s} 가 사용 칸에 없다");
                }
            }
        }

        [Test]
        public void 비율_칸은_비율을_받는_종류만_쓴다()
        {
            const EffectColumns ratio = EffectColumns.MagnitudeMode | EffectColumns.BasisStat | EffectColumns.Ratio;
            foreach (EffectKind k in Enum.GetValues(typeof(EffectKind)))
            {
                if (!EffectSlots.UsedColumns(k, out var cols)) continue;
                Assert.AreEqual(EffectMagnitude.AcceptsRatio(k) ? ratio : EffectColumns.None, cols & ratio, k.ToString());
            }
        }

        [Test]
        public void 효과_값의_칸마다_열_하나가_이름으로_짝이다()
        {
            // 열 이름 = `EffectValues` 칸 이름의 첫 글자 대문자(참조 셋 `…Id` 는 효과 에셋 칸 — 값 struct 밖). 칸을 늘리고 열을 잊으면 빨갛다.
            var columns = new HashSet<string>(Enum.GetNames(typeof(EffectColumns)));
            columns.Remove(nameof(EffectColumns.None));
            foreach (var f in typeof(EffectValues).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.Name == nameof(EffectValues.kind)) continue;   // 모든 줄의 칸
                string col = char.ToUpperInvariant(f.Name[0]) + f.Name.Substring(1);
                Assert.IsTrue(columns.Remove(col), $"EffectValues.{f.Name} 에 짝 열 {col} 이 없다");
            }
            CollectionAssert.AreEquivalent(new[] { "ProjectileId", "PatternId", "HazardId" }, columns, "값 struct 밖의 열은 참조 셋뿐이다");
        }

        [Test]
        public void 상시_효과_4종의_칸은_unit_8_표와_같다()
        {
            Assert.IsTrue(EffectSlots.UsedColumns(EffectKind.FactionStatBuff, out var buff));
            Assert.AreEqual(EffectColumns.BuffStat | EffectColumns.Percent | EffectColumns.AllyFilter, buff);
            Assert.IsTrue(EffectSlots.UsedColumns(EffectKind.ProjectileBounce, out var bounce));
            Assert.AreEqual(EffectColumns.Count | EffectColumns.RangeTiles | EffectColumns.Mul, bounce);
            Assert.IsTrue(EffectSlots.UsedColumns(EffectKind.FrontmostTarget, out var front));
            Assert.AreEqual(EffectColumns.Mul, front);
            Assert.IsTrue(EffectSlots.UsedColumns(EffectKind.DamageVsSleeping, out var sleep));
            Assert.AreEqual(EffectColumns.Mul, sleep);
            // 배치 오라의 수혜 대상도 효과 칸이다(계약 12).
            Assert.IsTrue(EffectSlots.UsedColumns(EffectKind.PlacementAura, out var aura));
            Assert.IsTrue((aura & EffectColumns.AllyFilter) != 0);
        }
    }
}
