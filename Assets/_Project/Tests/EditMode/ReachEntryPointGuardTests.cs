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

        // ⚠ **Bridge 를 빼면 사각지대가 된다**(리뷰 M-3) — 표기 소비처 3곳과 판정 모양 코드
        // (`CollectShieldBreakTargets`)가 전부 거기 산다. sim 은 아니지만 «판정을 흉내내는» 코드다.
        private static string BridgeRoot =>
            Path.Combine(UnityEngine.Application.dataPath, "_Project", "Scripts", "Bridge");

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

        // ★★ **복사 지점을 고정한다 — 이 그물이 없어서 unit 23b 가 런타임 효과 0 으로 나갔다.**
        //
        // 초판은 `originBodyRadius` 를 `BallisticArcToPoint` **분기에만** 대입했는데, 자기 자리
        // 폭발 4종(자폭·사망폭발·보스 도약·궁극기 슬램)이 전부 `SkyFall` 이라 **값이 상태에
        // 한 번도 안 실렸다.** 생산자도 소비자도 옳았고 **그 사이의 복사 지점**만 비어 있었다 —
        // 종전 그물은 양 끝만 봐서 통째로 놓쳤다. `0` 이 「자리형」과 「안 실었다」를 겸직해
        // 관측상 완전히 조용했고, 골든 A/B 가 무변으로 나온 이유의 절반이 이것이다.
        [Test]
        public void OriginRadius_IsCopiedInTheCommonInitializer_NotPerBranch()
        {
            var src = Read("Bridge", "BattleBridge.cs");

            // 오브젝트 초기화자 형태(`originBodyRadius = req.originBodyRadius,`)여야 한다.
            Assert.IsTrue(Regex.IsMatch(src, @"\n\s*originBodyRadius\s*=\s*req\.originBodyRadius\s*,"),
                "요청 → 상태 복사가 «공통 초기화 블록» 에 없다. 분기에 두면 다음 movement 가 생길 때 "
                + "같은 누락이 재발하고, 기본 0 = 레거시라 «조용히» 산다");

            // 분기 대입(`state.originBodyRadius = …`)은 금지 — 그게 이 결함의 형태였다.
            Assert.IsFalse(Regex.IsMatch(src, @"state\.originBodyRadius\s*="),
                "분기별 대입이 부활했다 — movement 하나를 빠뜨리면 그 경로만 조용히 옛 자로 돈다");
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

        [Test]
        public void ImpactJudgment_ReadsTheCarriedOrigin_NotACellConstant()
        {
            var src = Read("Battle", "Combat", "Projectile", "ProjectileHitSystem.cs");
            Assert.IsTrue(src.Contains("SkillMath.ReachFromImpact("),
                "착탄 광역이 실려 온 원점을 안 읽는다 — 자기 자리 폭발이 칸 반폭으로 잘린다");
            Assert.IsTrue(src.Contains("originBodyRadius"),
                "요청에 실린 원점 반경이 판정까지 도달하지 않는다");
        }

        // 표기도 판정과 같은 자를 지나야 한다. 착지 예고는 **화면이 규칙을 가르치는 자리**다.
        //
        // ⚠ **점 + 거리이지 칸 열거가 아니다**(2026-09-07 사용자 결정). 「점에서 계산한 칸 집합」은
        // 「점 + 거리」를 이산 격자로 **양자화**하는 것이고, 그 양자화가 몸 있는 유닛을 예고 밖에서
        // 맞게 만들었다(실측 32곳).
        [Test]
        public void LandingTelegraph_IsAPointAndRadius_NotACellEnumeration()
        {
            var view = Read("Bridge", "BattleBridge.UltimateLeap.cs");
            Assert.IsTrue(Regex.IsMatch(view,
                    @"SetTelegraphRing\(\s*new Vector2\(\s*leap\.landingCell"),
                "예고가 착지 좌표 중심의 링을 안 그린다 — 「점 + 거리」가 깨졌다");
            Assert.IsTrue(view.Contains("CenteredRingRadius(leap.slamTileRange)"),
                "예고 반경이 운석과 **같은 단일 지점**에서 오지 않는다 — 두 벌이 되면 한쪽만 조용히 갈린다");
            // ⚠ **«있으면 통과» 로는 부족하다** — 몸을 실은 두 번째 그리기 분기를 옆에 추가해도
            // 위 단언은 초록이다. 링은 하나뿐이어야 한다(그려지는 원이 둘이면 어느 쪽이 규칙인가?).
            Assert.AreEqual(1, CountOf(StripComments(view), "SetTelegraphRing("),
                "예고를 그리는 자리가 하나가 아니다 — 분기가 늘면 어느 원이 규칙인지 말할 수 없다");

            // ⚠ **주석은 빼고 «코드» 만 본다.** 이 헤더가 금지어를 이력으로 언급하기 때문이다.
            // 초판은 `Contains(banned) && !view.Contains("되돌리지 말 것")` 이었는데, 그 문구가
            // 같은 파일 주석에 있어 조건이 **항상 거짓** = 칸 열거가 되살아나도 통과하는
            // **vacuous 가드**였다(이 레포에서 세 번째다).
            var code = StripComments(view);
            foreach (var banned in new[] { "SetTelegraphCells", "BuildZoneCells", "_zoneCellScratch" })
                Assert.IsFalse(code.Contains(banned),
                    $"예고가 칸 열거(`{banned}`)로 되돌아갔다 — 「점 + 거리」가 이산 격자로 "
                    + "양자화되면서 몸 있는 유닛이 예고 밖에서 맞는 오차가 그대로 돌아온다");

            // 칸 열거 자체가 브리지에서 사라졌는지 — 소비처 0 인 열거가 남아 있으면 다음 사람이 되쓴다.
            var bridge = Read("Bridge", "BattleBridge.cs");
            Assert.IsFalse(Regex.IsMatch(bridge, @"private\s+void\s+BuildZoneCells\("),
                "`BuildZoneCells` 가 되살아났다 — 소비처 0 이면 지운다(제약 8)");
        }

        // ★★ **예고는 «전용» 채널이어야 한다 — 이 그물이 가장 비싼 회귀를 막는다.**
        //
        // 한 번 `PinSkillTelegraph`(공유 `_rangeOwner` 채널)로 그렸다가 리뷰가 잡았다(2026-09-07).
        // 그 채널은 `SetPlacementRange`/`SetAreaRange` 가 매번 `ClearPlacementRange()` 로 시작하는
        // **단일 owner set/clear** 라, 예고 2초 동안:
        //   · 플레이어가 유닛을 드래그하면 배치 프리뷰가 **예고를 지우고**, 그 뒤
        //     `ClearSkillTelegraph` 는 owner 불일치로 no-op — **재페인트 경로가 없어** 남은 시간
        //     전부 무경고로 착탄한다(탭 배치 peek 는 **매 프레임** 훔쳐서 예고가 1프레임만 뜬다).
        //   · 반대로 예고가 배치 사거리 타일·링·타겟 마크를 통째로 지운다.
        //   · 운석 착탄 예고와 owner 를 겸해 **서로를 지운다**(`_skillTelegraphProjectile` 도 stale).
        // 「예고 중 배치는 막을 수 없다 — 유닛을 빼고 다시 놓는 것이 이 스킬의 놀이다.」
        [Test]
        public void LandingTelegraph_UsesItsOwnChannel_NotTheSharedRangeOwner()
        {
            var code = StripComments(Read("Bridge", "BattleBridge.UltimateLeap.cs"));
            foreach (var shared in new[]
                     { "PinSkillTelegraph", "ClearSkillTelegraph", "SetAreaRange", "SetRangeOwner" })
                Assert.IsFalse(code.Contains(shared),
                    $"예고가 공유 범위 채널(`{shared}`)을 쓴다 — 배치 프리뷰·운석 예고와 서로를 지운다. "
                    + "예고는 `SetTelegraphRing`/`ClearTelegraphRing` 전용 채널만 쓴다");

            // 전용 채널이 실제로 존재하는지 — 없으면 위 금지 스캔이 vacuous 해진다(존재 단언 짝).
            var mapView = Read("Core", "TilemapMapView.cs");
            Assert.IsTrue(mapView.Contains("public void SetTelegraphRing("),
                "전용 예고 링 채널이 없다 — 이름이 바뀌었다면 위 금지 스캔이 무의미해진 것이다");
            Assert.IsTrue(mapView.Contains("public void ClearTelegraphRing("), "전용 clear 가 없다");
        }

        // 반경은 **운석과 공유하는 단일 지점**에서 온다. `N + 칸 반폭` 이 D6 계약(2026-09-02)이고,
        // 「대상 몸은 그림자가 말한다」가 성립하는 것은 이 값이 판정의 **원점 항과 같기** 때문이다.
        [Test]
        public void CenteredRingRadius_IsTheSingleSource_AndCarriesNoBody()
        {
            var bridge = Read("Bridge", "BattleBridge.cs");
            var body = MethodBody(bridge, bridge.IndexOf("internal static float CenteredRingRadius("));
            Assert.IsTrue(Regex.IsMatch(body,
                    @"tileRange\s*\+\s*Wassup\.Skills\.SkillMath\.CellShapePaddingTiles"),
                "링 반경이 `N + 칸 반폭` 이 아니다 — 이 값이 판정의 원점 항과 갈리면 "
                + "「그림자가 링에 닿으면 걸린다」가 더는 판정식과 동치가 아니다");
            Assert.IsFalse(Regex.IsMatch(body, @"(?:BodyRadius|bodyR)"),
                "링 반경에 «몸» 이 들어왔다 — 대상 몸은 그림자가 말한다(D6). 표기가 몸을 알면 "
                + "「누구 기준이냐」가 생긴다");

            // 운석 경로도 같은 지점을 쓰는가 — 두 벌이 되면 한쪽만 조용히 갈린다.
            var pin = MethodBody(bridge, bridge.IndexOf("private void PinCenteredRange("));
            Assert.IsTrue(pin.Contains("CenteredRingRadius(tileRange)"),
                "운석·착탄 예고가 공유 반경 지점을 안 쓴다 — 착지 예고와 갈린다");
        }

        // ⚠ **착지 슬램의 «형» 을 세 파일에서 함께 고정한다**(2026-09-07 사용자 결정).
        // 착지 슬램(도약·강습)은 **운석과 같은 「자리에 떨어지는 것」**이다 — 보스가 «지정한 좌표»에
        // 내리는 것이지 그 몸이 뻗는 것이 아니다. unit 23b 가 이것을 몸형으로 읽어 `HitRadius` 를
        // 실었고 unit 24 가 그 전제 위에 예고를 지었다 — **둘 다 정정된 것이 이 단언이다.**
        //
        // 형이 갈리면 화면과 판정이 서로 다른 규칙을 말한다. 그래서 **예고 1 + 생산자 2** 를
        // 한 테스트에 묶는다 — 하나만 되돌려도 여기서 빨개진다(unit 23b 는 «양 끝만» 보다 놓쳤다).
        // ⚠ 「1 + 2」가 **1:1 대응이 아니다**(리뷰 L-7): 예고는 **궁극기 강습에만** 있다.
        //   일반 보스 도약은 슬램만 쏘고 예고 타일을 안 칠한다 — 그래서 ①은 생산자 ②의 짝이다.
        [Test]
        public void LandingSlam_IsAPlaceForm_InTelegraphAndBothProducers()
        {
            // ① 예고 — 도약 주체의 몸을 **읽지 않는다**(주석의 언급은 통과, 실제 호출만 잡는다).
            var view = Read("Bridge", "BattleBridge.UltimateLeap.cs");
            Assert.IsFalse(Regex.IsMatch(view, @"GetComponentData<[^>]*HitRadius>"),
                "예고가 다시 보스 몸을 읽는다 — 자리형을 몸형으로 바꿔 그리게 된다");
            // ⚠ **헬퍼 경유 재유입도 막는다**(리뷰 M-3). 위 단언은 `HitRadius` **직접 읽기**만 본다.
            // 같은 partial 클래스에 `HostBodyRadiusOf(Entity)` 가 살아 있고(실드 파열이 실사용),
            // `6b1bb6ff` 이전의 `BattleBridge.BossLeap.cs` 가 **정확히 그 헬퍼로** 몸을 실었다.
            // 제약 13 이 이 실패 모양을 문장으로 적어 뒀다 — 「흔적이 **함수 뒤에 숨어** grep 이 못 잡는다」.
            Assert.IsFalse(view.Contains("HostBodyRadiusOf("),
                "예고가 헬퍼를 거쳐 몸을 읽는다 — 직접 읽기만 막는 그물은 이 경로를 통과시킨다");

            // ② 강습 슬램(sim) · ③ 일반 도약 슬램(브리지) — 둘 다 0 을 싣는다.
            // 0 은 「이 자리에 주인이 없다」이고 판정이 칸 반폭으로 접는다(`ReachFromImpact`).
            foreach (var (src, who) in new[]
                     {
                         (Read("Battle", "Combat", "UltimateLeapSystem.cs"), "강습 슬램"),
                         (Read("Bridge", "BattleBridge.BossLeap.cs"),        "일반 도약 슬램"),
                     })
            {
                Assert.IsTrue(Regex.IsMatch(src, @"originBodyRadius\s*=\s*0f\s*,"),
                    $"{who} 가 원점 항 0(자리형)을 안 싣는다");
                // ⚠ 부정 룩어헤드를 `=` 바로 뒤에 붙인다 — `=\s*(?!0f)` 로 쓰면 `\s*` 가 되짚어
                // 공백 한 칸을 덜 먹은 자리에서 룩어헤드가 성립해 **항상 매치**한다(vacuous 의 반대: 상시 실패).
                Assert.IsFalse(Regex.IsMatch(src, @"originBodyRadius\s*=(?!\s*0f\s*,)"),
                    $"{who} 가 0 이 아닌 원점 항을 싣는다 — 착지 슬램은 자리형이다");
            }
        }
    }
}
