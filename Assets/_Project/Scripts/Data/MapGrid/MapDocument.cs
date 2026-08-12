using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data.MapGrid
{
    [CreateAssetMenu(fileName = "MapDocument", menuName = "Wassup/Map/MapDocument", order = 1)]
    public class MapDocument : ScriptableObject
    {
        [SerializeField] private int width = 20;
        [SerializeField] private int height = 10;
        [SerializeField] private MapTileType[] tiles;
        [SerializeField] private byte[] placeMask;     // 0/1. 1 = 배치 가능. 부재/길이 불일치 = tiles==Place 파생 폴백(placement-mask unit 0). 타일 종류와 직교 — Walk 셀도 1 가능.
        [SerializeField] private Vector2Int goal;      // primary = goals[0]. 레거시 asset 폴백용(goals 비면 이 값).
        [SerializeField] private Vector2Int[] goals;   // multi-goal 목록(1~4). 비면 [goal] 로 폴백.
        // battle-structures unit 0 — goalMaxStability(per-goal 최대 안정도 M) 저작 축을 제거했다.
        // 전 맵 미저작(0)이라 소비처가 한 번도 엔티티를 만들지 않았고, 읽는 코드를 걷어내
        // «저작해도 아무 일이 없는 필드» 가 남는 것을 막는다. 거점 체력 저작은 unit 3 의
        // StructureData 가 맡는다. (기존 asset 의 YAML 키는 orphan 으로 남지만 무해하다.)
        [SerializeField] private Vector2Int[] spawns;
        // waypoint-routing unit 0 — 맵이 소유하는 경로 N개. 적 SO 는 배열 인덱스로 참조한다.
        // null/빈 = 경로 없는 맵(현행 폴백).
        [SerializeField] private WaypointPath[] waypointPaths;
        // battle-structures unit 3 — 거점 저작(마음·본능). 셀 × 편 × StructureData.
        // 진영은 (편 × data.kind)에서 파생한다 — StructurePlacements.DeriveFaction.
        // 비면 거점 없는 맵(현행 9장 전부) = 행동 변화 0.
        [SerializeField] private StructureEntry[] structures;

        // -1 = 수동 입력, 그 외 값 = 절차적 결과 캐시.
        [SerializeField] private int authoringSeed = -1;

        // 절차적 생성기 버전. 수동 입력은 0.
        [SerializeField] private int generatorVersion;

        public int Width => width;
        public int Height => height;
        public IReadOnlyList<MapTileType> Tiles => tiles;
        public IReadOnlyList<byte> PlaceMask => placeMask;   // null/length-0 가능 — 소비 시 tiles==Place 파생 폴백(ToGeneratedMap)
        public Vector2Int Goal => (goals != null && goals.Length > 0) ? goals[0] : goal;   // primary
        public IReadOnlyList<Vector2Int> Goals => goals;   // null/빈 가능 — 소비 시 [Goal] 폴백(ToGeneratedMap)
        public IReadOnlyList<Vector2Int> Spawns => spawns;
        public IReadOnlyList<WaypointPath> WaypointPaths => waypointPaths;
        public IReadOnlyList<StructureEntry> Structures => structures;   // null/빈 가능 = 거점 없는 맵
        public int AuthoringSeed => authoringSeed;
        public int GeneratorVersion => generatorVersion;

        internal void SetFrom(
            int w, int h,
            MapTileType[] t,
            Vector2Int[] goalsArr, Vector2Int[] s,
            int seed, int version,
            byte[] placeMaskArr = null)
        {
            width = w;
            height = h;
            tiles = t;
            placeMask = placeMaskArr;
            goals = goalsArr;
            goal = (goalsArr != null && goalsArr.Length > 0) ? goalsArr[0] : goal;   // primary 동기
            spawns = s;
            authoringSeed = seed;
            generatorVersion = version;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        // battle-structures unit 3 — 거점 저작은 SetFrom 과 **분리한다.** GeneratedMap 은
        // unmanaged 라 StructureData 참조를 왕복시킬 수 없어, 거점을 SetFrom 에 끼우면
        // «전달 안 하면 지워짐 / 유지됨» 이 암묵 규칙이 된다. 저작 주체(페인터)만 이걸 부른다.
        internal void SetStructures(StructureEntry[] structuresArr)
        {
            structures = structuresArr;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        // waypoint-routing unit 0 — SetFrom 은 타일/메타 저장이므로 경로를 암묵적으로 지우지
        // 않는다. unit 5 페인터와 테스트는 경로 저작만 이 명시 경로로 교체한다.
        internal void SetWaypointPaths(WaypointPath[] paths)
        {
            waypointPaths = paths;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (width < 1 || height < 1)
            {
                Debug.LogError($"[MapDocument] width/height 는 ≥1 이어야 한다 ({width}×{height})", this);
                return;
            }

            int n = width * height;
            if (tiles != null && tiles.Length != n)
                Debug.LogError($"[MapDocument] tiles.Length={tiles.Length} 가 width*height={n} 와 불일치", this);
            // placeMask: Unity 는 신규 배열 필드를 기존 asset 에 length-0 으로 로드하므로
            // length-0 = 부재 = 파생 폴백으로 유효. 길이 불일치만 에러.
            if (placeMask != null && placeMask.Length > 0 && placeMask.Length != n)
                Debug.LogError($"[MapDocument] placeMask.Length={placeMask.Length} != {n} — 소비 시 tiles==Place 파생 폴백", this);

            // goals 좌표 범위 — 이 파일만 아는 정보(격자 크기 대 좌표)라 여기 남는다.
            // (Unity 는 신규 배열 필드를 기존 asset 에 length-0 으로 직렬화하므로 length<1 은 에러 아님.)
            if (goals != null)
                foreach (var g in goals)
                    if (g.x < 0 || g.x >= width || g.y < 0 || g.y >= height)
                        Debug.LogError($"[MapDocument] goal {g} 이 격자 밖 ({width}×{height})", this);

            // battle-structures unit 3(투트랙 리뷰 M-a·M-b 정정) — 개수 규칙과 거점 규칙은
            // 런타임 StructureAuthoringRules 가 단일 소유하고 페인터와 여기가 같은 함수를
            // 부른다. 이전엔 spawns<1 을 무조건 에러로 잡아 공성 맵(스폰 0 = 파생이 채움)이
            // 페인터를 통과하고도 import 에서 에러를 뱉는 자기모순이 있었다.
            var authoringErrors = new List<string>();
            int goalCount = (goals != null && goals.Length > 0) ? goals.Length : 1;   // 폴백 [goal]
            StructureAuthoringRules.ValidateMode(
                StructureAuthoringRules.CountEnemyCores(structures),
                goalCount, spawns?.Length ?? 0, authoringErrors);
            StructureAuthoringRules.ValidateStructures(structures, width, height, authoringErrors, tiles);
            foreach (var e in authoringErrors)
                Debug.LogError($"[MapDocument] {e}", this);

            var waypointErrors = new List<string>();
            var waypointWarnings = new List<string>();
            IReadOnlyList<Vector2Int> waypointGoals = goals != null && goals.Length > 0
                ? goals
                : new[] { goal };
            WaypointAuthoringRules.ValidatePaths(
                waypointPaths, width, height, tiles, waypointGoals, spawns,
                waypointErrors, waypointWarnings);
            foreach (var e in waypointErrors)
                Debug.LogError($"[MapDocument] {e}", this);
            foreach (var warning in waypointWarnings)
                Debug.LogWarning($"[MapDocument] {warning}", this);
        }
#endif
    }
}
