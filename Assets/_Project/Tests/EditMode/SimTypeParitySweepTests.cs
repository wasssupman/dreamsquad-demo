using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-M — **타입 대조 오라클 (자동 발견).**
    ///
    /// 21 타입은 `SimLegacyTraceContractTests` 가 손 목록으로 대조하지만 나머지는 **미검증
    /// 표면**이었다. 손 목록을 118 로 늘리는 대신 **리플렉션이 두 어셈블리에서 동명 struct 를
    /// 자동 매칭**한다 — 목록이 없으므로 목록이 낡을 수도 없고, 구 쪽에 타입이 추가되면
    /// 그것도 자동으로 걸린다.
    ///
    /// 잡는 것:
    /// ① 이식이 필드를 빠뜨리거나 개명함 ② 구 쪽 필드·타입 변경을 sim 이 못 따라감
    /// ③ 구 데이터 타입의 통째 이식 누락(old-only 장부) ④ 동명 enum 의 멤버/값 드리프트.
    ///
    /// 안 잡는 것(소유자 명시):
    /// 값 변환의 충실함 — bake 오라클(18-N~P) · 개명 이식의 필드 대응 — 개명 장부의 소유자
    /// (예: `StackThresholdRule` ← `ThresholdRule` 는 `BattleBridge.Shadow`).
    ///
    /// ⚠ 이 대조가 가능한 창은 **구 sim 이 살아 있는 units 18~20 뿐**이다.
    /// 전체 보고서는 실행마다 `Temp/sim-type-parity-report.txt` 로 나간다.
    /// </summary>
    public class SimTypeParitySweepTests
    {
        // ── 표면 정의 ─────────────────────────────────────────────────────────

        private static readonly string[] NewContexts =
        {
            "Wassup.Sim.Units", "Wassup.Sim.Movement", "Wassup.Sim.Combat", "Wassup.Sim.Effects",
        };

        private static bool IsNewSurface(Type t)
            => t.Namespace != null && NewContexts.Contains(t.Namespace);

        /// 매칭 후보는 `Wassup.Battle.*` + `Wassup.Data`(아키텍처 중립 정의 계층 — `PatternSpec` 선례).
        private static bool IsOldSurface(Type t)
            => t.Namespace != null
               && (t.Namespace == "Wassup.Data"
                   || t.Namespace == "Wassup.Battle"
                   || t.Namespace.StartsWith("Wassup.Battle.", StringComparison.Ordinal));

        /// old-only **감사**는 `Wassup.Battle.*` 만 — `Wassup.Data` 는 저작 SO 계층이라
        /// sim 에 없는 것이 정상이다(있으면 매칭으로 잡히고, 없어도 결손이 아니다).
        private static bool IsOldAuditSurface(Type t)
            => t.Namespace != null
               && (t.Namespace == "Wassup.Battle"
                   || t.Namespace.StartsWith("Wassup.Battle.", StringComparison.Ordinal));

        private static bool IsPlainStruct(Type t)
            => t.IsValueType && !t.IsEnum && !t.IsPrimitive && t.IsPublic && !t.IsNested
               && !t.Name.Contains("<")
               // ⚠ 구 ISystem 구현체도 public struct 다 — 시스템은 데이터가 아니고 T1 이
               //   클래스로 옮겼다(첫 스윕에서 44개가 old-only 로 쏟아진 원인).
               && !typeof(Unity.Entities.ISystem).IsAssignableFrom(t);

        private static bool IsPlainEnum(Type t) => t.IsEnum && t.IsPublic && !t.IsNested;

        private static IEnumerable<Type> NewTypes()
            => typeof(Wassup.Sim.SimWorld).Assembly.GetTypes().Where(IsNewSurface);

        private static IEnumerable<Type> OldTypes()
        {
            var asms = new HashSet<Assembly>
            {
                typeof(Wassup.Battle.Units.Health).Assembly,
                typeof(Wassup.Data.PatternSpec).Assembly,
            };
            return asms.SelectMany(a => a.GetTypes()).Where(IsOldSurface);
        }

        // ── 장부 (이유 없는 항목 금지) ────────────────────────────────────────

        /// <summary>
        /// old-only 허용 — 패턴: 채널 싱글턴은 `SimChannels` 생성자 주입으로 **증발**했다
        /// (게이트 53 분류 A). 이름 규약이 일정해서 패턴 하나가 전부를 덮는다.
        /// </summary>
        private static bool OldOnlyAllowedByPattern(string name)
            => name.EndsWith("EventsSingleton", StringComparison.Ordinal)
               || name.EndsWith("RequestsSingleton", StringComparison.Ordinal);

        /// old-only 개별 허용 — **이유가 곧 소유자 포인터**다. 첫 스윕(2026-08-06) 결과로 채웠다.
        private static readonly Dictionary<string, string> OldOnlyAllowed = new Dictionary<string, string>
        {
            ["ClockOutGimmickConfig"] = "분류 B 이사 + 개명 — `SimConfig.ClockOut`(`ClockOutConfig`)",
            ["BattleTimeScale"] = "시계 스케일 싱글턴 — 신 sim 은 스케일된 dt 를 받는다(P0). 처분은 unit 19 시계 정책",
            ["SimEntityId"] = "컴포넌트 → 핸들 승격(`Wassup.Sim.SimEntityId`, Core) — 순번 대응은 `SpawnOrdinal`(18-K/2a)",
        };

        /// new-only 개별 허용 — 개명 이식과 신 sim 고유 어휘. 이유 필수.
        private static readonly Dictionary<string, string> NewOnlyAllowed = new Dictionary<string, string>
        {
            ["SimTransform"] = "구 `LocalTransform` − `Rotation` — 대조는 `SimLegacyTraceContractTests` 소유",
            ["StackThresholdRule"] = "구 `Wassup.Data.ThresholdRule` + `kind` 축 — 변환은 `BattleBridge.Shadow.BuildSimStackThresholds` 소유",
        };

        /// 매칭 쌍에서 특정 필드의 상이를 허용 — 원칙적으로 비어 있어야 한다.
        private static readonly Dictionary<string, string> FieldDiffAllowed = new Dictionary<string, string>
        {
            ["HazardSingleton"] = "`NativeParallelMultiHashMap` → sim 소유 `HazardCellIndex` — " +
                                  "순회 순서 보존(tie-break ⑥)은 18-E 오라클 소유",
        };

        // ── 타입 번역 (필드 타입 대응 판정) ───────────────────────────────────

        private static readonly Dictionary<string, string> TypePairs = new Dictionary<string, string>
        {
            ["float3"] = "SimVec3",
            ["float2"] = "SimVec2",
            ["int2"] = "SimInt2",
            ["Random"] = "SimRandom",
            ["Entity"] = "SimEntityId",
        };

        private static bool Correspond(Type oldT, Type newT)
        {
            if (oldT == newT) return true;                                  // primitive · 공유 타입
            if (oldT.IsEnum && newT.IsEnum) return oldT.Name == newT.Name;  // 값 평행성은 enum 스윕이 본다
            if (oldT.Name == newT.Name) return true;                        // 동명 struct — 자기 쌍에서 검사된다
            if (TypePairs.TryGetValue(oldT.Name, out string mapped) && mapped == newT.Name) return true;

            // 컨테이너: NativeArray<T>/FixedList*Bytes<T> → T'[]
            if (oldT.IsGenericType && newT.IsArray)
            {
                string def = oldT.GetGenericTypeDefinition().Name;
                if (def.StartsWith("NativeArray", StringComparison.Ordinal)
                    || def.StartsWith("FixedList", StringComparison.Ordinal))
                    return Correspond(oldT.GetGenericArguments()[0], newT.GetElementType());
            }
            // 컨테이너: NativeHashSet<T> → HashSet<T'> (`ObstacleSingleton.blockedCells`)
            if (oldT.IsGenericType && newT.IsGenericType
                && oldT.GetGenericTypeDefinition().Name.StartsWith("NativeHashSet", StringComparison.Ordinal)
                && newT.GetGenericTypeDefinition() == typeof(HashSet<>))
                return Correspond(oldT.GetGenericArguments()[0], newT.GetGenericArguments()[0]);
            if (oldT.IsArray && newT.IsArray)
                return Correspond(oldT.GetElementType(), newT.GetElementType());
            return false;
        }

        // ── 스윕 본체 ─────────────────────────────────────────────────────────

        private sealed class SweepResult
        {
            public int MatchedStructs, MatchedEnums;
            public readonly List<string> Ambiguities = new List<string>();
            public readonly List<string> FieldMismatches = new List<string>();
            public readonly List<string> EnumMismatches = new List<string>();
            public readonly List<string> OldOnlyUnexplained = new List<string>();
            public readonly List<string> NewOnlyUnexplained = new List<string>();
        }

        private static readonly Lazy<SweepResult> Sweep = new Lazy<SweepResult>(Compute);

        private static Dictionary<string, Type> Unique(IEnumerable<Type> ts, List<string> ambiguities, string side)
        {
            var map = new Dictionary<string, Type>();
            foreach (var g in ts.GroupBy(t => t.Name))
            {
                if (g.Count() > 1)
                    ambiguities.Add($"{side} '{g.Key}' ×{g.Count()}: {string.Join(" · ", g.Select(t => t.FullName))}");
                else map[g.Key] = g.Single();
            }
            return map;
        }

        private static string[] FieldNames(Type t) => t
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        private static SweepResult Compute()
        {
            var r = new SweepResult();
            var report = new StringBuilder(16384);

            Dictionary<string, Type> oldStructs = Unique(OldTypes().Where(IsPlainStruct), r.Ambiguities, "old");
            Dictionary<string, Type> newStructs = Unique(NewTypes().Where(IsPlainStruct), r.Ambiguities, "new");
            Dictionary<string, Type> oldEnums = Unique(OldTypes().Where(IsPlainEnum), r.Ambiguities, "old-enum");
            Dictionary<string, Type> newEnums = Unique(NewTypes().Where(IsPlainEnum), r.Ambiguities, "new-enum");

            // ① 매칭 쌍 — 필드 이름 집합 + 대응 타입.
            report.AppendLine("== matched struct pairs ==");
            foreach (var kv in newStructs.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (!oldStructs.TryGetValue(kv.Key, out Type oldT)) continue;
                r.MatchedStructs++;
                report.AppendLine($"  {kv.Key}: {oldT.FullName} <-> {kv.Value.FullName}");

                if (FieldDiffAllowed.ContainsKey(kv.Key)) continue;

                string[] oldF = FieldNames(oldT), newF = FieldNames(kv.Value);
                if (!oldF.SequenceEqual(newF))
                {
                    r.FieldMismatches.Add(
                        $"{kv.Key}: 이름 집합 상이 — old[{string.Join(",", oldF)}] new[{string.Join(",", newF)}]");
                    continue;
                }
                foreach (string f in oldF)
                {
                    Type ot = oldT.GetField(f).FieldType, nt = kv.Value.GetField(f).FieldType;
                    if (!Correspond(ot, nt))
                        r.FieldMismatches.Add($"{kv.Key}.{f}: 타입 비대응 — {ot.Name} → {nt.Name}");
                }
            }

            // ② 동명 enum — 멤버 이름·값·기반 타입.
            foreach (var kv in newEnums.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (!oldEnums.TryGetValue(kv.Key, out Type oldE)) continue;
                r.MatchedEnums++;
                if (Enum.GetUnderlyingType(oldE) != Enum.GetUnderlyingType(kv.Value))
                {
                    r.EnumMismatches.Add($"{kv.Key}: 기반 타입 상이");
                    continue;
                }
                Dictionary<string, long> om = Enum.GetNames(oldE).ToDictionary(n => n,
                    n => Convert.ToInt64(Enum.Parse(oldE, n)));
                Dictionary<string, long> nm = Enum.GetNames(kv.Value).ToDictionary(n => n,
                    n => Convert.ToInt64(Enum.Parse(kv.Value, n)));
                foreach (string name in om.Keys.Union(nm.Keys).OrderBy(n => n, StringComparer.Ordinal))
                {
                    if (!om.ContainsKey(name)) r.EnumMismatches.Add($"{kv.Key}.{name}: 신에만 있음");
                    else if (!nm.ContainsKey(name)) r.EnumMismatches.Add($"{kv.Key}.{name}: 구에만 있음");
                    else if (om[name] != nm[name]) r.EnumMismatches.Add($"{kv.Key}.{name}: {om[name]} ≠ {nm[name]}");
                }
            }

            // ③ old-only 감사(Battle 만) — 이식 누락 검출.
            report.AppendLine("== old-only ==");
            foreach (var kv in oldStructs.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (newStructs.ContainsKey(kv.Key) || !IsOldAuditSurface(kv.Value)) continue;
                string why = OldOnlyAllowedByPattern(kv.Key) ? "채널 싱글턴(분류 A 증발)"
                    : OldOnlyAllowed.TryGetValue(kv.Key, out string reason) ? reason : null;
                report.AppendLine($"  {kv.Value.FullName}  — {why ?? "??"}");
                if (why == null) r.OldOnlyUnexplained.Add(kv.Value.FullName);
            }

            // ④ new-only — 개명 이식의 소유자 장부.
            report.AppendLine("== new-only ==");
            foreach (var kv in newStructs.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (oldStructs.ContainsKey(kv.Key)) continue;
                string why = NewOnlyAllowed.TryGetValue(kv.Key, out string reason) ? reason : null;
                report.AppendLine($"  {kv.Value.FullName}  — {why ?? "??"}");
                if (why == null) r.NewOnlyUnexplained.Add(kv.Value.FullName);
            }

            report.AppendLine($"== counts: matchedStructs={r.MatchedStructs} matchedEnums={r.MatchedEnums} " +
                              $"oldOnly?={r.OldOnlyUnexplained.Count} newOnly?={r.NewOnlyUnexplained.Count} " +
                              $"fieldX={r.FieldMismatches.Count} enumX={r.EnumMismatches.Count} ==");

            foreach (string header in new[] { "== field mismatches ==", "== enum mismatches ==", "== ambiguities ==" })
            {
                report.AppendLine(header);
                List<string> src = header.Contains("field") ? r.FieldMismatches
                    : header.Contains("enum") ? r.EnumMismatches : r.Ambiguities;
                foreach (string line in src) report.AppendLine("  " + line);
            }

            try
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText(Path.Combine("Temp", "sim-type-parity-report.txt"), report.ToString());
            }
            catch { /* 보고서는 진단 편의 — 실패해도 단정이 진실이다 */ }
            return r;
        }

        private static string Detail(List<string> lines)
            => "\n  " + string.Join("\n  ", lines) + "\n(전체 보고: Temp/sim-type-parity-report.txt)";

        // ── 단정 ─────────────────────────────────────────────────────────────

        [Test]
        public void 동명_타입은_한쪽에_하나씩만_있다()
            => Assert.IsEmpty(Sweep.Value.Ambiguities,
                "동명 타입이 한쪽에 2개 이상 — simple name 매칭이 성립하지 않는다:" + Detail(Sweep.Value.Ambiguities));

        [Test]
        public void 매칭된_struct_쌍의_필드가_전부_대응한다()
            => Assert.IsEmpty(Sweep.Value.FieldMismatches,
                "필드 이름/타입 드리프트:" + Detail(Sweep.Value.FieldMismatches));

        [Test]
        public void 매칭된_enum_이_멤버와_값까지_같다()
            => Assert.IsEmpty(Sweep.Value.EnumMismatches,
                "enum 드리프트 — 트레이스에 정수로 실린다:" + Detail(Sweep.Value.EnumMismatches));

        [Test]
        public void 구_데이터_타입의_이식_누락이_없다()
            => Assert.IsEmpty(Sweep.Value.OldOnlyUnexplained,
                "old-only 인데 장부에 이유가 없다 — 이식 누락이거나 장부 결손:" + Detail(Sweep.Value.OldOnlyUnexplained));

        [Test]
        public void 신_고유_타입은_전부_이유가_있다()
            => Assert.IsEmpty(Sweep.Value.NewOnlyUnexplained,
                "new-only 인데 장부에 이유가 없다 — 개명 이식이면 소유자를 적는다:" + Detail(Sweep.Value.NewOnlyUnexplained));

        [Test]
        public void 스윕이_실제로_유의미한_수를_매칭한다()
        {
            // ⚠ 표면 정의(네임스페이스 문자열)가 낡으면 위 단정들이 빈 목록 위에서 전부
            //   초록이 된다 — 이 spec 이 반복해 경계하는 "조용한 no-op" 모양.
            Assert.GreaterOrEqual(Sweep.Value.MatchedStructs, 60, "매칭 struct 쌍");
            Assert.GreaterOrEqual(Sweep.Value.MatchedEnums, 10, "매칭 enum 쌍");
        }
    }
}
