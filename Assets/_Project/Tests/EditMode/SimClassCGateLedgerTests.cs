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
    /// battle-sim-extraction unit 18-K/4 — **분류 C 게이트 13 건의 장부.**
    ///
    /// ## 분류 C 가 뭐고 왜 18-K 가 소유하나
    ///
    /// 게이트 53 의 전수 분류(계획서 §"게이트 53 의 처분")에서 C 는 **월드 싱글턴**이다 —
    /// `FlowFieldSingleton` ×9 · `HazardSingleton` ×2 · `ObstacleSingleton` ×1 ·
    /// `DefenderFieldSingleton` ×1. 다른 분류와 성격이 다르다:
    ///
    /// - **A(채널 싱글턴)** 는 증발한다 — 신 sim 의 채널은 생성자 주입이라 "부재" 상태가 없다.
    /// - **B(기믹 config)** 는 진짜 규칙이라 저작면으로 이사한다.
    /// - **D(일감 존재)** 는 대개 no-op 등가라 쿼리가 대신한다.
    /// - **C 는 "판이 준비됐나" = 기동 순서**다. 맵이 빌드되기 전에 sim 이 돌면 이 시스템들이
    ///   **통째로 쉬어야** 한다. 그래서 소유자가 개별 조각이 아니라 **조립 지점(18-K)** 이다.
    ///
    /// ## 왜 소스 스캔인가
    ///
    /// 이 게이트가 사라지는 방식은 "누가 early-return 한 줄을 지운다" 이고, 그때 **테스트는
    /// 조용히 초록**이다 — 싱글턴이 있는 상황만 테스트하면 게이트를 안 밟기 때문이다.
    /// 그래서 장부가 필요하다: **구 sim 의 `RequireForUpdate` 를 세어** 신 sim 의 early-return
    /// 개수·분포와 대조한다. 한쪽만 바뀌면 즉시 빨개진다.
    ///
    /// (`SimEngineIndependenceTests` 가 같은 방식으로 어셈블리 경계를 지킨다 — 선례.)
    ///
    /// ⚠ 이 테스트는 **게이트의 존재**를 지킬 뿐 동작을 검증하지 않는다. 각 시스템의 동작은
    /// 자기 클러스터 오라클이 본다. 둘을 합쳐야 "게이트가 있고, 있는 채로 옳게 돈다"가 된다.
    /// </summary>
    public class SimClassCGateLedgerTests
    {
        private static readonly string[] ClassC =
        {
            "FlowFieldSingleton", "HazardSingleton", "ObstacleSingleton", "DefenderFieldSingleton",
        };

        private static string LegacyRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Battle");

        private static string SimRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Sim", "Lib");

        /// <summary>
        /// 신 sim 의 **하드 게이트** 분포 — `(파일, 싱글턴) → 개수`.
        ///
        /// 하드 = `if (!SimSingleton.TryGet…) return;` (구 `RequireForUpdate` 의 번역).
        /// ⚠ **소프트 가드는 세지 않는다** — `bool hasFlowField = TryGet(…)` 로 받아 분기하는
        /// 자리(`AttackSystem`·`EnemyAiState`·`ProjectileHit`·`ProjectileMove`·`AggroState`)는
        /// 구 sim 에서도 `RequireForUpdate` 가 **없었다**. 필드 없이도 폴백으로 계속 도는 것이
        /// 그쪽 계약이라, 하드로 바꾸면 오히려 규칙이 바뀐다.
        /// </summary>
        private static readonly (string file, string singleton, int count, string legacySystem)[] Expected =
        {
            ("Combat/BossPeriodicTriggerSystem.cs", "FlowFieldSingleton", 1, "BossPeriodicTriggerSystem"),
            ("Combat/HealthThresholdSystem.cs",     "FlowFieldSingleton", 1, "HealthThresholdSystem"),
            ("Combat/ProjectileEmitterSystem.cs",   "FlowFieldSingleton", 1, "ProjectileEmitterSystem"),
            ("Effects/HazardCastSystem.cs",         "FlowFieldSingleton", 1, "HazardCastSystem"),
            ("Effects/ShieldCastSystem.cs",         "FlowFieldSingleton", 1, "ShieldCastSystem"),
            ("Effects/PickupAndHeatSystems.cs",     "FlowFieldSingleton", 1, "PickupConsumeSystem"),
            ("Movement/MovementSystem.cs",          "FlowFieldSingleton", 1, "MovementSystem"),
            // ⚠ 한 파일에 두 시스템이 산다 — ZoneApply(#5)와 PatrolField(#16) 둘 다 필드를 요구한다.
            ("Effects/ZoneAndPatrolSystems.cs",     "FlowFieldSingleton", 2, "ZoneApplySystem · PatrolFieldSystem"),
            ("Effects/ZoneAndPatrolSystems.cs",     "HazardSingleton",    1, "ZoneApplySystem"),
            // ⚠ 여기도 둘 — HazardLifetime(#2)과 ObstacleLifetime(#6).
            ("Effects/LifetimeSystems.cs",          "HazardSingleton",    1, "HazardLifetimeSystem"),
            ("Effects/LifetimeSystems.cs",          "ObstacleSingleton",  1, "ObstacleLifetimeSystem"),
            ("Effects/FieldBuilderSystems.cs",      "DefenderFieldSingleton", 1, "DefenderFieldSystem"),
        };

        /// `if (!SimSingleton.TryGet<X>(…)) return;` 과 `if (!SimSingleton.TryGet(world, out X …)) return;` 둘 다.
        private static int CountHardGates(string source, string singleton)
        {
            // 두 표기가 공존한다: `TryGet<X>(world, out var v)` 와 `TryGet(world, out X v)`.
            // 한 정규식으로 접으면 소프트 가드(`bool has… = TryGet(…)`)까지 걸리므로 나눠 센다.
            int generic = Regex.Matches(source,
                @"if\s*\(\s*!\s*SimSingleton\.TryGet<\s*" + singleton + @"\s*>\([^)]*\)\s*\)\s*return\s*;").Count;
            int outTyped = Regex.Matches(source,
                @"if\s*\(\s*!\s*SimSingleton\.TryGet\(\s*world\s*,\s*out\s+" + singleton + @"\s+\w+\s*\)\s*\)\s*return\s*;").Count;
            return generic + outTyped;
        }

        private static string ReadSim(string relative)
        {
            string path = Path.Combine(SimRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), $"신 sim 파일이 없다: {path}");
            return File.ReadAllText(path);
        }

        // ── 구 sim 쪽 진실 ───────────────────────────────────────────────────

        /// 구 `RequireForUpdate<분류C>` 를 전수로 센다 — 이것이 장부의 상대편이다.
        private static Dictionary<string, int> LegacyGateCounts()
        {
            var counts = ClassC.ToDictionary(s => s, _ => 0);
            foreach (string path in Directory.GetFiles(LegacyRoot, "*.cs", SearchOption.AllDirectories))
            {
                string src = File.ReadAllText(path);
                foreach (string s in ClassC)
                    counts[s] += Regex.Matches(src, @"RequireForUpdate<\s*" + s + @"\s*>").Count;
            }
            return counts;
        }

        [Test]
        public void 구_sim_의_분류C_게이트는_정확히_13건이다()
        {
            var counts = LegacyGateCounts();
            Assert.AreEqual(9, counts["FlowFieldSingleton"], "FlowField ×9");
            Assert.AreEqual(2, counts["HazardSingleton"], "Hazard ×2 (HazardLifetime · ZoneApply)");
            Assert.AreEqual(1, counts["ObstacleSingleton"]);
            Assert.AreEqual(1, counts["DefenderFieldSingleton"]);
            Assert.AreEqual(13, counts.Values.Sum(),
                "계획서 §게이트 53 의 처분: 분류 C = 13. 이 수가 변했으면 장부부터 갱신한다.");
        }

        // ── 신 sim 쪽 장부 ───────────────────────────────────────────────────

        [Test]
        public void 신_sim_이_분류C_게이트를_전부_early_return_으로_들고_있다()
        {
            var missing = new List<string>();
            foreach (var (file, singleton, count, legacy) in Expected)
            {
                int actual = CountHardGates(ReadSim(file), singleton);
                if (actual != count)
                    missing.Add($"{file} / {singleton}: 기대 {count} · 실제 {actual}  (구 {legacy})");
            }
            CollectionAssert.IsEmpty(missing,
                "분류 C 게이트가 증발했다 — 맵 빌드 전에 이 시스템이 돌면 판이 갈린다:\n"
                + string.Join("\n", missing));
        }

        [Test]
        public void 신_sim_의_게이트_총량이_구와_같다()
        {
            // ⚠ 이 단정이 장부의 핵심이다. 한쪽만 늘거나 줄면 여기서 잡힌다 —
            //   구에 게이트가 추가됐는데 이식을 잊은 경우도 포함.
            var legacy = LegacyGateCounts();
            foreach (string s in ClassC)
            {
                int ported = Expected.Where(e => e.singleton == s).Sum(e => e.count);
                Assert.AreEqual(legacy[s], ported, $"{s}: 구 {legacy[s]} · 신 {ported}");
            }
            Assert.AreEqual(13, Expected.Sum(e => e.count));
        }

        [Test]
        public void 소프트_가드는_장부에_넣지_않는다()
        {
            // 구 sim 에 `RequireForUpdate` 가 **없는** 시스템들이다 — 필드가 없어도 폴백으로
            // 계속 돈다. 하드로 바꾸면 규칙이 바뀌므로, 이 자리에 early-return 이 생기면 실패한다.
            var softFiles = new[]
            {
                "Combat/AttackSystem.cs", "Combat/EnemyAiSystems.cs",
                "Combat/ProjectileHitSystem.cs", "Combat/ProjectileMoveSystem.cs",
                "Effects/AggroStateSystem.cs",
            };
            foreach (string f in softFiles)
            {
                string src = ReadSim(f);
                Assert.AreEqual(0, CountHardGates(src, "FlowFieldSingleton"),
                    $"{f}: 구 sim 은 이 시스템을 필드 부재로 막지 않는다 — 하드 게이트는 규칙 변경이다");
            }

            // 그리고 실제로 소프트 가드를 들고 있어야 한다(그냥 지운 것과 구분).
            StringAssert.Contains("hasFlowField", ReadSim("Combat/AttackSystem.cs"));
        }

        [Test]
        public void 장부의_파일이_전부_존재한다()
        {
            // 파일이 이사하면 위 정규식이 **0 을 세는 대신 예외**로 죽어야 한다 —
            // 조용히 "게이트 0 건" 이 되는 것이 이 장부의 최악 실패 모드다.
            foreach (string file in Expected.Select(e => e.file).Distinct())
                Assert.DoesNotThrow(() => ReadSim(file), file);
        }
    }
}
