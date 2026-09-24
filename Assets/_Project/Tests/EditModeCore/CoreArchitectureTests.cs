using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 4 — **구조를 코드로 지킨다.**
    //
    // 아래 셋은 리뷰어가 매번 눈으로 세던 것이다. 눈으로 세는 계약은 바쁜 날 깨지고,
    // 깨진 것을 아무도 모른다 — 그래서 자로 만든다.
    [TestFixture]
    public class CoreArchitectureTests
    {
        private static string CoreDir
            => Path.Combine(CoreGoldenStore.RepoRoot, "Assets/_Project/Scripts/BattleCore");

        private static string[] Sources(string subDir)
            => Directory.GetFiles(Path.Combine(CoreDir, subDir), "*.cs", SearchOption.AllDirectories);

        // 주석·문자열을 뺀 **코드만** 본다. 주석에 적힌 이름 때문에 빨개지면 다음 사람이
        // 이 테스트를 지운다.
        private static string CodeOnly(string path)
        {
            string s = File.ReadAllText(path);
            s = Regex.Replace(s, @"/\*.*?\*/", "", RegexOptions.Singleline);
            s = Regex.Replace(s, @"//[^\n]*", "");
            s = Regex.Replace(s, "\"(\\\\.|[^\"\\\\])*\"", "\"\"");
            return s;
        }

        [Test]
        public void 저장소_루트를_찾는다()
            => Assert.IsNotNull(CoreGoldenStore.RepoRoot,
                "못 찾으면 아래 테스트들은 «통과하지만 아무것도 증언하지 않는다»");

        [Test]
        public void EndMatch_호출처는_정확히_넷이다()
        {
            var hits = new List<string>();
            foreach (var path in Directory.GetFiles(CoreDir, "*.cs", SearchOption.AllDirectories))
            {
                string code = CodeOnly(path);
                // 선언(`void EndMatch(`)은 호출이 아니다. **바로 앞** 토큰만 본다 —
                // 넓게 보면 식 본문 메서드(`public void Complete() => _clock.EndMatch(…)`)가
                // 선언으로 오인돼 호출 하나가 조용히 안 세어진다(실제로 그랬다).
                foreach (Match m in Regex.Matches(code, @"(?<!void\s)\bEndMatch\s*\("))
                    hits.Add(Path.GetFileName(path));
            }

            // 통로는 3(`complete` · `submitted` · `stress_full`)인데 호출처가 4인 것은
            // **만료와 목표 달성이 `complete` 를 나눠 갖기** 때문이다.
            Assert.AreEqual(4, hits.Count,
                "호출처: MatchClock(만료) · CommandPhase(제출) · HeartMeter(붕괴) · MatchGoalContext(목표). "
                + "늘어나면 「패배」가 조용히 되살아난다. 실제: " + string.Join(", ", hits));

            CollectionAssert.AreEquivalent(
                new[] { "MatchClock.cs", "CommandPhase.cs", "HeartMeter.cs", "IMatchGoal.cs" },
                hits);
        }

        [Test]
        public void 담당자는_모드를_모른다()
        {
            foreach (var path in Sources("Owners"))
            {
                string name = Path.GetFileName(path);
                string code = CodeOnly(path);

                // `MatchClock` 은 **시계 정책의 소유자**라 `ModeDef` 를 받는다(문서화된 예외).
                // 그것 말고 어떤 담당자도 모드를 알면 안 된다 — 아는 순간 `if (mode == …)` 가
                // 생기고, 그것이 계약 12 가 담당자 안으로 새는 첫 장면이다.
                if (name != "MatchClock.cs")
                    StringAssert.DoesNotContain("ModeDef", code, $"{name} 이 모드를 안다");

                StringAssert.DoesNotContain("GoalKind", code, $"{name} 이 목표 종류를 안다");
                StringAssert.DoesNotContain("ModeId", code, $"{name} 이 «어느 모드인가» 를 안다");
            }
        }

        [Test]
        public void 코어에는_엔진_참조가_없다()
        {
            foreach (var path in Directory.GetFiles(CoreDir, "*.cs", SearchOption.AllDirectories))
            {
                string code = CodeOnly(path);
                StringAssert.DoesNotContain("UnityEngine", code, Path.GetFileName(path));
                StringAssert.DoesNotContain("Unity.Entities", code, Path.GetFileName(path));
                StringAssert.DoesNotContain("Unity.Collections", code, Path.GetFileName(path));
            }
        }

        [Test]
        public void 매니저_브리지_컨트롤러라는_이름이_없다()
        {
            foreach (var path in Directory.GetFiles(CoreDir, "*.cs", SearchOption.AllDirectories))
            {
                string code = CodeOnly(path);
                foreach (Match m in Regex.Matches(code, @"\b(class|struct|interface)\s+(\w+)"))
                {
                    string type = m.Groups[2].Value;
                    Assert.IsFalse(type.EndsWith("Manager") || type.EndsWith("Bridge")
                                   || type.EndsWith("Controller"),
                        $"{Path.GetFileName(path)} 의 `{type}` — 절대 제약 1");
                }
            }
        }

        [Test]
        public void 개체를_목록에서_빼는_길은_하나다()
        {
            // `BattleWorld` 밖에서 `_units.Remove*` 같은 것이 생기면 계약 7
            // (「모든 소멸은 소멸 이벤트를 낸다」)이 종류마다 따로 지켜져야 한다.
            foreach (var path in Directory.GetFiles(CoreDir, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(path) == "BattleWorld.cs") continue;
                string code = CodeOnly(path);
                StringAssert.DoesNotContain("_units.Remove", code, Path.GetFileName(path));
                StringAssert.DoesNotContain("_projectiles.Remove", code, Path.GetFileName(path));
            }
        }
    
        [Test]
        public void 스킬_경로의_쓰기는_IntentApplier_한_표면을_지난다()
        {
            // unit 7a · S20 — 옛 전투는 asmdef 가 「쓰기는 발행으로만」을 컴파일러로 강제했다. 새 코어 안에서는
            // 아무것도 막지 않으므로 규율을 **표면 하나**로 옮기고 그 형태를 여기서 못박는다.
            // 트리거 폴더에서 세계를 바꾸는 호출(관문·요청 줄·인박스·장·장판)은 `IntentApplier` 에만 있어야 한다.
            // 예외 하나: 등록부의 소급 회수(`RevokeTag`) — 수명의 일이지 스킬의 쓰기가 아니다.
            var writes = new Regex(@"EffectApply\.|RequestCc\(|ProjectileRequests\.Add|GrantShield\(|SpawnField\(|HazardSpawn\.|AggroRequests\.Add|Inbox\.(Damage|Heal|Shield)|\.Cc\.(Apply|Clear)\(|\.Health\s*=[^=]|\.Position\s*=[^=]");
            var offenders = new List<string>();
            foreach (var path in Sources("Trigger"))
            {
                string name = Path.GetFileName(path);
                if (name == "IntentApplier.cs") continue;
                if (writes.IsMatch(CodeOnly(path))) offenders.Add(name);
            }
            CollectionAssert.IsEmpty(offenders, "스킬 경로의 상태 변경은 `IntentApplier.Apply` 뿐이다");

            // 문맥(질의)은 쓰기를 **위임만** 한다 — `Emit` 두 줄이 applier 로 간다.
            string ctx = CodeOnly(Path.Combine(CoreDir, "Trigger/CoreSkillContext.cs"));
            StringAssert.Contains("_applier.Apply(in intent)", ctx);
        }

        [Test]
        public void 트리거_레이어에_매니저_이름이_없다()
        {
            foreach (var path in Sources("Trigger"))
                Assert.IsFalse(Regex.IsMatch(CodeOnly(path), @"class\s+\w*(Manager|Bridge|Controller)\b"),
                    Path.GetFileName(path) + " — 새 코어 절대 제약 1");
        }
}
}
