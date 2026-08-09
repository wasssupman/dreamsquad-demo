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
        [SerializeField] private byte[] mergeDegree;
        [SerializeField] private bool[] chokepoint;
        [SerializeField] private byte[] propLayerId;
        [SerializeField] private byte[] placeMask;     // 0/1. 1 = 배치 가능. 부재/길이 불일치 = tiles==Place 파생 폴백(placement-mask unit 0). 타일 종류와 직교 — Walk 셀도 1 가능.
        [SerializeField] private Vector2Int goal;      // primary = goals[0]. 레거시 asset 폴백용(goals 비면 이 값).
        [SerializeField] private Vector2Int[] goals;   // multi-goal 목록(1~4). 비면 [goal] 로 폴백.
        // battle-structures unit 0 — goalMaxStability(per-goal 최대 안정도 M) 저작 축을 제거했다.
        // 전 맵 미저작(0)이라 소비처가 한 번도 엔티티를 만들지 않았고, 읽는 코드를 걷어내
        // «저작해도 아무 일이 없는 필드» 가 남는 것을 막는다. 거점 체력 저작은 unit 3 의
        // StructureData 가 맡는다. (기존 asset 의 YAML 키는 orphan 으로 남지만 무해하다.)
        [SerializeField] private Vector2Int[] spawns;

        // -1 = 수동 입력, 그 외 값 = 절차적 결과 캐시.
        [SerializeField] private int authoringSeed = -1;

        // 절차적 생성기 버전. 수동 입력은 0.
        [SerializeField] private int generatorVersion;

        public int Width => width;
        public int Height => height;
        public IReadOnlyList<MapTileType> Tiles => tiles;
        public IReadOnlyList<byte> MergeDegree => mergeDegree;
        public IReadOnlyList<bool> Chokepoint => chokepoint;
        public IReadOnlyList<byte> PropLayerId => propLayerId;
        public IReadOnlyList<byte> PlaceMask => placeMask;   // null/length-0 가능 — 소비 시 tiles==Place 파생 폴백(ToGeneratedMap)
        public Vector2Int Goal => (goals != null && goals.Length > 0) ? goals[0] : goal;   // primary
        public IReadOnlyList<Vector2Int> Goals => goals;   // null/빈 가능 — 소비 시 [Goal] 폴백(ToGeneratedMap)
        public IReadOnlyList<Vector2Int> Spawns => spawns;
        public int AuthoringSeed => authoringSeed;
        public int GeneratorVersion => generatorVersion;

        internal void SetFrom(
            int w, int h,
            MapTileType[] t, byte[] md, bool[] cp, byte[] pl,
            Vector2Int[] goalsArr, Vector2Int[] s,
            int seed, int version,
            byte[] placeMaskArr = null)
        {
            width = w;
            height = h;
            tiles = t;
            mergeDegree = md;
            chokepoint = cp;
            propLayerId = pl;
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
            if (mergeDegree != null && mergeDegree.Length != n)
                Debug.LogError($"[MapDocument] mergeDegree.Length={mergeDegree.Length} != {n}", this);
            if (chokepoint != null && chokepoint.Length != n)
                Debug.LogError($"[MapDocument] chokepoint.Length={chokepoint.Length} != {n}", this);
            if (propLayerId != null && propLayerId.Length != n)
                Debug.LogError($"[MapDocument] propLayerId.Length={propLayerId.Length} != {n}", this);
            // placeMask: Unity 는 신규 배열 필드를 기존 asset 에 length-0 으로 로드하므로
            // length-0 = 부재 = 파생 폴백으로 유효. 길이 불일치만 에러.
            if (placeMask != null && placeMask.Length > 0 && placeMask.Length != n)
                Debug.LogError($"[MapDocument] placeMask.Length={placeMask.Length} != {n} — 소비 시 tiles==Place 파생 폴백", this);

            if (spawns == null || spawns.Length < 1 || spawns.Length > 4)
                Debug.LogError($"[MapDocument] spawns.Length 는 1~4 (현재 {spawns?.Length ?? 0})", this);

            // goals 빈 배열/null = primary [goal] 폴백(레거시 asset·미authored) → 유효. 상한·범위만 검증.
            // (Unity 는 신규 배열 필드를 기존 asset 에 length-0 으로 직렬화하므로 length<1 은 에러 아님.)
            if (goals != null)
            {
                if (goals.Length > 4)
                    Debug.LogError($"[MapDocument] goals.Length 는 최대 4 (현재 {goals.Length})", this);
                foreach (var g in goals)
                    if (g.x < 0 || g.x >= width || g.y < 0 || g.y >= height)
                        Debug.LogError($"[MapDocument] goal {g} 이 격자 밖 ({width}×{height})", this);
            }
        }
#endif
    }
}
