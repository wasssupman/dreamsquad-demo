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
        // map-view-deadcode-removal unit 3 — 단수 `goal` 레거시 폴백 축을 제거했다. 9장 전부
        // goals 를 채우고 안전망(BuildFallbackLinear)도 goals 를 명시 세팅해 폴백 분기가 전부
        // 도달 불가였다. 폴백을 지우면서 검증을 안 넣으면 «빈 goals 문서» 가 조용한 인덱스
        // 예외로 바뀌므로, OnValidate 가 loud 하게 잡는다(다른 저작 축과 같은 형태).
        [SerializeField] private Vector2Int[] goals;   // multi-goal 목록(1~4). 비면 저작 에러.
        // battle-structures unit 0 — goalMaxStability(per-goal 최대 안정도 M) 저작 축을 제거했다.
        // 전 맵 미저작(0)이라 소비처가 한 번도 엔티티를 만들지 않았고, 읽는 코드를 걷어내
        // «저작해도 아무 일이 없는 필드» 가 남는 것을 막는다. 거점 체력 저작은 unit 3 의
        // StructureData 가 맡는다. (기존 asset 의 YAML 키는 orphan 으로 남지만 무해하다.)
        [SerializeField] private Vector2Int[] spawns;
        // waypoint-routing unit 0 — 맵이 소유하는 경로 N개. 적 SO 는 배열 인덱스로 참조한다.
        // null/빈 = 경로 없는 맵(현행 폴백).
        [SerializeField] private WaypointPath[] waypointPaths;
        // waypoint-routing unit 8 — 스폰(레인)별 **기본** 경로 인덱스. `spawns` 와 같은 순서이고
        // -1 = 최단거리(현행). 적 SO 의 `waypointPathIndex` 지정이 있으면 그쪽이 이긴다(계약 10).
        //
        // 병렬 배열인 이유: `spawns` 의 **순서가 곧 레인 번호**이고 그 순서를 페인터·
        // `WavePatternGenerator.EffectiveSpawnIndex`·`PendingSpawnEntry.laneIndex` 가 함께 읽는다.
        // 스폰을 구조체로 승격하면 그 셋이 같이 바뀌는데 필요한 값은 int 하나다.
        // 길이 불일치·부재는 에러가 아니라 폴백(-1) — 이 필드가 없는 기존 문서가 그대로 서야 한다.
        [SerializeField] private int[] spawnRoutes;
        // battle-structures unit 3 — 거점 저작(마음·본능). 셀 × 편 × StructureData.
        // 진영은 (편 × data.kind)에서 파생한다 — StructurePlacements.DeriveFaction.
        // 비면 거점 없는 맵(현행 9장 전부) = 행동 변화 0.
        [SerializeField] private StructureEntry[] structures;
        // bonus-wave-pull unit 1 — 보너스 당기기의 포탈이 열릴 칸. **레인 스폰과 다른 축이다.**
        // `spawns` 에 얹으면 `QueueWave` 가 `laneCount = spawns.Length` 를 라운드로빈 분모로
        // 쓰므로 **모든 일반 웨이브의 레인 분포가 바뀐다**. `structures` 도 안 된다 —
        // 거점 개수가 맵 모드(공성/일반) 판정에 들어간다. 그래서 자기 배열을 갖는다.
        // null/빈 = 보너스 당기기가 없는 맵(버튼이 영영 안 뜬다). 검증은
        // BonusSpawnAuthoringRules 가 단일 소유하고 페인터와 OnValidate 가 같이 부른다.
        [SerializeField] private Vector2Int[] bonusSpawns;

        // -1 = 수동 입력, 그 외 값 = 절차적 결과 캐시.
        [SerializeField] private int authoringSeed = -1;

        // 절차적 생성기 버전. 수동 입력은 0.
        [SerializeField] private int generatorVersion;

        public int Width => width;
        public int Height => height;
        public IReadOnlyList<MapTileType> Tiles => tiles;
        public IReadOnlyList<byte> PlaceMask => placeMask;   // null/length-0 가능 — 소비 시 tiles==Place 파생 폴백(ToGeneratedMap)
        public Vector2Int Goal => goals[0];                // primary. 빈 goals 는 저작 에러(OnValidate)
        public IReadOnlyList<Vector2Int> Goals => goals;
        public IReadOnlyList<Vector2Int> Spawns => spawns;
        public IReadOnlyList<WaypointPath> WaypointPaths => waypointPaths;
        public IReadOnlyList<int> SpawnRoutes => spawnRoutes;   // null/빈 가능 = 전 레인 최단거리
        public IReadOnlyList<StructureEntry> Structures => structures;   // null/빈 가능 = 거점 없는 맵
        public IReadOnlyList<Vector2Int> BonusSpawns => bonusSpawns;   // null/빈 가능 = 보너스 당기기 없는 맵
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

        // bonus-wave-pull unit 1 — SetStructures 와 같은 이유로 SetFrom 과 분리한다.
        // SetFrom 에 끼우면 «전달 안 하면 지워짐 / 유지됨» 이 암묵 규칙이 된다.
        internal void SetBonusSpawns(Vector2Int[] cells)
        {
            bonusSpawns = cells;
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

        // waypoint-routing unit 8 — SetFrom/SetWaypointPaths 와 같은 이유로 별도 경로다.
        // spawnRoutes 는 spawns 와 병렬이라 SetFrom 에 끼우면 «전달 안 하면 지워짐» 이
        // 암묵 규칙이 된다. 저작 주체(페인터)만 이걸 부른다.
        internal void SetSpawnRoutes(int[] routes)
        {
            spawnRoutes = routes;
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

            // unit 3 — 단수 goal 폴백을 걷어냈으므로 «goals 최소 1개» 가 이제 계약이다.
            // 이걸 loud 로 잡지 않으면 Goal 접근이 조용한 IndexOutOfRange 로 바뀐다.
            if (goals == null || goals.Length < 1)
                Debug.LogError("[MapDocument] goals 는 1개 이상이어야 한다 (단수 goal 폴백 제거됨)", this);

            // goals 좌표 범위 — 이 파일만 아는 정보(격자 크기 대 좌표)라 여기 남는다.
            if (goals != null)
                foreach (var g in goals)
                    if (g.x < 0 || g.x >= width || g.y < 0 || g.y >= height)
                        Debug.LogError($"[MapDocument] goal {g} 이 격자 밖 ({width}×{height})", this);

            // battle-structures unit 3(투트랙 리뷰 M-a·M-b 정정) — 개수 규칙과 거점 규칙은
            // 런타임 StructureAuthoringRules 가 단일 소유하고 페인터와 여기가 같은 함수를
            // 부른다. 이전엔 spawns<1 을 무조건 에러로 잡아 공성 맵(스폰 0 = 파생이 채움)이
            // 페인터를 통과하고도 import 에서 에러를 뱉는 자기모순이 있었다.
            var authoringErrors = new List<string>();
            int goalCount = goals?.Length ?? 0;
            StructureAuthoringRules.ValidateMode(
                StructureAuthoringRules.CountEnemyCores(structures),
                goalCount, spawns?.Length ?? 0, authoringErrors);
            StructureAuthoringRules.ValidateStructures(structures, width, height, authoringErrors, tiles);
            foreach (var e in authoringErrors)
                Debug.LogError($"[MapDocument] {e}", this);

            // bonus-wave-pull unit 1 — 포탈 칸 검증. 페인터와 **같은 순수 함수**를 부른다
            // (규칙을 복제하면 «툴 통과 → 런타임 폴백» 이 생긴다 — waypoint-routing unit 5 선례).
            var bonusErrors = new List<string>();
            BonusSpawnAuthoringRules.Validate(
                bonusSpawns, width, height, tiles, goals, bonusErrors);
            foreach (var e in bonusErrors)
                Debug.LogError($"[MapDocument] {e}", this);

            var waypointErrors = new List<string>();
            var waypointWarnings = new List<string>();
            // goals 가 비면 위에서 이미 에러를 냈다 — 여기선 빈 목록으로 넘겨 경로 검증만 계속한다
            // (단수 goal 폴백 제거, unit 3).
            IReadOnlyList<Vector2Int> waypointGoals = goals ?? System.Array.Empty<Vector2Int>();
            // siege-lane-spawn unit 1 — 공성이면 레인/스폰의 정본은 저작 spawns(0개)가 아니라
            // 파생 스폰이다. 저작 spawns 를 넘기면 ⑴ ValidateSpawnRoutes 의 «어느 레인에도 안
            // 붙는다» 거짓 경고 + 인덱스 범위 검증 전체 스킵, ⑵ ValidatePaths 의 «경로 지점이
            // 스폰 셀과 겹친다» 경고가 파생 스폰을 못 본다(리뷰 F5 — 스폰 위 경유지는 체비셰프
            // 도달 판정 때문에 스폰 프레임에 소비돼 레인이 조용히 죽는다). 파생 규칙은
            // CollectDerivedSiegeSpawns 단일 소스(빌더와 같은 오프셋·순서).
            IReadOnlyList<Vector2Int> laneSpawns = spawns;
            if (StructureAuthoringRules.CountEnemyCores(structures) > 0)
            {
                var derived = new List<Vector2Int>();
                StructureAuthoringRules.CollectDerivedSiegeSpawns(structures, derived);
                laneSpawns = derived;
            }
            WaypointAuthoringRules.ValidatePaths(
                waypointPaths, width, height, tiles, waypointGoals, laneSpawns,
                waypointErrors, waypointWarnings);
            WaypointAuthoringRules.ValidateSpawnRoutes(
                spawnRoutes, waypointPaths, laneSpawns, waypointErrors, waypointWarnings);
            foreach (var e in waypointErrors)
                Debug.LogError($"[MapDocument] {e}", this);
            foreach (var warning in waypointWarnings)
                Debug.LogWarning($"[MapDocument] {warning}", this);
        }
#endif
    }
}
