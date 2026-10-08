using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Somnia.Battle.Tests.EditMode
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
    // ⚠ battle-core-rebuild unit 9 — 옛 ECS sim(`Scripts/Battle/`·`Bridge/`)의 소스를 읽던 그물 10개는 대상과 함께 은퇴했다.
    //   남은 것은 `Somnia.Battle.Skills` 쪽 형태 그물이다. 전투 코어의 원점 항은 행동 테스트(`Tests/EditModeCore/`)가 증언한다.
    public class ReachEntryPointGuardTests
    {

        private static string SkillsRoot =>
            Path.Combine(UnityEngine.Application.dataPath, "_Project", "Runtime", "Battle", "Scripts", "Skills");


        // 표기 전용 접근자. sim 은 이 값을 **판정에 쓰면 안 된다** — 형은 `RangeMetric` 이 정한다.
        private const string DisplayOnlyAccessor = "CellShapePaddingTiles";

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

            // ★ **금지 스캔에는 «존재 단언»이 짝으로 필요하다**(리뷰 T-1/M-2).
            // 없으면 리네임 한 번에 아래 스캔 셋이 전부 hit 0 으로 **vacuous 통과**하고,
            // 「위반 0」과 「검사 대상 없음」이 구분되지 않는다 — 그물이 조용히 사라진다.
            Assert.IsTrue(src.Contains("public static float " + DisplayOnlyAccessor),
                $"표기 전용 접근자 `{DisplayOnlyAccessor}` 가 없다 — 이름이 바뀌었다면 "
                + "아래 금지 스캔들이 전부 vacuous 통과 중이다. 상수와 스캔을 같이 갱신하라.");

            // 옛 이름이 되살아나면(복붙 복원) 그물이 통째로 무의미해진다.
            Assert.IsFalse(Regex.IsMatch(src, @"public\s+static\s+bool\s+InBodyReach\("),
                "옛 5-인자 공개 술어가 되살아났다 — 원점 항을 손으로 넘길 수 있다");
        }
    }

    // distance-based-range unit 23b — **「0 이 «자리형» 인지 «안 실었다» 인지」를 고정한다.**
    //
    // ★ `EffectBody`(옛 `EventBodyRadius` — unified-effect-layer unit 2 에서 `SkillOrigin` 으로)/`originBodyRadius` 의 0 은 **두 뜻을 겸직**한다: 「이 자리는 칸이다」와
    // 「생산자가 안 실었다」. 겸직 자체는 종전 동작과 같아 안전하지만, **배선 누락이 의도된
    // 자리형으로 위장돼 조용히 산다** — 그게 이 spec 이 반복해 당한 fail-open 모양이라
    // (unit 22 · unit 23 초판), 생산자를 **이름으로** 고정한다.
    //
    // ⚠ 개수만 세면 「하나 빼고 하나 더하면」 통과한다(`SkillAdapterDirectWriteTests` 의 한계).
    // 그래서 **어느 파일이 무엇을 싣는가**를 같이 단언한다.
    public class OriginBodyRadiusWiringTests
    {
        private static string Read(params string[] parts)
            => File.ReadAllText(Path.Combine(
                new[] { UnityEngine.Application.dataPath, "_Project", "Runtime", "Battle", "Scripts" }
                    .Concat(parts).ToArray()));

        // 선언 위치부터 **중괄호가 닫히는 데까지**. 소스 그물의 윈도를 글자 수나 「다음 선언」으로
        // 자르면 이웃 메서드와 그 **선행 주석**을 삼켜 오탐·미탐이 난다(리뷰 L-2·L-4 가 둘 다 겪었다).
        // ⚠ 문자열 리터럴 안의 중괄호는 안 센다 — 이 그물이 보는 메서드들엔 없고, 생기면
        //    윈도가 **길어져** 단언이 빡세지는 쪽(fail-closed)이라 조용히 통과하지 않는다.
        private static string MethodBody(string src, int declIndex)
        {
            int open = src.IndexOf('{', declIndex);
            Assert.Greater(open, declIndex, "메서드 본문의 여는 중괄호를 못 찾았다");
            int depth = 0;
            for (int k = open; k < src.Length; k++)
            {
                if (src[k] == '{') depth++;
                else if (src[k] == '}' && --depth == 0)
                    return src.Substring(declIndex, k + 1 - declIndex);
            }
            Assert.Fail("메서드 본문의 닫는 중괄호를 못 찾았다");
            return string.Empty;
        }

        // 줄 주석(`//`)을 제거한 «코드만» 남긴다. 금지어 스캔은 반드시 이걸 지나야 한다 —
        // 헤더가 그 금지어를 **이력으로 언급**하는 것이 이 레포의 관용구라, 원문을 그대로 스캔하면
        // 그물이 자기 주석에 걸려 오탐이 나고, 그걸 피하려고 예외를 얹으면 **vacuous** 가 된다.
        // ⚠ 블록 주석(`/* */`)·문자열 리터럴은 안 다룬다 — 이 그물이 보는 파일들엔 없다.
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            foreach (var line in src.Split('\n'))
            {
                int at = line.IndexOf("//", System.StringComparison.Ordinal);
                sb.Append(at >= 0 ? line.Substring(0, at) : line).Append('\n');
            }
            return sb.ToString();
        }

        private static int CountOf(string haystack, string needle)
        {
            int n = 0, at = 0;
            while ((at = haystack.IndexOf(needle, at, System.StringComparison.Ordinal)) >= 0)
            { n++; at += needle.Length; }
            return n;
        }

        [Test]
        public void SelfSiteBlasts_CarryTheirOwnerBody_ThroughTheIntentBoundary()
        {
            Assert.IsTrue(Read("Skills", "Concrete", "SelfAreaBlastSkill.cs")
                    .Contains("OriginBodyRadius = target.Origin.LaunchBody"),
                "자폭이 시전자 몸을 intent 경계 너머로 안 보낸다");
            Assert.IsTrue(Read("Skills", "Concrete", "DeathSiteBlastSkill.cs")
                    .Contains("OriginBodyRadius = target.Origin.EffectBody"),
                "사망/시체 폭발이 «자리의 주인» 의 몸을 안 보낸다 — caster 것을 쓰면 시체폭발이 틀린다");
        }

        // 「원점 항을 «상수로» 손넘길 수 없다」가 진짜 계약이다. 진입점이 늘어나는 것 자체는
        // 막지 않되(형이 늘면 정당하게 는다), **모르는 사이에 느는 것**은 막는다.
        [Test]
        public void PublicReachEntryPoints_AreExactlyTheKnownSet()
        {
            var src = Read("Skills", "SkillMath.cs");
            var found = Regex.Matches(src, @"public\s+static\s+bool\s+(Reach\w*)\s*\(")
                             .Select(m => m.Groups[1].Value).OrderBy(x => x).ToArray();
            // ⚠ **넷이다.** 2026-09-07 에 `ReachFromUnitToCell`(「몸이 칸을 친다」)을 5번째로
            // 넣었다가 같은 날 철거했다 — 착지 슬램이 **자리형**으로 확정되면서 소비처가 0 이 됐다.
            // 소비처 0 인 공개 진입점은 제약 8 이 금지하는 「나중을 위한 층」이다.
            var expected = new[] { "ReachFromCell", "ReachFromImpact", "ReachFromUnit",
                                   "ReachWithOrigin" };
            CollectionAssert.AreEqual(expected, found,
                "공개 도달 진입점 목록이 바뀌었다. 늘릴 때는 «원점 항이 데이터에서 온다» 를 지키는지 "
                + "확인하고 이 목록과 아키텍처 불변식 7 을 같이 갱신하라: " + string.Join(", ", found));
        }

    }
}
