using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-K/5 — **게이트 53 계정 감사.**
    ///
    /// 계획서 §"게이트 53 의 처분" 이 산문으로 적은 분류를 **실행 가능한 장부**로 바꾼다.
    /// 산문 분류의 문제는 코드가 바뀌어도 조용하다는 것이다 — 구 sim 에 게이트가 하나 붙으면
    /// 합계가 54 가 되지만 문서는 계속 53 이라고 말한다.
    ///
    /// | 분류 | 수 | 신 sim 에서의 운명 |
    /// |---|---:|---|
    /// | **A. 채널 싱글턴** | 14 | **증발** — 채널은 생성자 주입이라 "부재" 상태가 없다 |
    /// | **B. 기믹 config** | 7 | **이사** — 부재 = 기능 비활성이라 진짜 규칙이고, 저작면으로 간다 |
    /// | **C. 월드 싱글턴** | 13 | **이식** — 판이 준비됐나 = 기동 순서 (`SimClassCGateLedgerTests`) |
    /// | **D. 일감 존재** | 19 | 대개 **no-op 등가** — 쿼리가 비면 루프가 안 돈다 |
    ///
    /// ⚠ **D 를 버려도 되는지 가르는 질문은 하나다**(계획서): *그 시스템은 쿼리 루프 **밖**에서
    /// 상태를 바꾸는가?* — ① 채널 드레인 ② RNG 전진 ③ 싱글턴 갱신. 하나라도 예면 early-return
    /// 으로 이식해야 한다. 특히 ②는 상태 해시에 실려(`_meteorRng`) parity 가 조용히 깨진다.
    /// 그 판정은 각 조각이 자기 시스템에서 했고, 이 감사는 **분류가 빠짐없이 됐는지**만 본다.
    /// </summary>
    public class SimGate53AuditTests
    {
        private static string LegacyRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Battle");

        private static string SimRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Sim", "Lib");

        // ── 분류표 (계획서 §게이트 53 의 처분) ────────────────────────────────

        /// 부재 = 파이프라인 정지. 신 sim 의 채널은 항상 존재하므로 **게이트가 증발**한다.
        private static readonly string[] ClassA =
        {
            "StatModifierApplyEventsSingleton", "EnemyCcEventsSingleton",
            "StackModifierApplyEventsSingleton", "MeteorBarrageRequestsSingleton",
            "HazardSpawnRequestsSingleton", "DotApplyEventsSingleton",
            "CcClearRequestsSingleton", "BlinkRequestEventsSingleton",
        };

        /// 싱글턴 **존재 = 기믹 켜짐** 관용구. 부재는 진짜 규칙이라 저작면으로 이사한다.
        private static readonly string[] ClassB =
        {
            "RedBullGimmickConfig", "ClockOutGimmickConfig",
            "OnsenGimmickConfig", "BurnoutGimmickConfig",
        };

        /// 맵/브리지 라이프사이클 커플링 — 기동 순서. 상세 장부는 `SimClassCGateLedgerTests`.
        private static readonly string[] ClassC =
        {
            "FlowFieldSingleton", "HazardSingleton", "ObstacleSingleton", "DefenderFieldSingleton",
        };

        /// 그 기능의 대상이 있을 때만 도는 콘텐츠 게이트.
        private static readonly string[] ClassD =
        {
            "AttackState", "ProjectileTag", "DreamCocoon", "LethalTimer", "HitFlashTag",
            "UltimateLeapState", "EmitterInstance", "SummonedBy", "AllyBuffField",
            "PatrolAnchor", "DcTriggerSlot", "HazardCastState", "ShieldCastState",
            "EnemyAiState", "IncomingDamage", "Health", "PathFollowState", "PickupSpawnState",
        };

        // ── 구 sim 스캔 ──────────────────────────────────────────────────────

        private static List<string> LegacyGateTypes()
        {
            var hits = new List<string>();
            foreach (string path in Directory.GetFiles(LegacyRoot, "*.cs", SearchOption.AllDirectories))
                foreach (Match m in Regex.Matches(File.ReadAllText(path),
                             @"RequireForUpdate<\s*([A-Za-z0-9_.]+)\s*>"))
                    hits.Add(m.Groups[1].Value.Split('.').Last());
            return hits;
        }

        [Test]
        public void 게이트_총계가_53_이다()
        {
            Assert.AreEqual(53, LegacyGateTypes().Count,
                "계획서 §게이트 53 의 처분의 전제. 이 수가 변했으면 분류표부터 갱신한다 — " +
                "새 게이트가 분류 없이 들어오면 이식에서 조용히 빠진다.");
        }

        [Test]
        public void 모든_게이트가_정확히_한_분류에_속한다()
        {
            var table = new Dictionary<string, string>();
            void Put(string[] types, string cls)
            {
                foreach (string t in types)
                {
                    Assert.IsFalse(table.ContainsKey(t), $"{t} 가 두 분류에 있다({table.GetValueOrDefault(t)}·{cls})");
                    table[t] = cls;
                }
            }
            Put(ClassA, "A"); Put(ClassB, "B"); Put(ClassC, "C"); Put(ClassD, "D");

            var unclassified = LegacyGateTypes().Where(t => !table.ContainsKey(t)).Distinct().ToList();
            CollectionAssert.IsEmpty(unclassified,
                "분류되지 않은 게이트 — 처분(증발/이사/이식/no-op)이 정해지지 않았다:\n  "
                + string.Join("\n  ", unclassified));

            // 반대 방향: 표에만 있고 코드에 없는 항목 = 구 sim 이 게이트를 뗐다는 뜻.
            var stale = table.Keys.Except(LegacyGateTypes()).ToList();
            CollectionAssert.IsEmpty(stale, "분류표에만 남은 항목:\n  " + string.Join("\n  ", stale));
        }

        [Test]
        public void 분류별_개수가_계획서와_같다()
        {
            var byType = LegacyGateTypes().GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
            int Count(string[] types) => types.Sum(t => byType.GetValueOrDefault(t));

            Assert.AreEqual(14, Count(ClassA), "A 채널 싱글턴");
            Assert.AreEqual(7, Count(ClassB), "B 기믹 config");
            Assert.AreEqual(13, Count(ClassC), "C 월드 싱글턴");
            Assert.AreEqual(19, Count(ClassD), "D 일감 존재");
            Assert.AreEqual(53, Count(ClassA) + Count(ClassB) + Count(ClassC) + Count(ClassD));
        }

        // ── 처분이 실제로 이뤄졌나 ────────────────────────────────────────────

        /// `//` 이후를 자른다. 주석에 남은 증발 기록(장부)은 위반이 아니다 — 오히려 권장이다.
        private static string StripComment(string line)
        {
            int i = line.IndexOf("//", StringComparison.Ordinal);
            return i >= 0 ? line.Substring(0, i) : line;
        }

        private static IEnumerable<string> SimFiles()
            => Directory.GetFiles(SimRoot, "*.cs", SearchOption.AllDirectories);

        [Test]
        public void 분류A_는_신_sim_코드에_흔적이_없다()
        {
            // 채널은 `SimChannels` 필드로 **항상 존재**한다 — "부재" 를 표현할 타입 자체가 없어야
            // 증발이 진짜다. 주석의 장부는 통과시킨다(실제로 `GimmickSystems.cs` 가 그렇게 적는다).
            var hits = new List<string>();
            foreach (string path in SimFiles())
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripComment(lines[i]);
                    foreach (string t in ClassA)
                        if (code.Contains(t)) hits.Add($"{Path.GetFileName(path)}:{i + 1}  {t}");
                }
            }
            CollectionAssert.IsEmpty(hits,
                "분류 A 는 증발이어야 한다 — 채널 존재를 코드로 묻는 순간 없는 상태가 생긴다:\n  "
                + string.Join("\n  ", hits));
        }

        [Test]
        public void 분류B_는_증발이_아니라_저작면으로_이사했다()
        {
            // ⚠ 여기가 A 와 갈리는 지점이다. 기믹을 끈 매치와 켠 매치는 **다른 판**이므로
            //   부재가 규칙이고, 규칙은 사라지면 안 된다. `SimConfig.ClockOut` 이 첫 사례였다.
            string all = string.Join("\n", SimFiles().Select(File.ReadAllText));
            foreach (string t in ClassB)
                StringAssert.Contains(t, all,
                    $"{t} 가 신 sim 에서 사라졌다 — 기믹 비활성이 표현되지 않으면 항상 켜진 판이 된다");
        }

        [Test]
        public void 분류C_는_이식이고_상세는_전용_장부가_본다()
        {
            // 여기서는 개수만 재확인한다(상세 분포는 `SimClassCGateLedgerTests`).
            var byType = LegacyGateTypes().GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(9, byType.GetValueOrDefault("FlowFieldSingleton"));
            Assert.AreEqual(13, ClassC.Sum(t => byType.GetValueOrDefault(t)));
        }

        [Test]
        public void 스캔이_실제로_파일을_읽는다()
        {
            // 경로 오타 하나면 위 단정이 전부 영원히 초록이다 — 이 spec 이 반복해 경계하는 모양.
            Assert.Greater(Directory.GetFiles(LegacyRoot, "*.cs", SearchOption.AllDirectories).Length, 50);
            Assert.Greater(SimFiles().Count(), 30);
        }
    }
}
