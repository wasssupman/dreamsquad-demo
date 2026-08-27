using UnityEditor;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.EditorTools
{
    // map-diorama-stage — 사용자 저작 스테이지 프리팹에 마커를 심는 도구(러너 태스크/코드 호출 전용, 메뉴 없음).
    // 구 MapStageDummyGenerator(KayKit 절차 조립 — Pilot·DuelClassic)는 unit 12 에서 은퇴했다. 절차 조립 예시는
    // MapStageDuelGenerator(Street 제작방식: 바닥 Plane + 스프라이트 프랍 + 마커).
    public static class MapStageAuthoringTools
    {
        // unit 9 — 기존 스테이지 프리팹에 보너스 포탈 마커 2개를 저작한다(비주얼 없음 — 포탈 뷰는
        // BattleBridge.BonusWave 가 웨이브 수명으로 띄운다). 이미 있으면 갈아끼운다(멱등).
        public static void AuthorBonusPortals(string prefabPath, Vector2Int a, Vector2Int b)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var old in root.GetComponentsInChildren<BonusSpawnMarker>(true))
                    Object.DestroyImmediate(old.gameObject);
                Portal(root, "bonus_portal_0", a);
                Portal(root, "bonus_portal_1", b);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log($"[MapStageAuthoringTools] 보너스 포탈 저작: {prefabPath} {a} {b}");
        }

        // 사용자 저작 스테이지에 스폰 2 + 골 1 마커를 넣고 dev 슬롯 등록. 기존 스폰/골 마커는 갈아끼운다(멱등).
        // 비주얼은 저작하지 않는다 — 공용 포탈 프랍(MarkerPropStyle)이 런타임/프리뷰에 붙는다(unit 6).
        public static void AuthorSpawnsAndGoal(string prefabPath, Vector2Int spawn0, Vector2Int spawn1, Vector2Int goal)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var old in root.GetComponentsInChildren<SpawnMarker>(true)) Object.DestroyImmediate(old.gameObject);
                foreach (var old in root.GetComponentsInChildren<GoalMarker>(true)) Object.DestroyImmediate(old.gameObject);
                Host(root, "spawn0", spawn0).AddComponent<SpawnMarker>().laneIndex = 0;
                Host(root, "spawn1", spawn1).AddComponent<SpawnMarker>().laneIndex = 1;
                Host(root, "goal", goal).AddComponent<GoalMarker>();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var stage = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)?.GetComponent<MapStage>();
            var pool = AssetDatabase.LoadAssetAtPath<MapStagePool>("Assets/_Project/Data/Maps/MapStagePool.asset");
            if (stage != null && pool != null && pool.EditorRegisterDevStage(stage))
            {
                EditorUtility.SetDirty(pool);
                AssetDatabase.SaveAssetIfDirty(pool);   // SaveAssets 는 무관한 dirty 에셋까지 디스크에 쓴다(클린 워크트리 빌드 게이트 오탐)
            }
            Debug.Log($"[MapStageAuthoringTools] 스폰/골 저작: {prefabPath} S0{spawn0} S1{spawn1} G{goal}");
        }

        // ── unit 6 — 포탈 프랍 = 스폰/골 마커 비주얼(맵에 상관없이 공유) ─────────────────────────
        public const string SpawnPortalPrefab = "Assets/_Project/Prefabs/Structures/SpawnPortal_Red.prefab";
        public const string GoalPortalPrefab = "Assets/_Project/Prefabs/Structures/GoalPortal_Yellow.prefab";
        public const string MarkerPropStylePath = "Assets/_Project/Data/Maps/MarkerPropStyle.asset";

        // 공용 프랍 스타일 에셋(설치자·프리뷰의 단일 정본)을 **보장**한다 — 없으면 만들고, **빈 슬롯만** 기본 포탈로 채운다.
        // 아티스트가 슬롯을 다른 프리팹으로 바꿔 둔 것은 되돌리지 않는다(가이드가 «스타일 에셋의 슬롯을 바꾼다»를 안내한다).
        public static string EnsureMarkerPropStyle()
        {
            var spawn = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnPortalPrefab);
            var goal = AssetDatabase.LoadAssetAtPath<GameObject>(GoalPortalPrefab);
            if (spawn == null || goal == null) return "ERROR|포탈 프리팹 없음(SpawnPortal_Red / GoalPortal_Yellow)";
            var style = AssetDatabase.LoadAssetAtPath<MarkerPropStyle>(MarkerPropStylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<MarkerPropStyle>();
                AssetDatabase.CreateAsset(style, MarkerPropStylePath);
            }
            var filled = new System.Collections.Generic.List<string>();
            if (style.spawnProp == null) { style.spawnProp = spawn; filled.Add("spawnProp"); }
            if (style.goalProp == null) { style.goalProp = goal; filled.Add("goalProp"); }
            if (filled.Count > 0)
            {
                EditorUtility.SetDirty(style);
                AssetDatabase.SaveAssetIfDirty(style);
            }
            return $"OK|{MarkerPropStylePath} spawn={style.spawnProp.name} goal={style.goalProp.name} " +
                   $"{(filled.Count > 0 ? "filled=" + string.Join(",", filled) : "unchanged")} guid={AssetDatabase.AssetPathToGUID(MarkerPropStylePath)}";
        }

        // 프리뷰(EditMode 라 설치자의 OnEnable 이 돌지 않음) — 런타임과 **같은** MarkerPropInstaller.Apply 로 공용 프랍을 얹는다.
        // 반환: 얹은 수. -1 = 스타일 에셋/MapStage 부재(«프랍 없는 프리뷰»를 정상으로 읽지 않게 호출자가 상태에 싣는다).
        public static int ApplySharedMarkerProps(GameObject stageInstance)
        {
            var style = AssetDatabase.LoadAssetAtPath<MarkerPropStyle>(MarkerPropStylePath);
            var stage = stageInstance.GetComponent<MapStage>();
            if (style == null || stage == null) return -1;
            return Wassup.Presentation.MarkerPropInstaller.Apply(stage, style);
        }

        // SpawnPortal_Red 의 색상 변형 — 파티클 startColor(min 흰색 / max 빨강 계열) 의 색조만 노랑으로 돌린다(채도·명도 유지).
        // 머티리얼(Portal_Circle/Point/Smoke)은 공유 — 색은 startColor 에만 있어야 GoalMarker 의 스트레스 틴트(머티리얼 _Color 에 곱)와 겹치지 않는다.
        // 방향은 스폰 포탈과 동일(루트 identity — 수직으로 선 포탈, 사용자 결정 2026-08-27). 멱등: 있으면 덮어쓴다.
        [MenuItem("Window/Wassup/Map Stage/Create Goal Portal (Yellow)")]
        public static void CreateGoalPortalYellowMenu() => Debug.Log(CreateGoalPortalYellow());

        public static string CreateGoalPortalYellow()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnPortalPrefab);
            if (src == null) return "ERROR|SpawnPortal_Red 없음";
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            try
            {
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                inst.name = "GoalPortal_Yellow";
                const float yellowHue = 50f / 360f;
                foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    var sc = main.startColor;
                    sc.mode = ParticleSystemGradientMode.TwoColors;
                    sc.colorMin = ShiftHue(sc.colorMin, yellowHue);
                    sc.colorMax = ShiftHue(sc.colorMax, yellowHue);
                    main.startColor = sc;
                }
                PrefabUtility.SaveAsPrefabAsset(inst, GoalPortalPrefab);
                return $"OK|{GoalPortalPrefab} ← SpawnPortal_Red 색조 {50}° (startColor 만)";
            }
            finally { Object.DestroyImmediate(inst); }
        }

        // 무채색(흰색)은 그대로, 유채색은 색조만 교체 — «흰 → 빨강» 그라데이션이 «흰 → 노랑» 이 된다.
        static Color ShiftHue(Color c, float hue)
        {
            Color.RGBToHSV(c, out _, out float s, out float v);
            if (s < 0.05f) return c;
            var o = Color.HSVToRGB(hue, s, v);
            o.a = c.a;
            return o;
        }

        // 호스트 = 셀 중심, gridOriginLocal 을 더한다 — 원점이 0 이 아닌 사용자 저작 스테이지(Subway z 1.41,
        // StreetDay z −0.76)에서도 의도한 셀에 떨어진다. 원점을 빼먹으면 스캐너 양자화에서 한 칸 밀린다.
        static GameObject Host(GameObject root, string name, Vector2Int cell)
        {
            var stage = root.GetComponent<MapStage>();
            var host = new GameObject(name);
            host.transform.SetParent(root.transform, false);
            host.transform.localPosition = stage.gridOriginLocal + new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f);
            return host;
        }

        static void Portal(GameObject root, string name, Vector2Int cell)
            => Host(root, name, cell).AddComponent<BonusSpawnMarker>();
    }
}
