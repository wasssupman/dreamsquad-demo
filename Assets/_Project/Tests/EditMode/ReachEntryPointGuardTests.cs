using System.IO;
using System.Linq;
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

    // distance-based-range unit 23b — **「0 이 «자리형» 인지 «안 실었다» 인지」를 고정한다.**
    //
    // ★ `EventBodyRadius`/`originBodyRadius` 의 0 은 **두 뜻을 겸직**한다: 「이 자리는 칸이다」와
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
                new[] { UnityEngine.Application.dataPath, "_Project", "Scripts" }
                    .Concat(parts).ToArray()));

        [Test]
        public void DamageSeams_CarryTheOwnersBody_AndKillUsesTheVictimNotTheKiller()
        {
            var src = Read("Battle", "Units", "DamageApplicationSystem.cs");

            // 피격·실드파열 — 자리도 몸도 host 자신.
            Assert.GreaterOrEqual(Regex.Matches(src, @"EventBodyRadius\s*=\s*SelfBodyRadius\(entity\)").Count, 3,
                "피격·실드파열·처치 세 seam 이 모두 자리의 주인의 몸을 실어야 한다");

            // ★ 처치(시체폭발) — 시전자는 킬러지만 **폭심은 죽은 적**이다.
            Assert.IsTrue(src.Contains("CasterBodyRadius = SelfBodyRadius(killerSource)"),
                "처치 seam 의 시전자 몸은 킬러 것이다");
            Assert.IsFalse(Regex.IsMatch(src, @"EventBodyRadius\s*=\s*SelfBodyRadius\(killerSource\)"),
                "시체폭발의 폭심에 «킬러» 의 몸이 붙었다 — 폭심은 죽은 적이고, 킬러 몸을 쓰면 "
                + "방어유닛(1.0)의 몸으로 적 시체 위 폭발 반경을 정하게 된다");
        }

        [Test]
        public void DeathSeam_SnapshotsTheBody_BecauseTheEntityIsGoneAtDrain()
        {
            var src = Read("Battle", "Units", "UnitLifecycleSystem.cs");
            Assert.IsTrue(src.Contains("EventBodyRadius = bodyRadius"),
                "자기 죽음 seam 이 몸을 값으로 안 싣는다 — 드레인 시점엔 파괴돼 못 읽고, "
                + "안 실으면 0 으로 새어 사망 폭발이 «조용히» 좁아진다");
            Assert.IsTrue(src.Contains("CasterBodyRadius = bodyRadius"));
        }

        // ★ 퇴근 운석은 **의도적으로 0** 이다 — 「자리에 떨어지는 것」(사용자 결정 2026-09-06).
        // 이 단언이 없으면 다음 사람이 「배선 누락이네」 하고 채워 넣고, 그러면 배스티온이
        // 퇴근할 때만 운석이 1칸 넓어진다.
        [Test]
        public void RetireMeteor_IsDeliveryForm_AndSaysSoExplicitly()
        {
            var src = Read("Bridge", "BattleBridge.cs");
            Assert.IsTrue(src.Contains("«의도적으로» 안 싣는다 = 0 = 자리형"),
                "퇴근 운석의 자리형 의도가 코드에 안 적혀 있다 — 0 이 누락으로 오해된다");
        }

        [Test]
        public void SelfSiteBlasts_CarryTheirOwnerBody_ThroughTheIntentBoundary()
        {
            Assert.IsTrue(Read("Skills", "Concrete", "SelfAreaBlastSkill.cs")
                    .Contains("OriginBodyRadius = caster.BodyRadius"),
                "자폭이 시전자 몸을 intent 경계 너머로 안 보낸다");
            Assert.IsTrue(Read("Skills", "Concrete", "DeathSiteBlastSkill.cs")
                    .Contains("OriginBodyRadius = p.EventBodyRadius"),
                "사망/시체 폭발이 «자리의 주인» 의 몸을 안 보낸다 — caster 것을 쓰면 시체폭발이 틀린다");
        }

        [Test]
        public void ImpactJudgment_ReadsTheCarriedOrigin_NotACellConstant()
        {
            var src = Read("Battle", "Combat", "Projectile", "ProjectileHitSystem.cs");
            Assert.IsTrue(src.Contains("SkillMath.ReachFromImpact("),
                "착탄 광역이 실려 온 원점을 안 읽는다 — 자기 자리 폭발이 칸 반폭으로 잘린다");
            Assert.IsTrue(src.Contains("originBodyRadius"),
                "요청에 실린 원점 반경이 판정까지 도달하지 않는다");
        }
    }
}
