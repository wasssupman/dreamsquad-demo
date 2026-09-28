#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.EditorTools.BattleCore
{
    // skill-data-table unit 4 — **옛 저작 → 새 저작 형식 이전**(효과 에셋 + 소유 줄). 계획은 `LegacyBindingMigration`(런타임 어셈블리 ·
    // 테스트 `BindingSpecBakeTests` 가 같은 계획을 메모리 사본에 입혀 옛 굽기와 대조한다).
    //
    //   · dry-run — 표 파일만 쓴다(에셋 0). 사용자 확인용.
    //   · 적용 — 효과 에셋을 `Data/Effects/` 에 만들고 소유자 에셋의 **새 칸**(bindings · hostKinds · 액티브 대기 · 분열 고유 값)을 쓴다.
    //     옛 칸(mechanics · nightmareMechanics · 규칙 레일 능력 · skill)은 지우지 않는다(4-정리 — 사용자 승인 뒤).
    //     ⚠ 사용자 승인 전에는 돌리지 않는다. 적용 뒤 굽기 스냅샷(카드 · 유닛/적) 두 테스트가 값 동치의 증거다.
    public static class SkillDataTableMigrationMenu
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string ReportPath = "docs/spec/skill-data-table/dry-run/dry_run_table.md";

        [MenuItem("Wassup/BattleCore/Skill Data Table/이전 dry-run (표만 쓴다)")]
        private static void DryRun()
        {
            var plan = BuildPlan();
            string path = WriteReport(plan);
            Debug.Log($"[SkillDataTableMigration] dry-run — 소유자 {plan.Owners.Count} · 에셋 0 · 표: {path}");
        }

        [MenuItem("Wassup/BattleCore/Skill Data Table/이전 적용 (에셋을 쓴다 — 승인 뒤)")]
        private static void ApplyMenu()
        {
            if (!EditorUtility.DisplayDialog("skill-data-table 이전 적용",
                    "효과 에셋을 만들고 카드 · 방어유닛 · 적 에셋의 새 칸을 씁니다(옛 칸은 그대로).\n사용자가 dry-run 표를 승인했나요?",
                    "적용", "취소")) return;
            var plan = BuildPlan();
            foreach (var o in plan.Owners)
                foreach (var r in o.Rows)
                    if (File.Exists(r.AssetPath) || AssetDatabase.LoadAssetAtPath<EffectData>(r.AssetPath) != null)
                    {
                        Debug.LogError($"[SkillDataTableMigration] 이미 있는 효과 에셋: {r.AssetPath} — 적용을 멈춘다(덮어쓰지 않는다).");
                        return;
                    }
            if (!AssetDatabase.IsValidFolder(LegacyBindingMigration.EffectFolder))
                AssetDatabase.CreateFolder(DataRoot, "Effects");
            int made = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var o in plan.Owners)
                {
                    LegacyBindingMigration.Apply(o, o.Owner, r =>
                    {
                        var fx = LegacyBindingMigration.Materialize(r);
                        AssetDatabase.CreateAsset(fx, r.AssetPath);
                        made++;
                        return fx;
                    });
                    EditorUtility.SetDirty(o.Owner);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            string path = WriteReport(plan);
            Debug.Log($"[SkillDataTableMigration] 적용 — 효과 에셋 {made} · 소유자 {plan.Owners.Count}. 표: {path}. 다음: 굽기 스냅샷 테스트 2(카드 · 유닛/적) + BindingSpecBakeTests.");
        }

        private static LegacyBindingMigration.Plan BuildPlan()
            => LegacyBindingMigration.Build(ByPath<DreamcatcherCard>("t:DreamcatcherCard"),
                                            ByPath<DefenderUnitData>("t:DefenderUnitData"),
                                            ByPath<AttackUnitData>("t:AttackUnitData"));

        private static string WriteReport(LegacyBindingMigration.Plan plan)
        {
            string full = Path.Combine(Path.GetDirectoryName(Application.dataPath), ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, LegacyBindingMigration.Report(plan, AssetDatabase.GetAssetPath), new System.Text.UTF8Encoding(false));
            return ReportPath;
        }

        private static List<T> ByPath<T>(string filter) where T : Object
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { DataRoot })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
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
