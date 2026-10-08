using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Somnia.Battle.BattleCore;

namespace Somnia.Battle.Tests.EditMode.Core
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

        // unity-6-6-upgrade — 6.6 부터 `Unity.Mathematics` 가 엔진 모듈이라 세 어셈블리의
        // `noEngineReferences` 를 껐다. 컴파일러가 더는 경계를 막지 않으므로 **이 스캔이 경계다**
        // (헤드리스 lane 의 `BattleCore.csproj` 가 MathematicsModule 만 참조하는 것과 함께).
        private static readonly string[] EngineFreeDirs =
        {
            "Assets/_Project/Scripts/BattleCore",
            "Assets/_Project/Scripts/Skills",
            "Assets/_Project/Scripts/UnitAi",
        };

        [Test]
        public void 코어에는_엔진_참조가_없다()
        {
            foreach (var dir in EngineFreeDirs)
            foreach (var path in Directory.GetFiles(Path.Combine(CoreGoldenStore.RepoRoot, dir), "*.cs", SearchOption.AllDirectories))
            {
                string code = CodeOnly(path);
                string where = Path.GetFileName(dir) + "/" + Path.GetFileName(path);
                StringAssert.DoesNotContain("UnityEngine", code, where);
                StringAssert.DoesNotContain("UnityEditor", code, where);
                StringAssert.DoesNotContain("Unity.Entities", code, where);
                StringAssert.DoesNotContain("Unity.Collections", code, where);
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
        public void 손패_담당자에는_효과가_한_줄도_없다()
        {
            // unit 7b — 「효과는 `BindingRegistry` 호출로만 나간다」. 손패가 의도·관문·스탯·군중 제어를 직접 만지면
            // 그것이 카드 효과를 아는 첫 줄이고, 다음 사람이 그 옆에 다음 카드의 효과를 적는다.
            string code = CodeOnly(Path.Combine(CoreDir, "Owners/HandDeck.cs"));
            var effects = new Regex(@"SimIntent|IntentApplier|EffectApply\.|RequestCc\(|\.Modifiers\.|\.Cc\.|ProjectileRequests|Inbox\.|\.Mods\.|ISkill\b|\.Execute\(");
            var hit = effects.Match(code);
            Assert.IsFalse(hit.Success, "HandDeck 에 효과 코드: " + hit.Value);
            StringAssert.Contains("_registry.AttachCard(", code, "부착은 등록부로 나간다");
            StringAssert.Contains("_registry.DetachCard(", code, "회수도 등록부로 나간다");
        }

        [Test]
        public void 탄_부여_생산자는_디버그_커맨드뿐이다_생산자를_열면_상한_줄을_같이_저작한다()
        {
            // unit 7b 완료 기준(6a2 리뷰 F1) — 상한 줄이 없는 키의 부여는 관문이 거절한다(빌더는 어떤 키가 쓰일지 모른다).
            // 7b 는 부여 생산자를 **열지 않았다**: 카드의 착탄 효과(비수의 출혈·서리 화살의 기절)는 공격 seam 에서 대상에
            // 직접 걸고(옛 RESOLVE 시점), 카드 탄은 관문이 시전자 저작 출력을 접는다(6a2 결정 ①). 이 그물은 **다음 생산자**를
            // 잡는다 — 여기가 빨개지면 그 키의 `ImbueCapConfig` 줄을 같은 커밋에서 저작하고 목록에 더할 것.
            var grants = new List<string>();
            foreach (var path in Directory.GetFiles(CoreDir, "*.cs", SearchOption.AllDirectories))
                if (Regex.IsMatch(CodeOnly(path), @"ImbueGate\.Grant\(")) grants.Add(Path.GetFileName(path));
            grants.Remove("ProjectileImbueSet.cs");   // 관문 자신
            CollectionAssert.AreEquivalent(new[] { "CommandPhase.cs" }, grants);
        }

        [Test]
        public void 트리거_레이어에_매니저_이름이_없다()
        {
            foreach (var path in Sources("Trigger"))
                Assert.IsFalse(Regex.IsMatch(CodeOnly(path), @"class\s+\w*(Manager|Bridge|Controller)\b"),
                    Path.GetFileName(path) + " — 새 코어 절대 제약 1");
        }

        // unit 9 감사 B — 옛 `ReachEntryPointGuardTests::SimLayer_NeverReadsTheDisplayOnlyShapePadding` 의
        // 코어판. 그 그물이 옛 sim 소스와 함께 은퇴(e548eda90)한 사이 포탈 입구 반경이 표기 전용 접근자로
        // 판정 자를 만들고 있었다(`IntentApplier.SpawnField`). 판정의 원점 항은 `SkillMath` 진입점의
        // 성질이다(제약 13) — 표기 접근자가 코어로 새면 「도형 보정항」이 다시 「내 몸」 행세를 한다.
        private const string DisplayOnlyShapePadding = "CellShapePaddingTiles";

        [Test]
        public void 전투_코어는_표기_전용_도형_보정항을_읽지_않는다()
        {
            // 존재 단언 짝 — 이름이 바뀌면 아래 스캔이 vacuous 통과한다.
            string skillMath = Path.Combine(CoreGoldenStore.RepoRoot, "Assets/_Project/Scripts/Skills/SkillMath.cs");
            StringAssert.Contains("public static float " + DisplayOnlyShapePadding, File.ReadAllText(skillMath),
                "표기 전용 접근자 이름이 바뀌었다 — 이 스캔을 같이 갱신하라");

            var hits = new List<string>();
            foreach (var path in Directory.GetFiles(CoreDir, "*.cs", SearchOption.AllDirectories))
                if (CodeOnly(path).Contains(DisplayOnlyShapePadding))
                    hits.Add(Path.GetFileName(path));
            Assert.IsEmpty(hits, "전투 코어가 표기 전용 도형 보정항을 읽는다 — 판정은 `SkillMath.ReachFrom*` "
                                 + "진입점만 통한다(제약 13): " + string.Join(", ", hits));
        }

        // unified-effect-layer unit 2 — **concrete 는 원점을 `target.Origin` 한 곳에서만 읽는다**(H1 · README 계약 1).
        // 옛 세 갈래(`ctx.Position(caster.Unit)` · `target.CellA` · `p.EventPosition`/`EventBodyRadius`)가 되살아나면
        // 같은 효과가 출처마다 다른 자리를 원점으로 삼는다 — 산출은 드레인 한 곳인데 읽기가 다시 흩어진다.
        // ⚠ 후보 거리(`ctx.Position(candidate)`)는 원점 읽기가 아니다 — 시전자 자리만 막는다.
        private static readonly string[] OldOriginReads =
        {
            @"\bctx\s*\.\s*Position\s*\(\s*caster\s*\.\s*Unit\s*\)",
            @"\.\s*CellA\b",
            @"\bEventPosition\b",
            @"\bEventBodyRadius\b",
        };

        [Test]
        public void concrete_는_원점을_Origin_에서만_읽는다()
        {
            string concreteDir = Path.Combine(CoreGoldenStore.RepoRoot, "Assets/_Project/Scripts/Skills/Concrete");
            var files = Directory.GetFiles(concreteDir, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files, "concrete 폴더를 못 찾았다 — 아래 스캔이 vacuous 통과한다");

            var hits = new List<string>();
            int originReaders = 0;
            foreach (var path in files)
            {
                string code = CodeOnly(path);
                if (code.Contains("target.Origin.")) originReaders++;
                foreach (var pattern in OldOriginReads)
                    if (Regex.IsMatch(code, pattern)) hits.Add(Path.GetFileName(path) + " ← " + pattern);
            }
            // 존재 단언 짝 — 이름이 바뀌면 금지 스캔만 초록으로 남는다.
            Assert.Greater(originReaders, 0, "`target.Origin.` 을 읽는 concrete 가 없다 — 이름이 바뀌었다면 이 스캔을 같이 갱신하라");
            Assert.IsEmpty(hits, "concrete 가 원점을 옛 갈래로 읽는다 — `target.Origin` 만 읽는다: " + string.Join(", ", hits));
        }

        [Test]
        public void 옛_사건_자리_필드는_참조가_없고_원점은_드레인_한_곳에서_채운다()
        {
            string scripts = Path.Combine(CoreGoldenStore.RepoRoot, "Assets/_Project/Scripts");
            var hits = new List<string>();
            var fillers = new List<string>();
            foreach (var sub in new[] { "Skills", "BattleCore", "BattleCoreUnity" })
            {
                foreach (var path in Directory.GetFiles(Path.Combine(scripts, sub), "*.cs", SearchOption.AllDirectories))
                {
                    string code = CodeOnly(path);
                    if (Regex.IsMatch(code, @"\bEventPosition\b|\bEventBodyRadius\b")) hits.Add(Path.GetFileName(path));
                    if (sub != "Skills" && Regex.IsMatch(code, @"new\s+SkillOrigin\s*\(")) fillers.Add(Path.GetFileName(path));
                }
            }
            Assert.IsEmpty(hits, "옛 사건 자리 필드가 되살아났다 — 원점은 `SkillOrigin` 이 나른다: " + string.Join(", ", hits));
            CollectionAssert.AreEqual(new[] { "TriggerDispatcher.cs" }, fillers,
                "원점을 채우는 곳은 드레인 하나다(`TriggerDispatcher.Execute`) — 늘면 원점 산출이 다시 갈라진다");
        }
}
}
