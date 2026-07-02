using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Wassup.Editor
{
    // projectile-ga-reskin unit 0.
    // Gabriel Aguiar "Unique Projectiles Vol 4" 프리팹을 ECS-driven 파이프라인이
    // 그대로 쓸 수 있는 view-only 파생 프리팹으로 변환한다.
    //   제거: ProjectileMoveScript(데모 무버) + Rigidbody + 모든 Collider
    //   유지: ParticleSystem / TrailRenderer / Light(+URP LightData) / 렌더러
    //   고정: 모든 ParticleSystem.emitterVelocityMode = Transform
    //         (RB 없이도 velocity 기반 시각이 SyncTransforms 이동으로 as-is 유지)
    public static class GaProjectileStripper
    {
        private const string OutputDir = "Assets/_Project/VFX/Projectiles/GA";

        [MenuItem("Wassup/VFX/Strip GA Projectile (Selection)")]
        public static void StripSelection()
        {
            var selected = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
            if (selected == null || selected.Length == 0)
            {
                Debug.LogWarning("[GaStripper] Project 창에서 GA 투사체 프리팹을 선택하세요.");
                return;
            }

            // 프리팹 후보만 추림 (비프리팹, 이미 산출 폴더 내부 프리팹 제외).
            var candidates = new List<string>();
            foreach (var go in selected)
            {
                var path = AssetDatabase.GetAssetPath(go);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
                {
                    Debug.LogWarning($"[GaStripper] 스킵(프리팹 아님): {path}");
                    continue;
                }
                if (path.StartsWith(OutputDir + "/"))
                {
                    Debug.LogWarning($"[GaStripper] 스킵(이미 산출 폴더 내부): {path}");
                    continue;
                }
                candidates.Add(path);
            }
            if (candidates.Count == 0) return;

            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
                AssetDatabase.Refresh();
            }

            // 산출 경로(파일 stem) 충돌 사전 탐지 → 서로 다른 소스가 침묵 덮어쓰기 방지.
            var claimed = new Dictionary<string, string>();
            int ok = 0;
            foreach (var srcPath in candidates)
            {
                var outPath = $"{OutputDir}/{Path.GetFileNameWithoutExtension(srcPath)}.prefab";
                if (claimed.TryGetValue(outPath, out var prev))
                {
                    Debug.LogError($"[GaStripper] 스킵(출력 이름 충돌): {srcPath} ↔ {prev} → {outPath}");
                    continue;
                }
                claimed[outPath] = srcPath;
                if (StripOne(srcPath, outPath)) ok++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GaStripper] 완료: {ok}/{candidates.Count} 프리팹 → {OutputDir}");
        }

        private static bool StripOne(string srcPath, string outPath)
        {
            var name = Path.GetFileNameWithoutExtension(srcPath);
            var root = PrefabUtility.LoadPrefabContents(srcPath);
            try
            {
                // 1) 데모 무버 스크립트 제거 (타입명 문자열 매칭 — asmdef 참조 불필요)
                int movers = 0;
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType().Name == "ProjectileMoveScript")
                    {
                        Object.DestroyImmediate(mb, true);
                        movers++;
                    }
                }

                // 2) Rigidbody 제거
                int rbs = 0;
                foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    Object.DestroyImmediate(rb, true);
                    rbs++;
                }

                // 3) 모든 Collider 제거
                int cols = 0;
                foreach (var col in root.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(col, true);
                    cols++;
                }

                // 4) 파티클 속도 소스 = Transform + velocity 의존성 감사
                int ps = 0;
                var velDep = new List<string>();
                foreach (var p in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = p.main;
                    main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
                    ps++;

                    bool dep = p.inheritVelocity.enabled
                               || p.emission.rateOverDistanceMultiplier > 0f;
                    var r = p.GetComponent<ParticleSystemRenderer>();
                    if (r != null && r.renderMode == ParticleSystemRenderMode.Stretch)
                        dep = true;
                    if (dep) velDep.Add(p.name);
                }

                // 잔존 vendor MonoBehaviour 감사 (Unity 내장/URP 는 제외) — 데모 스크립트 누락 탐지.
                var leftover = new List<string>();
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var ns = mb.GetType().Namespace ?? "";
                    if (!ns.StartsWith("UnityEngine") && !ns.StartsWith("UnityEditor"))
                        leftover.Add(mb.GetType().Name);
                }

                var sb = new StringBuilder();
                sb.AppendLine($"[GaStripper] {name}:");
                sb.AppendLine($"  제거 → mover {movers} / rigidbody {rbs} / collider {cols}");
                sb.AppendLine($"  ParticleSystem {ps}개 → emitterVelocityMode=Transform");
                sb.AppendLine(velDep.Count > 0
                    ? $"  velocity 의존 PS(스트립으로 Transform 고정됨): {string.Join(", ", velDep)}"
                    : "  velocity 의존 PS: 없음 (RB 제거 영향 0)");
                if (leftover.Count > 0)
                    sb.AppendLine($"  ⚠ 잔존 vendor 스크립트(확인 요): {string.Join(", ", leftover)}");
                Debug.Log(sb.ToString());

                PrefabUtility.SaveAsPrefabAsset(root, outPath, out bool success);
                if (!success)
                    Debug.LogError($"[GaStripper] 저장 실패: {outPath}");
                return success;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
