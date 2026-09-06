using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Wassup.Tests.EditMode
{
    // distance-based-range unit 23a — **「원점 항을 손으로 넘길 수 없다」를 그물로 고정한다.**
    //
    // ★ 이 파일이 지키는 것은 값이 아니라 **형태**다. unit 22 와 unit 23 초판이 같은 결함을
    // 두 번 놓쳤고, 두 번 다 원인이 같았다 — **호출부가 「내 몸」 자리에 칸 상수를 손으로
    // 넘길 수 있었다.** 값을 고치는 것으로는 재발을 못 막는다(고쳐도 다음 호출부가 또 넘긴다).
    //
    // 1차 방어는 **컴파일러**다: `SkillMath.CellHalfWidthTiles` 가 `private` 이라 sim 이 그 값을
    // 아예 못 본다. 진입점도 `ReachFromUnit`(몸을 요구) / `ReachFromCell`(안 받음) 둘뿐이다.
    // 이 그물은 **2차 방어** — 표기 전용 접근자(`CellShapePaddingTiles`)가 sim 으로 새는 것을 막는다.
    // 그게 새면 「도형 보정항」이 다시 「내 몸」 행세를 하게 되고, 정확히 그 혼동이 이 결함이었다.
    //
    // `SkillAdapterDirectWriteTests` 와 같은 관용구(소스 정규식 스캔)이고, 그 파일이 적어 둔
    // 한계도 같이 진다 — **개수만 세면 「하나 빼고 하나 더하면」 통과**하므로 위치를 같이 본다.
    public class ReachEntryPointGuardTests
    {
        private static string SimRoot =>
            Path.Combine(UnityEngine.Application.dataPath, "_Project", "Scripts", "Battle");

        private static string SkillsRoot =>
            Path.Combine(UnityEngine.Application.dataPath, "_Project", "Scripts", "Skills");

        // 표기 전용 접근자. sim 은 이 값을 **판정에 쓰면 안 된다** — 형은 `RangeMetric` 이 정한다.
        private const string DisplayOnlyAccessor = "CellShapePaddingTiles";

        [Test]
        public void SimLayer_NeverReadsTheDisplayOnlyShapePadding()
        {
            var hits = new System.Collections.Generic.List<string>();
            foreach (var f in Directory.GetFiles(SimRoot, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    var t = lines[i].TrimStart();
                    if (t.StartsWith("//")) continue;                  // 주석의 언급은 허용(이력 서술)
                    if (!lines[i].Contains(DisplayOnlyAccessor)) continue;
                    hits.Add($"{Path.GetFileName(f)}:{i + 1}");
                }
            }

            Assert.IsEmpty(hits,
                "sim 경로가 표기 전용 도형 보정항을 읽고 있다 — 판정의 원점 항은 `RangeMetric` 이 정하고 "
                + "`ReachFromUnit`/`ReachFromCell` 두 진입점만 통한다. "
                + "이 상수를 「내 몸」 자리에 넘길 수 있게 되는 순간 unit 22·23 의 결함이 재발한다: "
                + string.Join(", ", hits));
        }

        // 도메인(순수 스킬 레이어)도 같다 — concrete 가 자를 직접 만들면 어댑터와 갈린다.
        [Test]
        public void SkillDomain_NeverReadsTheDisplayOnlyShapePadding()
        {
            var hits = new System.Collections.Generic.List<string>();
            foreach (var f in Directory.GetFiles(SkillsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(f) == "SkillMath.cs") continue;    // 정의 자리
                var lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    var t = lines[i].TrimStart();
                    if (t.StartsWith("//")) continue;
                    if (!lines[i].Contains(DisplayOnlyAccessor)) continue;
                    hits.Add($"{Path.GetFileName(f)}:{i + 1}");
                }
            }
            Assert.IsEmpty(hits, "스킬 도메인이 표기 상수를 읽는다: " + string.Join(", ", hits));
        }

        // 진입점이 둘로 유지되는가 — 본문이 다시 공개되면 「손으로 넘기기」가 되살아난다.
        [Test]
        public void ThePredicateBody_StaysPrivate_SoOnlyTheTwoEntryPointsExist()
        {
            var src = File.ReadAllText(Path.Combine(SkillsRoot, "SkillMath.cs"));

            Assert.IsTrue(Regex.IsMatch(src, @"private\s+static\s+bool\s+Reach\("),
                "판정 본체가 private 이 아니다 — 공개되면 호출부가 원점 항을 손으로 넘길 수 있다");
            Assert.IsTrue(Regex.IsMatch(src, @"private\s+const\s+float\s+CellHalfWidthTiles"),
                "칸 반폭 상수가 private 이 아니다 — 이 값이 보이는 순간 「내 몸」 자리에 들어간다");
            Assert.IsTrue(src.Contains("public static bool ReachFromUnit("), "몸형 진입점이 없다");
            Assert.IsTrue(src.Contains("public static bool ReachFromCell("), "자리형 진입점이 없다");

            // 옛 이름이 되살아나면(복붙 복원) 그물이 통째로 무의미해진다.
            Assert.IsFalse(Regex.IsMatch(src, @"public\s+static\s+bool\s+InBodyReach\("),
                "옛 5-인자 공개 술어가 되살아났다 — 원점 항을 손으로 넘길 수 있다");
        }
    }
}
