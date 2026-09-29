#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

namespace Wassup.EditorTools.BattleCore
{
    // skill-data-table unit 8 — **상시 효과 이전**(카드 `effects` · `attackMods` → 효과 에셋 + 카드 소유 줄 · 배치 오라 효과 `allyFilter`).
    // 계획은 `AlwaysOnEffectMigration`(순수 — 헤드리스 하네스 · 테스트가 같은 함수) · 입히기는 `AlwaysOnEffectMigrationApply`(테스트는 메모리 사본에).
    //
    //   · dry-run — 표 파일만 쓴다(에셋 0). 사용자 확인용.
    //   · 적용 — 효과 에셋 18 을 `Data/Effects/` 에 만들고 카드 에셋의 **새 칸**(bindings 뒤에 줄 · 배치 오라 효과 allyFilter)을 쓴다.
    //     옛 칸(effects · attackMods)은 지우지 않는다(단계 B — 사용자 승인 뒤 · 과도기 굽기는 새 줄이 있으면 옛 칸을 안 읽는다).
    //     ⚠ 사용자 승인 전에는 돌리지 않는다. 적용 뒤 굽기 스냅샷(카드) · 전 카드 문안 테스트가 값 동치의 증거다.
    // 이 파일은 이전 과도기 전용이다 — 옛 칸이 걷히면(단계 B) 같이 지운다(unit 4 선례 `f64e18aa3`).
    public static class AlwaysOnEffectMigrationMenu
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string CardsRoot = "Assets/_Project/Data/Dreamcatcher";
        private const string ReportPath = "docs/spec/skill-data-table/dry-run/dry_run_part2.md";

        [MenuItem("Wassup/BattleCore/Skill Data Table/2부 이전 dry-run — 상시 효과 (표만 쓴다)")]
        private static void DryRun()
        {
            var plan = BuildPlan(out _);
            string path = WriteReport(plan);
            Debug.Log($"[AlwaysOnEffectMigration] dry-run — 효과 줄 {plan.Rows.Count} · 오라 수혜 대상 {plan.AuraFilters.Count} · 에셋 0 · 표: {path}");
        }

        [MenuItem("Wassup/BattleCore/Skill Data Table/2부 이전 적용 — 상시 효과 (에셋을 쓴다 — 승인 뒤)")]
        private static void ApplyMenu()
        {
            if (!EditorUtility.DisplayDialog("skill-data-table unit 8 이전 적용",
                    "효과 에셋을 만들고 카드 에셋의 새 칸(소유 줄 · 배치 오라 효과 allyFilter)을 씁니다(옛 칸은 그대로).\n사용자가 dry-run 표를 승인했나요?",
                    "적용", "취소")) return;
            var plan = BuildPlan(out var cards);
            foreach (var r in plan.Rows)
                if (File.Exists(r.AssetPath) || AssetDatabase.LoadAssetAtPath<EffectData>(r.AssetPath) != null)
                {
                    Debug.LogError($"[AlwaysOnEffectMigration] 이미 있는 효과 에셋: {r.AssetPath} — 적용을 멈춘다(덮어쓰지 않는다).");
                    return;
                }
            var effectsById = new Dictionary<string, EffectData>(System.StringComparer.Ordinal);
            foreach (var e in ByPath<EffectData>("t:EffectData", DataRoot)) if (e != null && !string.IsNullOrEmpty(e.id)) effectsById[e.id] = e;
            foreach (var a in plan.AuraFilters)
                if (!effectsById.ContainsKey(a.EffectId))
                {
                    Debug.LogError($"[AlwaysOnEffectMigration] 배치 오라 효과 '{a.EffectId}' 를 못 찾았다 — 적용을 멈춘다.");
                    return;
                }
            int made = 0, owners = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var card in cards)
                {
                    bool mine = false;
                    foreach (var r in plan.Rows) if (r.CardId == card.id) { mine = true; break; }
                    if (!mine) continue;
                    bool ok = AlwaysOnEffectMigrationApply.AppendRows(card, plan.Rows, r =>
                    {
                        var fx = AlwaysOnEffectMigrationApply.Materialize(r);
                        AssetDatabase.CreateAsset(fx, r.AssetPath);
                        made++;
                        return fx;
                    });
                    if (ok) { EditorUtility.SetDirty(card); owners++; }
                }
                foreach (var a in plan.AuraFilters)
                {
                    var e = effectsById[a.EffectId];
                    e.values.allyFilter = a.To;
                    EditorUtility.SetDirty(e);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            string path = WriteReport(plan);
            Debug.Log($"[AlwaysOnEffectMigration] 적용 — 효과 에셋 {made} · 카드 {owners} · 오라 수혜 대상 {plan.AuraFilters.Count}. 표: {path}. "
                      + "다음: 카드 굽기 스냅샷 · 전 카드 문안 테스트(값 동치) → 단계 B(옛 칸 제거 · 재직렬화).");
        }

        private static AlwaysOnEffectMigration.Plan BuildPlan(out List<DreamcatcherCard> cards)
        {
            cards = ByPath<DreamcatcherCard>("t:DreamcatcherCard", CardsRoot);
            var inputs = new List<AlwaysOnEffectMigration.CardInput>();
            foreach (var c in cards) inputs.Add(AlwaysOnEffectMigrationApply.InputOf(c, AssetDatabase.GetAssetPath(c)));
            var ids = new List<string>();
            foreach (var e in ByPath<EffectData>("t:EffectData", DataRoot)) if (e != null && !string.IsNullOrEmpty(e.id)) ids.Add(e.id);
            return AlwaysOnEffectMigration.Build(inputs, ids);
        }

        private static string WriteReport(AlwaysOnEffectMigration.Plan plan)
        {
            string full = Path.Combine(Path.GetDirectoryName(Application.dataPath), ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, AlwaysOnEffectMigration.Report(plan, "생성: Unity 메뉴 `Wassup/BattleCore/Skill Data Table/2부 이전 dry-run — 상시 효과`(에셋 경로 순)"),
                              new System.Text.UTF8Encoding(false));
            return ReportPath;
        }

        private static List<T> ByPath<T>(string filter, string root) where T : Object
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { root })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);
            var list = new List<T>();
            foreach (var p in paths)
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(p);
                if (a != null && !list.Contains(a)) list.Add(a);
            }
            return list;
        }
    }
}
#endif
