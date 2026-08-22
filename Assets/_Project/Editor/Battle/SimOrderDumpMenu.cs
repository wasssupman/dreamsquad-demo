using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using Wassup.Battle;

namespace Wassup.EditorTools.Battle
{
    // battle-sim-extraction M0 unit 0 — 유효 시스템 총순서 캡처.
    //
    // `BattleSimGroup` 의 어트리뷰트 순서 그래프는 불완전하다. 미선언 지점의 실행 순서를
    // **Unity 토폴로지 정렬의 tie-break** 이 결정하고 있고, 그 순서가 곧 시뮬 의미론이다
    // (같은 틱 소비 · IncomingDamage 정산 시점). 그래서 먼저 **러닝 월드의 실제 순서를
    // 사실로 덤프**하고, 그 다음 미선언 지점을 어트리뷰트로 박제한다.
    //
    // ⚠ 이 유틸은 순서를 **고치지 않는다.** 현행 유효 순서를 그대로 기록할 뿐이다 —
    // 재배치 판단은 M1 설계의 몫이고, 여기서 순서를 바꾸면 골든의 기준선이 무너진다.
    //
    // Play 중에만 의미가 있다: 정렬은 그룹이 시스템을 다 받은 뒤에 확정되므로 러닝 월드가
    // 아니면 볼 것이 없다.
    public static class SimOrderDumpMenu
    {
        private const string CapturePath = "docs/spec/battle-sim-extraction/order-capture.md";

        [MenuItem("Wassup/Battle/Sim Order/Dump BattleSimGroup Order")]
        public static void Dump()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SimOrderDump] Play Mode 에서 실행하라 — 정렬은 러닝 월드에서만 확정된다.");
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[SimOrderDump] 기본 월드가 없다. 전투 씬에서 Play 진입 후 다시 실행하라.");
                return;
            }

            var group = world.GetExistingSystemManaged<BattleSimGroup>();
            if (group == null)
            {
                Debug.LogWarning("[SimOrderDump] BattleSimGroup 이 없다.");
                return;
            }

            var rows = CollectOrder(world, group);
            string markdown = BuildMarkdown(rows);
            System.IO.File.WriteAllText(CapturePath, markdown);
            AssetDatabase.Refresh();
            Debug.Log($"[SimOrderDump] {rows.Count} 시스템 순서를 {CapturePath} 에 기록했다. " +
                      $"미선언(무순서) {rows.Count(r => !r.HasAnyOrderAttribute)}개.");
        }

        // 덤프 1행. 이름·순서는 러닝 월드에서, 어트리뷰트는 타입에서 온다.
        private sealed class Row
        {
            public int Index;
            public string Name;
            public Type Type;                 // 이름으로 역해석 — 못 찾으면 null(어트리뷰트 미상)
            public List<string> Before = new List<string>();
            public List<string> After = new List<string>();
            public bool HasAnyOrderAttribute => Before.Count > 0 || After.Count > 0;
        }

        private static List<Row> CollectOrder(World world, ComponentSystemGroup group)
        {
            // `GetAllSystems` 는 master update list 를 그대로 훑으므로 **정렬된 실행 순서**다.
            using var handles = group.GetAllSystems(Allocator.Temp);
            var byName = SystemTypesByName();
            var rows = new List<Row>(handles.Length);

            for (int i = 0; i < handles.Length; i++)
            {
                // DebugName·ResolveSystemStateRef 는 공개 API — 리플렉션 없이 이름을 얻는다.
                ref var state = ref world.Unmanaged.ResolveSystemStateRef(handles[i]);
                string name = state.DebugName.ToString();
                var row = new Row { Index = i, Name = name };
                byName.TryGetValue(StripNamespace(name), out row.Type);
                if (row.Type != null) ReadOrderAttributes(row);
                rows.Add(row);
            }
            return rows;
        }

        // ISystem/ComponentSystemBase 타입을 짧은 이름으로 색인한다. DebugName 이 네임스페이스를
        // 포함하는지 여부가 버전마다 다를 수 있어 짧은 이름 기준으로 맞춘다.
        private static Dictionary<string, Type> SystemTypesByName()
        {
            var map = new Dictionary<string, Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract) continue;
                    bool isSystem = typeof(ISystem).IsAssignableFrom(t) || typeof(ComponentSystemBase).IsAssignableFrom(t);
                    if (!isSystem) continue;
                    map[t.Name] = t;
                }
            }
            return map;
        }

        private static string StripNamespace(string debugName)
        {
            int dot = debugName.LastIndexOf('.');
            return dot >= 0 ? debugName.Substring(dot + 1) : debugName;
        }

        private static void ReadOrderAttributes(Row row)
        {
            foreach (var a in row.Type.GetCustomAttributes(typeof(UpdateBeforeAttribute), false))
                row.Before.Add(((UpdateBeforeAttribute)a).SystemType.Name);
            foreach (var a in row.Type.GetCustomAttributes(typeof(UpdateAfterAttribute), false))
                row.After.Add(((UpdateAfterAttribute)a).SystemType.Name);
        }

        private static string BuildMarkdown(List<Row> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# order-capture — `BattleSimGroup` 유효 시스템 총순서");
            sb.AppendLine();
            sb.AppendLine("> **자동 생성물.** `Wassup/Battle/Sim Order/Dump BattleSimGroup Order` 로 Play 중 갱신한다.");
            sb.AppendLine("> 손으로 고치지 말 것 — 이 파일은 «지금 실제로 이 순서로 돈다» 는 사실의 기록이고,");
            sb.AppendLine("> 골든(unit 4)과 신 sim 틱 파이프라인(M1)이 이 순서를 기준으로 삼는다.");
            sb.AppendLine();
            sb.AppendLine($"- 시스템 수: **{rows.Count}**");
            sb.AppendLine($"- 순서 어트리뷰트가 **하나도 없는** 시스템: **{rows.Count(r => !r.HasAnyOrderAttribute)}**");
            sb.AppendLine();
            sb.AppendLine("「무순서」 = `UpdateBefore`/`UpdateAfter` 를 하나도 선언하지 않은 시스템이다.");
            sb.AppendLine("그 위치는 지금 토폴로지 정렬의 tie-break 이 정하고 있어, 시스템이 하나 추가되면");
            sb.AppendLine("조용히 움직일 수 있다. unit 0 의 핀 대상이 바로 이 목록이다.");
            sb.AppendLine();
            sb.AppendLine("| # | 시스템 | UpdateAfter | UpdateBefore | 무순서 |");
            sb.AppendLine("|---:|---|---|---|:-:|");
            foreach (var r in rows)
            {
                string after = r.After.Count > 0 ? string.Join(", ", r.After) : "—";
                string before = r.Before.Count > 0 ? string.Join(", ", r.Before) : "—";
                string unpinned = r.HasAnyOrderAttribute ? "" : "⚠";
                string name = r.Type == null ? $"{StripNamespace(r.Name)} *(타입 미상)*" : StripNamespace(r.Name);
                sb.AppendLine($"| {r.Index} | `{name}` | {after} | {before} | {unpinned} |");
            }
            sb.AppendLine();
            sb.AppendLine("## 무순서 시스템 (핀 대상)");
            sb.AppendLine();
            var unpinnedRows = rows.Where(r => !r.HasAnyOrderAttribute).ToList();
            if (unpinnedRows.Count == 0) sb.AppendLine("없음.");
            else foreach (var r in unpinnedRows) sb.AppendLine($"- `{StripNamespace(r.Name)}` — 현재 위치 {r.Index}");
            return sb.ToString();
        }
    }
}
