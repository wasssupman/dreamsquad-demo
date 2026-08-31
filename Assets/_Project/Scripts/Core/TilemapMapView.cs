using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Core
{
    // tilemap-view-backend unit 1 — GeneratedMap 을 Unity Tilemap 에 칠하는 write-only 뷰.
    // source of truth 는 MapDocument/GeneratedMap. 이 클래스는 절대 읽히지 않는다 (GetTile 호출 0건).
    // 셀↔월드 정합의 권위는 Grid (BoardSpace 와 동일). 정합은 TilemapMapViewTests 가 못 박는다.
    public class TilemapMapView : MonoBehaviour
    {
        [SerializeField] private Grid grid;
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Tilemap overlayTilemap;
        // tilemap-real-shadows unit 0 — 그림자 receive 머티리얼(Wassup/Tile_ShadowReceive). 비면 기존 머티리얼 유지.
        [SerializeField] private Material groundShadowMaterial;

        [Header("배치 확정 팝 (placement-cell-snap unit 4)")]
        [Tooltip("포커스 타일이 확정(변경)될 때 셀 위에 뜨는 스케일-페이드 팝의 지속(초).")]
        [SerializeField] private float commitPopDuration = 0.16f;
        [Tooltip("팝 시작 스케일(타일 기준). >1 = 타일보다 크게 시작해 안착.")]
        [SerializeField] private float commitPopStartScale = 1.3f;
        [Tooltip("팝 끝 스케일(타일 기준).")]
        [SerializeField] private float commitPopEndScale = 1.0f;
        [Tooltip("팝 시작 알파(0 으로 페이드).")]
        [SerializeField, Range(0f, 1f)] private float commitPopStartAlpha = 0.85f;
        [SerializeField] private Color commitPopValidColor = new Color(0.35f, 1f, 0.9f, 1f);
        [SerializeField] private Color commitPopInvalidColor = new Color(1f, 0.4f, 0.32f, 1f);

        // active-ally-zone unit 2 — 아군 장판 색(민트). 조준 시안/불가 적색과 구분되는 색.
        // 타일맵당 색 1개라 채널 균일하다. 이 파일의 색 규약(SerializeField 또는 tileSet SO)을 따른다.
        [SerializeField] private Color allyZoneColor = new Color(0.42f, 0.95f, 0.72f, 0.42f);
        // ultimate-leap unit 4 — 착지 예고. **dimmed 주황 채움**(사용자 결정 2026-08-02).
        // 빨강 outline 에서 바꾼 이유: 빨강은 이미 `rangeInvalidColor`(배치 불가)가 쓰고 있어
        // 의미가 겹치고, outline 은 면적이 없어 "여기서 나가라" 가 주변시로 안 읽힌다.
        // 주황은 `aimRangeColor`(조준)와 계열이 같지만 그건 배치 조작 중에만 뜨고 이건 전투
        // 중에만 떠서 화면에 공존하지 않는다. 알파는 낮게 — 밑의 유닛·바닥이 비쳐야 어느 칸인지 읽힌다.
        [SerializeField] private Color landingTelegraphColor = new Color(1f, 0.45f, 0.08f, 0.42f);

        [Header("끈적 액체 타일 (placement-cell-snap unit 7 rev)")]
        // 포커스 셀 하이라이트 자체가 액체 — 테두리(셀 고정) + 내부 번짐(손가락 방향).
        // 모양 튜닝(테두리 폭/도달/목 등)은 PlacementLiquidTile.mat 인스펙터. 여기는 팔레트 + 관성만.
        [SerializeField] private Color liquidValidBorder = new Color(0.45f, 1f, 0.55f, 0.95f);
        [SerializeField] private Color liquidValidFill = new Color(0.35f, 0.95f, 0.45f, 0.45f);
        [SerializeField] private Color liquidInvalidBorder = new Color(1f, 0.45f, 0.35f, 0.95f);
        [SerializeField] private Color liquidInvalidFill = new Color(1f, 0.35f, 0.28f, 0.4f);
        [Tooltip("점액 관성 스프링 강성 — 당김 신호를 늦게 따라오게. ↑=빠릿, ↓=더 걸쭉하게 늘어짐.")]
        [SerializeField] private float liquidSpring = 90f;
        [Tooltip("감쇠 — ↓=멈출 때 출렁임(오버슈트) 큼, ↑=바로 멎음. 셀 전환 직후 되돌아오는 출렁도 이 값.")]
        [SerializeField] private float liquidDamping = 9f;

        private TileSetData _tileSet;
        private Tilemap _rangeTilemap;
        // placement-enemy-see-through unit 6 — 하이라이트 상승 상태(sticky). range 타일맵 lazy 생성 시 반영.
        private bool _highlightAbove;
        private readonly HashSet<Vector2Int> _rangeCells = new();
        // 범위 타일 세기 배율(펄스 알파에 곱). 1 = 기존 사각 범위. unit 9.
        private float _rangeAlphaMul = 1f;
        // placement-thumb-occlusion unit 3 — 배치 판정 유효성 + 전이 스탬프(SetPlacementRangeValidity 단독 소유).
        private bool _rangeInvalid;
        private float _rangeInvalidSince;
        // unit 4 — 지금 깔려 있는 범위 표시가 조준 페이즈 스타일인가. 페인트 시점에 정해지고
        // Update() 의 틴트가 같은 플래그로 색을 고른다(타일과 색이 갈라지지 않게).
        private bool _rangeAimStyle;
        // placement-eligible-tile-highlight unit 1 — 배치 가능 셀 밝은 하이라이트(정적, 펄스 없음).
        // range 와 분리된 전용 타일맵. 드래그 중엔 range 처럼 유닛 위로 상승(9998).
        private Tilemap _placeableTilemap;
        private bool _placeableActive;
        private float _placeableShowTime; // unscaledTime 캡처(페이드인 기준)
        // first-run-tutorial unit 1 — 배치 **불가** 칸 하이라이트(맵 설명 전용). placeable 과
        // 정의상 서로 겹치지 않는 집합이라 같은 sorting 층을 쓰되 z 만 살짝 갈라 z-fight 를 피한다.
        private Tilemap _blockedTilemap;
        private bool _blockedActive;
        private float _blockedShowTime;
        // defender-directional-volley unit 9 — 방향 지정 화살표(재사용 풀, 최대 4).
        private readonly List<SpriteRenderer> _aimArrows = new();
        private static Sprite _arrowSprite;
        private int2 _gridSize;
        private readonly Dictionary<Vector2Int, Coroutine> _activeFlashes = new();
        private readonly HashSet<Vector2Int> _hoverCells = new();
        // placement-cell-snap unit 4 — 확정 팝: 재사용 SpriteRenderer(grid 자식, 코플레이너) + 절차적 흰 스프라이트.
        private SpriteRenderer _commitPop;
        private Coroutine _commitPopCo;
        private static Sprite _popSprite;
        // placement-cell-snap unit 7 rev — 끈적 액체 하이라이트: 상주형 SpriteRenderer(확정 팝과 동일 PopSprite 쿼드 시드).
        // 렌더 = PopSprite 쿼드 + PlacementLiquidTile 셰이더 인스턴스 머티리얼(원본 .mat 비오염).
        private SpriteRenderer _liquidTile;
        private Material _liquidTileMat;
        private bool _liquidTileMatMissing; // 미배선 경고 1회 게이트
        // distance-based-range unit 5 — 사거리 **링**(윤곽). 액체 하이라이트와 같은 관용구:
        // grid 자식 절차적 쿼드 + 인스턴스 머티리얼(에셋 원본 비오염). 모양은 전부 셰이더 SDF.
        // grid 자식이라 스테이지 평면에 자동으로 코플레이너다 — 바닥 높이를 손으로 계산하지 않는다.
        private SpriteRenderer _rangeRing;
        private Material _rangeRingMat;
        private bool _rangeRingMatMissing;   // 미배선 경고 1회 게이트
        private static readonly int RingHalfExtentId = Shader.PropertyToID("_HalfExtent");
        private static readonly int RingRangeId = Shader.PropertyToID("_Range");
        private static readonly int RingQuadCellsId = Shader.PropertyToID("_QuadCells");
        private static readonly int RingColorId = Shader.PropertyToID("_Color");
        // 쿼드 한 변(셀 배수). 링 지름 = 2×(half + range) 이고 최대 사거리 5 + 여유 → 2×(0.5+5)+1 = 12.
        // 사거리마다 쿼드를 키우지 않고 **한 크기로 고정**한다 — 셰이더가 uv→타일 매핑에 이 값을 쓰므로
        // 여기가 단일 소스다(액체의 LiquidQuadCells 와 같은 규약).
        private const float RingQuadCells = 14f;

        // 점액 관성 — 표시용 당김 벡터(dir×t)를 스프링으로 지연/출렁. 신호(정책)는 그대로, 시각만 늦는다.
        private Vector2 _pullSmoothed;
        private Vector2 _pullVel;
        // 쿼드 한 변(셀 배수). 혀 최대 도달 = reach×t^pow + 방울반지름×신장 ≈ 오버슈트(1.2)에서 1.9셀 —
        // 캔버스(±절반)가 이보다 좁으면 옆 타일 위에서 칼로 잘린다. 셰이더 _QuadCells 와 동기(여기가 단일 소스).
        private const float LiquidQuadCells = 4f;
        private static readonly int PullId = Shader.PropertyToID("_Pull");
        private static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
        private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
        private static readonly int QuadCellsId = Shader.PropertyToID("_QuadCells");
        // tilemap-world-surround unit 2 — 배경 프랍 호스트(Deco) 판정용 셀/리전 메타 + 프랍 인스턴스 루트.
        // tilemap-world-surround unit 4 — 외곽 링 원경 프랍 인스턴스 루트.
        // prop-placement-layer unit 1 — goal/spawn 구조물 프랍 루트. 부모(90°X)를 역회전 상쇄해 메쉬가 똑바로 선다.
        // waypoint-routing 후속(사용자 결정 B, 2026-08-12) — 붕괴한 골의 프랍을 셀로 찾아
        // «이미 뚫린 곳» 으로 전환하기 위한 추적. 붕괴 후에도 프랍이 멀쩡히 서 있어서
        // 적이 그 골에서 소멸(유출 전환)하는 것이 «살아있는 마음을 안 때리는 버그» 로 읽혔다.
        // first-session-tutorial unit 2 — 셀 중심이 아니라 실제 구조물 renderer 중심을 노출한다.
        // 구조물 미사용 테마에서는 같은 API가 셀 중심으로 폴백한다.
        // first-session-tutorial unit 26 — 효과 타일이 칠해진 셀. 튜토리얼 마커 조회용이며
        // 소유권은 BattleBridge._effectTilesByCell 에 있다(여기는 "보이는 곳" 미러).
        private readonly List<Vector2Int> _effectTileCells = new();
        // effect-tiles unit 1 — 효과 타일 전용 런타임 타일맵. overlayTilemap 은 hover/reject 가
        // SetTile/null 로 덮어쓰므로 공유 금지. sorting -15 = ground(-20) 위 / overlay·hover(-10) 아래.
        // 런타임 생성 → 씬 SerializeField/저장 불필요.
        private Tilemap _effectTilemap;

        public Grid Grid => grid;
        // 평면 빌보드 프랍이 바닥 타일과 z-fight 나지 않도록 살짝 띄우는 world +Y 오프셋.
        private const float PropGroundLift = 0.02f;
        // defender-directional-volley unit 9 — 조준 화살표도 같은 이유로 띄운다. 프랍보다
        // 조금 더(범위 타일 위에도 얹혀야) — 여전히 시각적으로 무시 가능한 높이.
        private const float ArrowGroundLift = 0.05f;
        // unit 4 — 화살표를 조준 색에서 얼마나 밝히나. 화살표 스케일/알파(0.92·0.7·0.5)와 같은
        // 결의 프레젠테이션 상수 — 색 자체는 TileSetData 가 소유하고 여기선 명도만 민다.
        private const float AimArrowLighten = 0.72f;

        // BattleBridge 맵 빌드 시 호출 (unit 2). Grid cellLayout/cellSize 를 설정한 뒤
        // 전체 셀을 일괄 페인트한다. 재진입(RebuildDraftMap) 안전 — Clear 선행.
        public void Initialize(in GeneratedMap map, float tileSize, TileSetData tileSet,
            bool realShadows = false)
        {
            Clear();
            _gridSize = map.IsCreated ? map.gridSize : default;
            _tileSet = tileSet;
            ConfigureGrid(tileSize, realShadows);
            // map-diorama-stage unit 3 — 바닥 페인팅(PaintGround/PaintSurroundRing) 은퇴.
            // 바닥 비주얼은 스테이지 프리팹(디오라마)이 소유하고, 타일맵은 오버레이 캔버스만 남는다.
            PaintMarkers(in map);
            // map-diorama-stage unit 2 (critic C-1) — CenterBoardAtWorldOrigin 은 제거됐다.
            // grid.transform 의 유일한 writer 는 브리지의 스테이지 정렬(AlignGridTo)이다.
            // map-diorama-stage unit 4 — VisualPlan/구조물 앵커 은퇴: 프랍·앵커는 스테이지 마커 소유.
        }

        // map-diorama-stage unit 2 (critic C-1) — grid.transform 의 유일한 writer.
        // 셀 (0,0)의 최소 모서리를 지정 월드 위치(=스테이지의 gridOriginLocal)에 맞춘다.
        // 구 CenterBoardAtWorldOrigin(-= 상대 이동)은 은퇴 — writer 가 둘이면 프랍-논리 정렬이
        // 조용히 깨지고, 격자 기준 검증은 전부 통과한 채로 깨진다.
        public void AlignGridTo(Vector3 cellZeroMinCornerWorld)
        {
            if (grid == null) return;
            grid.transform.position = cellZeroMinCornerWorld;
        }

        public void Clear()
        {
            StopAllFlashes();
            if (_commitPopCo != null) { StopCoroutine(_commitPopCo); _commitPopCo = null; }
            if (_commitPop != null) { SafeDestroy(_commitPop.gameObject); _commitPop = null; } // grid 자식 → 맵 리빌드 시 함께 정리
            if (_liquidTile != null) { SafeDestroy(_liquidTile.gameObject); _liquidTile = null; } // unit 7 — 액체 하이라이트도 동일
            if (_liquidTileMat != null) { SafeDestroy(_liquidTileMat); _liquidTileMat = null; }
            if (_rangeRing != null) { SafeDestroy(_rangeRing.gameObject); _rangeRing = null; }   // unit 5 — 링도 동일
            if (_rangeRingMat != null) { SafeDestroy(_rangeRingMat); _rangeRingMat = null; }
            _rangeRingMatMissing = false;
            _liquidTileMatMissing = false; // 맵 리빌드 시 tileSet 이 바뀔 수 있으니 재시도 허용
            _hoverCells.Clear();
            if (groundTilemap != null) groundTilemap.ClearAllTiles();
            if (overlayTilemap != null) overlayTilemap.ClearAllTiles();
            if (_effectTilemap != null) _effectTilemap.ClearAllTiles();
            if (_rangeTilemap != null) _rangeTilemap.ClearAllTiles();
            _rangeCells.Clear();
            _rangeInvalid = false; // unit 3 — 맵 리빌드 경계 방어(정상 경로는 호출부 리셋이 덮는다)
            if (_placeableTilemap != null) _placeableTilemap.ClearAllTiles(); // placement-eligible-tile-highlight unit 1
            _placeableActive = false;
            if (_blockedTilemap != null) _blockedTilemap.ClearAllTiles(); // first-run-tutorial unit 1
            _blockedActive = false;
            ClearZoneCells(); // active-ally-zone unit 2 — 맵 리빌드/티어다운에서 장판 점등 회수
            ClearTelegraphCells(); // ultimate-leap unit 4 — 예고가 맵 너머로 살아남지 않게
            ClearGhostCells(); // defender-footprint unit 2 — 고스트도 맵 경계에서 회수
            _effectTileCells.Clear(); // unit 26 — 타일맵을 비웠으니 기억도 비운다(맵 리빌드 경계)
        }

        private void Update()
        {
            if (_tileSet == null) return;
            // 사거리 알파 — 펄스 제거(사용자 요청): 정적 레벨(rangePulseMaxAlpha)만 적용.
            // 세기 차이(방향 미정 십자 vs 선택된 레인)는 _rangeAlphaMul 배율로만.
            if (_rangeTilemap != null && _rangeCells.Count > 0) ApplyRangeTint();
            // ultimate-leap unit 4 — 예고 tint 도 같은 이유로 매 프레임(셀이 있을 때만).
            if (_telegraphCells.Count > 0) ApplyTelegraphTint();
            // placement-eligible-tile-highlight unit 1 — 배치 하이라이트 페이드인(정적, 펄스 없음).
            // range 와 독립: 탭 arm 처럼 range 가 없어도 페이드가 돌아야 한다.
            if (_placeableActive && _placeableTilemap != null)
            {
                float pt = _tileSet.placeableFadeInDuration > 0f
                    ? Mathf.Clamp01((Time.unscaledTime - _placeableShowTime) / _tileSet.placeableFadeInDuration) : 1f;
                var pc = _tileSet.placeableColor; pc.a *= pt;
                _placeableTilemap.color = pc;
            }
            // first-run-tutorial unit 1 — 불가 하이라이트도 같은 페이드 규약(placeableFadeInDuration 공유).
            if (_blockedActive && _blockedTilemap != null)
            {
                float bt = _tileSet.placeableFadeInDuration > 0f
                    ? Mathf.Clamp01((Time.unscaledTime - _blockedShowTime) / _tileSet.placeableFadeInDuration) : 1f;
                var bc = _tileSet.blockedColor; bc.a *= bt;
                _blockedTilemap.color = bc;
            }
        }

        private void ConfigureGrid(float tileSize, bool realShadows)
        {
            if (grid == null) return;
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSize = new Vector3(tileSize, tileSize, 1f);

            // tilted-billboard — 타일맵을 XZ 바닥에 눕힌다(퍼스펙티브 3D 룩). grid 로컬 XY → 월드 XZ.
            // BoardSpace.ToView/ToSim/RaycastPlane 가 모두 grid 기준이라 회전을 자동 추종한다.
            grid.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 셀 중심 anchor — GetCellCenterWorld 정합의 전제 (정합 테스트와 일치).
            var anchor = new Vector3(0.5f, 0.5f, 0f);
            if (groundTilemap != null) groundTilemap.tileAnchor = anchor;
            if (overlayTilemap != null) overlayTilemap.tileAnchor = anchor;

            // unit 4 — "보드 레이어 < 유닛 레이어" 1규칙. 유닛/VFX 는 BoardSortOrder(양수) 사용 → 보드는 음수.
            SetRendererSorting(groundTilemap, -20);
            SetRendererSorting(overlayTilemap, -10);

            // tilemap-real-shadows — 진짜 그림자 모드일 때만 바닥 receive 머티리얼 적용. 아니면 기존 룩 유지.
            // 타일/맵은 RECEIVE 만(유닛·프랍만 CAST). receive 셰이더엔 ShadowCaster 패스가 없어 이미
            // cast 못 하지만, 의도 못박기 위해 두 타일맵 모두 cast off 를 명시한다.
            SetRendererCastShadows(groundTilemap, false);
            SetRendererCastShadows(overlayTilemap, false);
            if (realShadows && groundShadowMaterial != null && groundTilemap != null)
            {
                var tmr = groundTilemap.GetComponent<TilemapRenderer>();
                if (tmr != null)
                {
                    tmr.sharedMaterial = groundShadowMaterial;
                    tmr.receiveShadows = true;
                }
            }
        }

        // camera-direction unit 8 — 플레이 그리드(경로·배치 셀)의 월드 bounds.
        // **ground 렌더러 실측을 쓰면 안 된다** — 렌더러 bounds 는 외곽 터레인 링과 데코 지대까지
        // 포함해 20×12 맵이 35×32 로 잡히고, 카메라가 거리 54 까지 물러나 플레이 영역이 화면
        // 중앙의 작은 조각이 된다. 그래서 grid 셀 좌표로 플레이 범위만 직접 만든다.
        // **4코너를 모두 감싼다** — grid 는 회전해 있어서(XZ 바닥 90°X) 마주보는 두 코너만으로는
        // 월드 AABB 의 극단을 놓친다. 높이는 0(평면) — 유닛이 솟는 만큼은 fit margin 이 흡수한다.
        public bool TryGetPlayfieldWorldBounds(Vector2Int gridSize, out Bounds bounds)
        {
            bounds = default;
            if (grid == null || gridSize.x <= 0 || gridSize.y <= 0) return false;
            bounds = new Bounds(grid.CellToWorld(new Vector3Int(0, 0, 0)), Vector3.zero);
            bounds.Encapsulate(grid.CellToWorld(new Vector3Int(gridSize.x, 0, 0)));
            bounds.Encapsulate(grid.CellToWorld(new Vector3Int(0, gridSize.y, 0)));
            bounds.Encapsulate(grid.CellToWorld(new Vector3Int(gridSize.x, gridSize.y, 0)));
            return true;
        }

        private static void SetRendererSorting(Tilemap tilemap, int order)
        {
            if (tilemap == null) return;
            var r = tilemap.GetComponent<TilemapRenderer>();
            if (r != null) r.sortingOrder = order;
        }

        // placement-enemy-see-through unit 6 — 드래그 중 배치 하이라이트(range/overlay)를 적(빌보드) 위로 임시 상승.
        // 10000/10002 = 유닛(Compute 수백)·투사체(+1000) 위, 힛바(16000)·드래그 프리뷰(20000) 아래.
        // 드래그 종료 시 기본값(overlay -10 / range -12) 복원. "보드<유닛" 기본 규칙은 드래그 밖에서 불변.
        public void SetPlacementHighlightAboveUnits(bool above)
        {
            _highlightAbove = above; // range/placeable 타일맵이 아직 없으면 Ensure 시 이 값을 반영.
            SetRendererSorting(overlayTilemap, above ? 10002 : -10);
            // distance-based-range unit 5 커밋3 — **두 분기가 같은 값이다.** 사거리 채움도 링과 함께
            // 바닥에 남는다(사용자 조건 3): 세계에 그린 도형이 스프라이트를 관통하면 UI 로 읽힌다.
            // 이전에는 드래그 중 10000 으로 떠서 적 빌보드를 뚫고 보였다
            // (placement-enemy-see-through unit 6). **그 가림은 이제 채움이 아니라 링이 감당하고,
            // 링의 끊김은 채움이 흡수한다** — 정렬로 피하지 않는다.
            SetRendererSorting(_rangeTilemap, -12);
            SetRendererSorting(_placeableTilemap, above ? 9998 : -13); // placement-eligible-tile-highlight unit 1
            SetRendererSorting(_zoneTilemap, above ? 9997 : -14);     // active-ally-zone unit 2
            // ultimate-leap unit 4 — **이 목록에 반드시 있어야 한다.** 빠지면 예고가 -13 에 굳어,
            // 플레이어가 유닛을 빼려고 드래그를 시작하는 **바로 그 순간**(이 스킬이 성립하는 순간)
            // range/placeable 만 위로 올라가고 예고가 그 아래로 묻힌다.
            SetRendererSorting(_telegraphTilemap, above ? 9999 : -13);
            // first-run-tutorial unit 1 — 같은 이유로 이 목록에 있어야 한다. 지금은 맵 설명 전용이라
            // 드래그와 겹칠 일이 없지만, 빠뜨린 타일맵은 언젠가 -13 에 굳는다(위 ultimate-leap 사례).
            SetRendererSorting(_blockedTilemap, above ? 9998 : -13);
            // defender-footprint unit 2 — 고스트는 range(10000/-12) **위**: 확정될 영역·사유가
            // 최우선 정보다. hover overlay(10002)보다는 아래.
            SetRendererSorting(_ghostTilemap, above ? 10001 : -11);
            // distance-based-range unit 5 — **두 분기가 같은 값이다.** 링은 드래그 중에도
            // 유닛 위로 올라가지 않는다(사용자 결정 2026-08-31): 세계에 그린 도형이 스프라이트를
            // 관통하면 UI 오버레이로 읽혀 물성이 깨진다. **끊김은 채움이 흡수한다.**
            // 그럼에도 이 목록에 있는 이유 = 빠뜨린 렌더러가 옛 값에 굳는 사고 방지(궁극기 예고 선례).
            if (_rangeRing != null) _rangeRing.sortingOrder = BoardSortOrder.RangeRingOrder;
        }

        // tilemap-real-shadows — 타일/맵은 그림자를 드리우지 않는다(유닛·프랍만 cast).
        private static void SetRendererCastShadows(Tilemap tilemap, bool cast)
        {
            if (tilemap == null) return;
            var r = tilemap.GetComponent<TilemapRenderer>();
            if (r != null)
                r.shadowCastingMode = cast
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void PaintMarkers(in GeneratedMap map)
        {
            if (overlayTilemap == null || _tileSet == null || !map.IsCreated) return;

            if (_tileSet.goalTile != null)
            {
                // multi-goal-map — 골 마커 goals 순회(폴백 map.goal)
                if (map.goals.IsCreated && map.goals.Length > 0)
                    for (int i = 0; i < map.goals.Length; i++)
                        overlayTilemap.SetTile(ToCell(map.goals[i]), _tileSet.goalTile);
                else
                    overlayTilemap.SetTile(ToCell(map.goal), _tileSet.goalTile);
            }

            if (_tileSet.spawnTile != null && map.spawns.IsCreated)
            {
                for (int i = 0; i < map.spawns.Length; i++)
                    overlayTilemap.SetTile(ToCell(map.spawns[i]), _tileSet.spawnTile);
            }
        }

        // --- 배치 피드백 (SetPlacementHover/FlashTileReject/ClearPlacementHover) ---

        public void SetPlacementHover(Vector2Int cell, bool valid)
        {
            if (overlayTilemap == null || _tileSet == null) return;
            StopFlash(cell);
            overlayTilemap.SetTile(ToCell(cell), valid ? _tileSet.hoverTile : _tileSet.rejectTile);
            _hoverCells.Add(cell);
        }

        public void ClearPlacementHover(Vector2Int cell)
        {
            if (!_hoverCells.Remove(cell)) return;
            if (overlayTilemap != null) overlayTilemap.SetTile(ToCell(cell), null);
        }

        public void ClearPlacementHover()
        {
            if (overlayTilemap != null)
                foreach (var cell in _hoverCells)
                    overlayTilemap.SetTile(ToCell(cell), null);
            _hoverCells.Clear();
        }

        public void FlashTileReject(Vector2Int cell)
        {
            if (overlayTilemap == null || _tileSet == null || _tileSet.rejectTile == null) return;
            ClearPlacementHover(cell);
            StopFlash(cell);
            _activeFlashes[cell] = StartCoroutine(FlashCoroutine(cell));
        }

        private IEnumerator FlashCoroutine(Vector2Int cell)
        {
            var pos = ToCell(cell);
            overlayTilemap.SetTile(pos, _tileSet.rejectTile);
            yield return new WaitForSeconds(0.2f);
            if (overlayTilemap != null) overlayTilemap.SetTile(pos, null);
            _activeFlashes.Remove(cell);
        }

        private void StopFlash(Vector2Int cell)
        {
            if (_activeFlashes.TryGetValue(cell, out var c) && c != null) StopCoroutine(c);
            _activeFlashes.Remove(cell);
        }

        private void StopAllFlashes()
        {
            foreach (var c in _activeFlashes.Values)
                if (c != null) StopCoroutine(c);
            _activeFlashes.Clear();
        }

        // placement-cell-snap unit 4 — 포커스 타일 확정(변경) 시 셀 위에 스케일 오버슈트+알파 페이드 팝.
        // 하이라이트 타일(overlay)은 그대로 남고, 이 팝이 "여기로 확정됨"을 한 번 punctuate 한다.
        public void PulsePlacementHover(Vector2Int cell, bool valid)
        {
            if (grid == null) return;
            var sr = EnsureCommitPop();
            // 액체 하이라이트와 동일 — Ground(ZWrite On) 코플레이너 z-fight 방지 리프트.
            var popLocal = grid.CellToLocalInterpolated(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f));
            popLocal.z = -PropGroundLift;
            sr.transform.localPosition = popLocal;
            Color c = valid ? commitPopValidColor : commitPopInvalidColor;
            if (_commitPopCo != null) StopCoroutine(_commitPopCo);
            _commitPopCo = StartCoroutine(CommitPopCoroutine(sr, c));
        }

        private SpriteRenderer EnsureCommitPop()
        {
            if (_commitPop != null) return _commitPop;
            var go = new GameObject("PlacementCommitPop");
            go.transform.SetParent(grid.transform, false); // grid 자식 → 타일과 코플레이너
            go.transform.localRotation = Quaternion.identity;
            _commitPop = go.AddComponent<SpriteRenderer>();
            _commitPop.sprite = PopSprite();
            _commitPop.sortingOrder = BoardSortOrder.PlacementCommitPopOrder;
            var overlayR = overlayTilemap != null ? overlayTilemap.GetComponent<TilemapRenderer>() : null;
            if (overlayR != null) _commitPop.sortingLayerID = overlayR.sortingLayerID; // overlay 와 같은 sorting layer
            go.SetActive(false);
            return _commitPop;
        }

        // placement-cell-snap unit 7 rev — 포커스 셀 하이라이트 자체가 끈적한 액체(오버레이 아님).
        // dir = 당김 방향(셀 공간), t = 0(중심)~1(파열). 신호는 PlacementCellSnap.EvaluateStretch 가 Resolve 와
        // 같은 밴드로 계산 → t=1 이 실제 전환점과 일치(하이라이트가 거짓말 안 함).
        // 렌더 = 셀 2배 쿼드 1장 + Wassup/PlacementLiquidTile 셰이더(SDF): 테두리(둥근사각)는 셀에 고정 =
        // "릴리즈하면 여기" 계약, 내부 fill 은 손가락 방향 액적과 smin 블렌드로 번지다 테두리를 넘는다.
        // 모양 튜닝은 .mat 인스펙터(라이브 반영). 쿼드는 회전하지 않는다 — 테두리는 축 정렬, 방향은 셰이더 uniform.
        public void SetPlacementStretch(Vector2Int cell, Vector2 dir, float t, bool valid)
        {
            if (grid == null) return;
            var sr = EnsureLiquidTile();
            if (sr == null) return; // 머티리얼 미배선 — EnsureLiquidTile 이 1회 경고
            t = Mathf.Clamp01(t);
            float cs = grid.cellSize.x; // rect 보드·균일 cellSize 전제(ConfigureGrid)

            // 점액 관성 — 목표 당김(dir×t)을 스프링 추종. 늦게 따라오고, 멈추면/셀이 넘어가면 출렁하며 이완.
            // 표시가 숨겨져 있었으면 스냅 리셋(이전 세션 잔여 스윙 금지). 드래프트 일시정지에도 살아야 하니 unscaled.
            Vector2 targetPull = dir * t;
            if (!sr.gameObject.activeSelf) { _pullSmoothed = targetPull; _pullVel = Vector2.zero; }
            else
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f); // 히치 스파이크 시 폭주 방지
                _pullVel += (targetPull - _pullSmoothed) * (liquidSpring * dt);
                _pullVel /= 1f + liquidDamping * dt;
                _pullSmoothed += _pullVel * dt;
            }
            float tS = _pullSmoothed.magnitude;
            Vector2 dirS = tS > 1e-4f ? _pullSmoothed / tS : Vector2.zero;
            tS = Mathf.Min(tS, 1.2f); // 셰이더 허용 오버슈트 한계와 동기

            // local -Z = world +Y(부모 90°X 회전). Ground 가 ZWrite On 이라 코플레이너면 z-fight 로 깜빡인다 —
            // 프랍과 같은 해법(PropGroundLift)으로 띄운다. 타일맵끼리는 같은 메쉬라 안 겪는 문제.
            var local = grid.CellToLocalInterpolated(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f));
            local.z = -PropGroundLift;
            sr.transform.localPosition = local;
            sr.transform.localScale = new Vector3(LiquidQuadCells * cs, LiquidQuadCells * cs, 1f);
            _liquidTileMat.SetVector(PullId, new Vector4(dirS.x, dirS.y, tS, 0f));
            _liquidTileMat.SetColor(BorderColorId, valid ? liquidValidBorder : liquidInvalidBorder);
            _liquidTileMat.SetColor(FillColorId, valid ? liquidValidFill : liquidInvalidFill);
            if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
        }

        public void ClearPlacementStretch()
        {
            if (_liquidTile != null) _liquidTile.gameObject.SetActive(false);
        }

        private SpriteRenderer EnsureLiquidTile()
        {
            if (_liquidTile != null) return _liquidTile;
            if (_liquidTileMatMissing) return null;
            var srcMat = _tileSet != null ? _tileSet.placementLiquidMaterial : null;
            if (srcMat == null)
            {
                // 에디터 한정 폴백 없이 명시 실패 — Shader.Find 폴백은 기기 빌드에서만 조용히 죽는다(2026-07-15 사고).
                Debug.LogWarning("TilemapMapView: TileSetData.placementLiquidMaterial 미할당 — 액체 하이라이트 생략. " +
                                 "PlacementLiquidTile.mat 을 tileSet 에 배선할 것.", this);
                _liquidTileMatMissing = true;
                return null;
            }
            _liquidTileMat = new Material(srcMat); // 인스턴스 — 에셋 원본을 런타임 파라미터로 오염시키지 않는다
            _liquidTileMat.SetFloat(QuadCellsId, LiquidQuadCells); // 쿼드 크기 ↔ 셰이더 매핑 단일 소스 동기
            var go = new GameObject("PlacementLiquidTile");
            go.transform.SetParent(grid.transform, false); // grid 자식 → 타일과 코플레이너. 회전 없음(테두리 축 정렬).
            go.transform.localRotation = Quaternion.identity;
            _liquidTile = go.AddComponent<SpriteRenderer>();
            _liquidTile.sprite = PopSprite(); // 1×1 흰 full-rect — 모양은 전부 셰이더 SDF
            _liquidTile.sharedMaterial = _liquidTileMat;
            _liquidTile.sortingOrder = BoardSortOrder.PlacementLiquidOrder;
            var overlayR = overlayTilemap != null ? overlayTilemap.GetComponent<TilemapRenderer>() : null;
            if (overlayR != null) _liquidTile.sortingLayerID = overlayR.sortingLayerID;
            go.SetActive(false);
            return _liquidTile;
        }

        private SpriteRenderer EnsureRangeRing()
        {
            if (_rangeRing != null) return _rangeRing;
            if (_rangeRingMatMissing) return null;
            var srcMat = _tileSet != null ? _tileSet.placementRangeRingMaterial : null;
            if (srcMat == null)
            {
                // 에디터 한정 폴백 없이 명시 실패 — Shader.Find 폴백은 기기 빌드에서만 조용히 죽는다.
                Debug.LogWarning("TilemapMapView: TileSetData.placementRangeRingMaterial 미할당 — 사거리 링 생략. " +
                                 "PlacementRangeRing.mat 을 tileSet 에 배선할 것.", this);
                _rangeRingMatMissing = true;
                return null;
            }
            _rangeRingMat = new Material(srcMat);
            _rangeRingMat.SetFloat(RingQuadCellsId, RingQuadCells); // 쿼드 크기 ↔ 셰이더 매핑 단일 소스 동기
            var go = new GameObject("PlacementRangeRing");
            go.transform.SetParent(grid.transform, false); // grid 자식 → 타일과 코플레이너. 회전 없음.
            go.transform.localRotation = Quaternion.identity;
            _rangeRing = go.AddComponent<SpriteRenderer>();
            _rangeRing.sprite = PopSprite();   // 1×1 흰 full-rect — 모양은 전부 셰이더 SDF
            _rangeRing.sharedMaterial = _rangeRingMat;
            _rangeRing.sortingOrder = BoardSortOrder.RangeRingOrder;
            var overlayR = overlayTilemap != null ? overlayTilemap.GetComponent<TilemapRenderer>() : null;
            if (overlayR != null) _rangeRing.sortingLayerID = overlayR.sortingLayerID;
            go.SetActive(false);
            return _rangeRing;
        }

        // 사거리 링을 한 칸 중심에 띄운다. **인자가 판정 입력의 복사본이다** — 호출부가
        // `AttackReach` 에 넣는 값을 그대로 넣어야 「판정의 경계 그 자체」가 참으로 유지된다.
        // halfExtentTiles = 유닛 몸의 반폭(오늘 전 유닛 0.5), rangeTiles = 사거리.
        private void ShowRangeRing(Vector2Int center, float rangeTiles, float halfExtentTiles)
        {
            var sr = EnsureRangeRing();
            if (sr == null) return;   // 머티리얼 미배선 — EnsureRangeRing 이 1회 경고
            float cs = grid.cellSize.x;   // rect 보드·균일 cellSize 전제(ConfigureGrid)
            // ⚠ **`GetCellCenterLocal` 을 쓰면 안 된다** — 그건 z 에 0.5(셀 중심)를 넣는다.
            // 부모가 90°X 회전이라 local −Z = world +Y 이므로 그 0.5 가 **보드에서 0.5 유닛 뜨는**
            // 결과가 되고, 55° 카메라에서 링이 타일과 눈에 띄게 어긋난다(실측 후 수정).
            // 확정 팝·액체 하이라이트와 **같은 관용구**를 쓴다: CellToLocalInterpolated + 접지 리프트.
            var local = grid.CellToLocalInterpolated(new Vector3(center.x + 0.5f, center.y + 0.5f, 0f));
            local.z = -PropGroundLift;   // Ground(ZWrite On) 코플레이너 z-fight 방지
            sr.transform.localPosition = local;
            sr.transform.localScale = new Vector3(RingQuadCells * cs, RingQuadCells * cs, 1f);
            _rangeRingMat.SetVector(RingHalfExtentId, new Vector4(halfExtentTiles, halfExtentTiles, 0f, 0f));
            _rangeRingMat.SetFloat(RingRangeId, rangeTiles);
            // 선과 채움은 **같은 라임**이어야 한다(사용자 조건 2) — 시안(고스트)·노랑(점유)·
            // 무채색(지형)과 색상 자체로 갈려야 채움 두 겹 문제가 재발하지 않는다.
            // 그래서 링 색을 저작에서 끌어온다. 셰이더 기본값에 맡기면 둘이 조용히 갈린다.
            var ringColor = _tileSet.rangeColor;
            ringColor.a = _tileSet.rangeRingAlpha;
            _rangeRingMat.SetColor(RingColorId, ringColor);
            if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
        }

        private void HideRangeRing()
        {
            if (_rangeRing != null && _rangeRing.gameObject.activeSelf) _rangeRing.gameObject.SetActive(false);
        }

        // defender-directional-volley unit 9 — 방향 지정 화살표. 보드에 눕는 탭 어포던스라
        // 블롭/팝과 같은 방식(grid 자식 절차적 스프라이트)으로 그린다. 이 뷰는 write-only —
        // 어느 칸에 어느 각도로 무엇이 선택됐는지는 전부 호출부가 정해 넘긴다.
        // emphasized — unit 5. 방향이 확정된 상태(그 레인만 남은 상태)면 전부 또렷·크게,
        // 미선택(4레인 전부 표시)이면 전부 흐리게·작게. 개별 선택 인덱스는 없다.
        public void SetAimArrows(IReadOnlyList<Vector2Int> cells, IReadOnlyList<float> anglesDeg, bool emphasized)
        {
            if (grid == null || cells == null || anglesDeg == null || _tileSet == null) return;
            float cellWorld = grid.cellSize.x;
            for (int i = 0; i < cells.Count && i < anglesDeg.Count; i++)
            {
                var sr = EnsureAimArrow(i);
                var local = grid.CellToLocalInterpolated(new Vector3(cells[i].x + 0.5f, cells[i].y + 0.5f, 0f));
                sr.transform.localPosition = local;
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, anglesDeg[i]);
                // 셀 평면에 그대로 눕히면 불투명 바닥 타일과 코플레이너라 z-acne(자글거림)가
                // 난다 — 밉맵으로도 안 사라지는 렌더 단계 문제. 프랍이 PropGroundLift 로
                // 푸는 바로 그 함정. world +Y 로 살짝 띄워 항상 타일 앞에 오게 한다(부모
                // 회전/스케일 무관하게 정확히 +Y 만큼 — localPosition 뒤 world 로 보정).
                sr.transform.position += Vector3.up * ArrowGroundLift;

                bool on = emphasized;
                // 선택된 화살표만 또렷하고 살짝 크다. 색은 조준 슬롯(unit 4)에서 오되 **명도를
                // 올려** 쓴다 — 레인이 solid 로 채워지면 같은 색 화살표는 그 위에서 사라진다.
                // 같은 색상(hue)을 유지하니 "레인과 한 몸"이라는 신호는 남고, 값 대비로 읽힌다.
                var c = Color.Lerp(RangeTintColor(aimStyle: true), Color.white, AimArrowLighten);
                c.a = on ? 1f : 0.5f;
                sr.color = c;
                float s = cellWorld * (on ? 0.92f : 0.7f);
                sr.transform.localScale = new Vector3(s, s, 1f);
                sr.gameObject.SetActive(true);
            }
            for (int i = cells.Count; i < _aimArrows.Count; i++) _aimArrows[i].gameObject.SetActive(false);
        }

        public void ClearAimArrows()
        {
            for (int i = 0; i < _aimArrows.Count; i++)
                if (_aimArrows[i] != null) _aimArrows[i].gameObject.SetActive(false);
        }

        private SpriteRenderer EnsureAimArrow(int index)
        {
            while (_aimArrows.Count <= index)
            {
                var go = new GameObject($"AimArrow{_aimArrows.Count}");
                go.transform.SetParent(grid.transform, false); // grid 자식 → 타일과 코플레이너
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ArrowSprite();
                sr.sortingOrder = BoardSortOrder.AimArrowOrder;
                var overlayR = overlayTilemap != null ? overlayTilemap.GetComponent<TilemapRenderer>() : null;
                if (overlayR != null) sr.sortingLayerID = overlayR.sortingLayerID;
                go.SetActive(false);
                _aimArrows.Add(sr);
            }
            return _aimArrows[index];
        }

        // +Y 를 향하는 삼각형(호출부가 Z 회전으로 방향을 만든다). 가장자리를 살짝 흐려
        // 계단을 없앤다 — 타일 위에 눕는 작은 도형이라 에일리어싱이 그대로 보인다.
        // 꼭짓점 (0, 0.9) / 밑변 (±0.75, -0.7). 한 점이 삼각형 안이면 true(하드 테스트).
        private static bool InArrowTriangle(float u, float v)
        {
            if (v < -0.7f || v > 0.9f) return false;
            float halfWidth = Mathf.Lerp(0f, 0.75f, Mathf.InverseLerp(0.9f, -0.7f, v));
            return Mathf.Abs(u) <= halfWidth;
        }

        private static Sprite ArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;
            // 자글거림은 두 층이다:
            // (1) 소스 텍스처 앨리어싱 — 저해상도 + 대각 빗변에 수직이 아닌 페더. 해법 =
            //     커버리지 슈퍼샘플링(텍셀당 SS×SS 하드 테스트 → 통과 비율을 알파로).
            // (2) 렌더 minification — 보드가 40~58° 기울어 화살표가 비스듬히 눕는다.
            //     밉맵이 없으면 축소 방향에서 계단이 그대로 샌다(둥근 블롭은 저주파라
            //     안 보이지만 화살표는 대각 샤프 엣지라 두드러진다). 해법 = 밉체인 +
            //     Trilinear + aniso. 소팅(11500)은 레인 타일 위라 z-fight 는 아니다.
            const int R = 128;
            const int SS = 4;
            var tex = new Texture2D(R, R, TextureFormat.RGBA32, mipChain: true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            var px = new Color[R * R];
            float inv = 1f / (SS * SS);
            for (int y = 0; y < R; y++)
                for (int x = 0; x < R; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float u = (x + (sx + 0.5f) / SS) / R * 2f - 1f;
                            float v = (y + (sy + 0.5f) / SS) / R * 2f - 1f;
                            if (InArrowTriangle(u, v)) hits++;
                        }
                    px[y * R + x] = new Color(1f, 1f, 1f, hits * inv);
                }
            tex.SetPixels(px);
            tex.Apply();
            _arrowSprite = Sprite.Create(tex, new Rect(0f, 0f, R, R), new Vector2(0.5f, 0.5f), R);
            return _arrowSprite;
        }

        private static Sprite PopSprite()
        {
            if (_popSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _popSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), tex.width); // pivot 중심, 1 world unit → localScale 로 타일 크기 맞춤
            }
            return _popSprite;
        }

        private IEnumerator CommitPopCoroutine(SpriteRenderer sr, Color color)
        {
            float cellWorld = grid != null ? grid.cellSize.x : 1f; // 로컬 1셀 크기
            sr.gameObject.SetActive(true);
            float t = 0f;
            float dur = Mathf.Max(commitPopDuration, 0.01f);
            while (t < dur)
            {
                float u = t / dur;
                float ease = 1f - (1f - u) * (1f - u); // OutQuad — 빠르게 커졌다 감속 안착
                float s = Mathf.Lerp(commitPopStartScale, commitPopEndScale, ease) * cellWorld;
                sr.transform.localScale = new Vector3(s, s, 1f);
                color.a = Mathf.Lerp(commitPopStartAlpha, 0f, ease);
                sr.color = color;
                t += Time.unscaledDeltaTime; // 배치 슬로우모 무관 실시간
                yield return null;
            }
            sr.gameObject.SetActive(false);
            _commitPopCo = null;
        }

        // effect-tiles unit 1 — 효과 타일 페인트. Initialize(Clear 포함) 이후 호출 계약 (아니면 지워짐).
        public void SetEffectTile(Vector2Int cell, TileBase tile)
        {
            if (grid == null) return;
            EnsureEffectTilemap();
            _effectTilemap.SetTile(ToCell(cell), tile);
            // first-session-tutorial unit 26 — 칠한 셀을 기억한다. 튜토리얼이 "빛나는 타일" 하나를
            // 월드 마커로 지목하는 데 쓴다. 앵커를 미리 굳히지 않고 셀만 들고 조회 시점에
            // CellCenterToWorld 로 푸는 이유는 그리드 설정이 재빌드로 바뀔 수 있어서다.
            // 같은 셀 재페인트는 덮어쓰기이므로(BattleBridge._effectTilesByCell 과 동형) 중복을 막는다.
            if (tile != null) { if (!_effectTileCells.Contains(cell)) _effectTileCells.Add(cell); }
            else _effectTileCells.Remove(cell);
        }

        // unit 26 — 튜토리얼 조회용. 인덱스는 페인트 순서이고 의미는 없다(어느 하나면 된다).
        public int EffectTileCount => _effectTileCells.Count;

        public bool TryGetEffectTileAnchor(int index, out Vector3 worldPosition)
        {
            if (index >= 0 && index < _effectTileCells.Count)
            {
                var cell = _effectTileCells[index];
                worldPosition = CellCenterToWorld(cell.x, cell.y);
                return true;
            }

            worldPosition = default;
            return false;
        }

        // 효과 타일맵 전용 머티리얼 지정(펄스 발광 등). null 이면 기본 유지. TilemapRenderer 는
        // 타일맵당 머티리얼 1개라 전용 _effectTilemap 전체에 균일 적용된다.
        public void SetEffectTileMaterial(Material material)
        {
            if (grid == null || material == null) return;
            EnsureEffectTilemap();
            var r = _effectTilemap.GetComponent<TilemapRenderer>();
            if (r != null) r.sharedMaterial = material;
        }

        private void EnsureEffectTilemap()
        {
            if (_effectTilemap != null) return;
            var go = new GameObject("EffectTiles");
            go.transform.SetParent(grid.transform, false); // grid 90°X 회전 상속 — ground/overlay 와 동일 평면
            _effectTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _effectTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f); // 셀 중심 anchor (정합 전제와 일치)
            r.sortingOrder = -15;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void EnsureRangeTilemap()
        {
            if (_rangeTilemap != null) return;
            if (grid == null) return;
            var go = new GameObject("PlacementRangeTiles");
            go.transform.SetParent(grid.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.05f); // z-fight 방지(ground depth 평면 분리). placeable(-0.04)보다 카메라 쪽.
            _rangeTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _rangeTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            r.sortingOrder = _highlightAbove ? 10000 : -12; // 드래그 중 lazy 생성 시 상승 반영(unit 6).
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (overlayTilemap != null)
            {
                var or = overlayTilemap.GetComponent<TilemapRenderer>();
                if (or != null) r.sharedMaterial = or.sharedMaterial;
            }
        }

        // unit 4 — 범위 표시의 두 스타일. 조준 페이즈 슬롯이 비어 있으면(기본) 전부 range*
        // 로 폴백해 기존 동작과 바이트 동일하다 — 스타일 분리는 에셋 할당으로 켜진다.
        private TileBase RangeTileFor(bool aimStyle)
            => aimStyle && _tileSet.aimRangeTile != null ? _tileSet.aimRangeTile : _tileSet.rangeTile;

        // 알파는 세기 배율(_rangeAlphaMul) 을 곱하기 전 기준값까지만 담는다 — 배율 적용은
        // 알파를 소유한 Update() 의 몫(unit 9 계약).
        private Color RangeTintColor(bool aimStyle)
        {
            // placement-thumb-occlusion — **유효성 면제는 에셋 유무가 아니라 aimStyle 이 결정한다.**
            // 초판은 `aimStyle && aimRangeTile != null` 로 묶어서, aimRangeTile 미배선 tileset(새 시즌을
            // CreateAssetMenu 로 만들면 기본 null)에서 조준 페이즈가 아래 invalid 분기로 떨어져 배치
            // 유효성의 적색을 상속했다 — range-preview unit 4 가 약속한 "슬롯 비면 기존 동작과 바이트
            // 동일" 폴백이 깨진다. aimStyle 은 "조준 채널이다"라는 **의미**로 쓰고, 에셋 유무는 타일·색
            // 폴백에만 쓴다(스타일 축과 유효성 축을 직교로 유지).
            if (aimStyle)
            {
                if (_tileSet.aimRangeTile == null)
                {
                    var fc = _tileSet.rangeColor; fc.a = _tileSet.rangePulseMaxAlpha; return fc;
                }
                var ac = _tileSet.aimRangeColor; ac.a = _tileSet.aimRangeAlpha; return ac;
            }
            // placement-thumb-occlusion unit 3 — 배치 불가면 적색 + 전이 순간 1회 플래시.
            // aimStyle 경로는 유효성을 모른다(드롭 후 방향 지정 채널). 단 스킬 조준/텔레그래프는
            // aimStyle=false 로 그려지므로 이 분기 밖이 아니라 **owner 전환 시 리셋**이 그들을 지킨다
            // (BattleBridge.ClearRange) — 색이 아니라 소유권이 경계다.
            if (_rangeInvalid)
            {
                var ic = _tileSet.rangeInvalidColor;
                ic.a = RangeFillAlpha();
                float flashDur = _tileSet.rangeInvalidFlashSeconds;
                if (flashDur > 0f) // boost 0 은 아래 Lerp 가 항등이라 별도 가드 불요(0除만 막는다)
                {
                    float t = Mathf.Clamp01((Time.unscaledTime - _rangeInvalidSince) / flashDur);
                    float boost = _tileSet.rangeInvalidFlashBoost * (1f - t);
                    ic = Color.Lerp(ic, Color.white, boost);
                    ic.a = Mathf.Lerp(RangeFillAlpha(), 1f, boost);
                }
                return ic;
            }
            var c = _tileSet.rangeColor; c.a = RangeFillAlpha(); return c;
        }

        // 채움 알파는 **링이 있느냐가 정한다**(unit 5 커밋3).
        //   링 있음 → 옅게. 링이 「어디까지 닿나」를 말하고 채움은 **보험**이다
        //             (선이 유닛·프랍에 끊겨도 영역이 남는다 — 사용자 조건 1).
        //   링 없음 → 그대로. 스킬 조준·텔레그래프·방향 레인은 채움이 **유일한 신호**다.
        private float RangeFillAlpha()
            => _rangeRing != null && _rangeRing.gameObject.activeSelf
                ? _tileSet.rangeFillAlphaUnderRing
                : _tileSet.rangePulseMaxAlpha;

        // placement-thumb-occlusion unit 3 — 배치 판정 유효성. **이 메서드가 유일한 소유자**다.
        // Set/ClearPlacementRange 는 절대 건드리지 않는다: SetPlacementRange 가 내부에서
        // ClearPlacementRange 를 먼저 부르므로(:903/:927) 거기서 리셋하면 셀이 바뀔 때마다
        // false→true 전이가 재발생해 무효 영역을 훑는 동안 플래시가 연발한다.
        // 세션 경계 리셋은 호출부(컨트롤러 ClearHover/ClearBoardScout, bridge ClearRange)가 명시적으로 한다.
        public void SetPlacementRangeValidity(bool valid)
        {
            bool invalid = !valid;
            if (invalid == _rangeInvalid) return; // 전이에만 반응 — 매 프레임 호출돼도 스팸 아님
            _rangeInvalid = invalid;
            if (invalid) _rangeInvalidSince = Time.unscaledTime;
        }

        // includeCenter — 배치 프리뷰는 중심 셀(유닛 위치)을 비우고, 스킬 AOE 는
        // 중심도 피해 범위라 포함한다 (range-preview unit 3).
        // `squareShape` — 칠하는 모양이 **정사각형**인가.
        //
        // ⚠ 기본은 false(사거리 술어를 그대로 따르는 둥근 모양)다. distance-based-range unit 5:
        // **표기는 판정에서 나온다.** 사거리가 몸 기준 거리로 바뀐 뒤(unit 4a) 프리뷰만 정사각형으로
        // 남으면 사거리 2의 정대각 칸이 밝게 켜지는데 그 칸의 적은 안 맞는다 — 화면이 규칙을
        // **틀리게 가르치는** 상태다.
        //
        // true 로 부르는 곳은 **스킬 조준·텔레그래프** 하나뿐이고, 그건 거짓말이 아니라
        // **사실**이다 — 스킬 광역의 멤버십은 `TileAoe.IsInTileRange`(정사각형)로 남아 있다
        // (결정 4 — 저작이 칸 단위 조준이다). 표기가 그 자를 따라가는 것이 맞다.
        public void SetPlacementRange(Vector2Int center, int tileRange, bool includeCenter = false,
                                      bool squareShape = false)
        {
            if (grid == null || _tileSet == null || _tileSet.rangeTile == null || tileRange <= 0) return;
            ClearPlacementRange();
            EnsureRangeTilemap();
            _rangeAlphaMul = 1f;
            _rangeAimStyle = false;
            for (int dx = -tileRange; dx <= tileRange; dx++)
            for (int dz = -tileRange; dz <= tileRange; dz++)
            {
                if (!includeCenter && dx == 0 && dz == 0) continue;
                var cell = new Vector2Int(center.x + dx, center.y + dz);
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                // 판정과 **같은 본체**를 지난다 — 여기서 모양을 다시 그리지 않는다.
                if (!squareShape && !Wassup.Battle.Combat.AttackReach.InCellReach(
                        new Unity.Mathematics.int2(center.x, center.y),
                        new Unity.Mathematics.int2(cell.x, cell.y), tileRange)) continue;
                _rangeTilemap.SetTile(ToCell(cell), _tileSet.rangeTile);
                _rangeCells.Add(cell);
            }
            // unit 5 — 윤곽. **채움과 같은 입력**에서 나온다(모양을 다시 그리지 않는다).
            // 스킬 조준(squareShape)은 자가 정사각형이라 링을 띄우지 않는다 — 거짓말이 되기 때문.
            if (squareShape) HideRangeRing();
            else ShowRangeRing(center, tileRange, Wassup.Skills.SkillMath.SelfHalfWidthTiles);
            ApplyRangeTint();
        }

        // defender-directional-volley unit 9 — 임의 셀 집합 점등(방향 레인). 사각 범위와
        // 같은 타일맵·수명·펄스를 공유하고 셀 목록만 호출부가 정한다. alphaMul = 세기
        // (방향 미정 십자는 흐리게, 선택된 레인은 또렷하게).
        // aimStyle — unit 4. 조준 페이즈(레인/착지셀)는 배치 단계와 다른 타일·색을 쓴다.
        // 드래그 프리뷰·스킬 조준/텔레그래프는 false 로 남아 기존 outline 그대로.
        public void SetPlacementCells(IReadOnlyList<Vector2Int> cells, float alphaMul = 1f, bool aimStyle = false)
        {
            if (grid == null || _tileSet == null || _tileSet.rangeTile == null || cells == null) return;
            ClearPlacementRange();
            EnsureRangeTilemap();
            _rangeAlphaMul = Mathf.Clamp01(alphaMul);
            _rangeAimStyle = aimStyle;
            // unit 5 — 임의 셀 집합(방향 레인·스킬 조준)에는 링이 없다. 레인은 폭 0 정수 격자
            // 술어라 거리로 옮길 수 없고(비목표), 네모/원 어느 쪽으로 그려도 거짓말이 된다.
            HideRangeRing();
            var tile = RangeTileFor(aimStyle);
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                _rangeTilemap.SetTile(ToCell(cell), tile);
                _rangeCells.Add(cell);
            }
            // 이 프레임의 Update() 가 이미 지났으면 새 타일이 옛 색으로 한 프레임 보인다 —
            // 두 스타일의 색이 다르므로 즉시 반영한다(리페인트는 방향이 바뀔 때만이라 저렴).
            ApplyRangeTint();
        }

        private void ApplyRangeTint()
        {
            if (_rangeTilemap == null) return;
            var c = RangeTintColor(_rangeAimStyle); c.a *= _rangeAlphaMul;
            _rangeTilemap.color = c;
        }

        public void ClearPlacementRange()
        {
            HideRangeRing();   // unit 5 — 채움과 수명을 공유한다
            if (_rangeCells.Count == 0) return;
            if (_rangeTilemap != null)
                foreach (var cell in _rangeCells) _rangeTilemap.SetTile(ToCell(cell), null);
            _rangeCells.Clear();
        }

        // ── active-ally-zone unit 2 — 액티브 아군 장판 점등 ──────────────────────
        // 전용 타일맵이다. 조준 프리뷰(range)·맵 효과 타일(effect) 채널은 둘 다 단일 owner
        // set/clear 라 재사용하면 서로를 지운다(조준이 장판을 지우거나 그 반대).
        //
        // **칸별 refcount 필수**: 장판은 동시에 여러 장 존재할 수 있어서, 단순 set/clear 를
        // 복사하면 먼저 만료된 장판이 겹친 칸을 지우고 살아 있는 장판의 발자국이 사라진다.
        private Tilemap _zoneTilemap;
        private readonly Dictionary<Vector2Int, int> _zoneCellRefs = new Dictionary<Vector2Int, int>();

        public void AddZoneCells(IReadOnlyList<Vector2Int> cells)
        {
            if (grid == null || _tileSet == null || _tileSet.rangeTile == null || cells == null) return;
            EnsureZoneTilemap();
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                _zoneCellRefs.TryGetValue(cell, out int refs);
                _zoneCellRefs[cell] = refs + 1;
                if (refs == 0) _zoneTilemap.SetTile(ToCell(cell), _tileSet.rangeTile);
            }
        }

        public void RemoveZoneCells(IReadOnlyList<Vector2Int> cells)
        {
            if (_zoneTilemap == null || cells == null) return;
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (!_zoneCellRefs.TryGetValue(cell, out int refs)) continue;
                if (refs <= 1)
                {
                    _zoneCellRefs.Remove(cell);
                    _zoneTilemap.SetTile(ToCell(cell), null); // 마지막 참조만 실제로 끈다
                }
                else _zoneCellRefs[cell] = refs - 1;
            }
        }

        public void ClearZoneCells()
        {
            _zoneCellRefs.Clear();
            if (_zoneTilemap != null) _zoneTilemap.ClearAllTiles();
        }

        // ── ultimate-leap unit 4 — 착지 예고 타일 ────────────────────────────────
        // **또 하나의 전용 타일맵이다.** range 채널을 공유할 수 없다: 그쪽은 `SetPlacementRange`/
        // `SetPlacementCells` 가 매번 `ClearPlacementRange()` 로 시작하는 단일 owner set/clear 라,
        // 예고 2초 동안 플레이어가 유닛을 드래그하면 배치 프리뷰와 예고가 서로를 지운다.
        // (예고 중 배치는 막을 수 없다 — 유닛을 빼고 다시 놓는 것이 이 스킬의 놀이다.)
        //
        // zone 처럼 refcount 는 두지 않는다: 궁극기는 생존당 1회라 동시 예고가 존재할 수 없다.
        // 두 번째 소비처가 생기면 그 spec 이 refcount 를 붙인다(제약 8).
        private Tilemap _telegraphTilemap;
        private readonly List<Vector2Int> _telegraphCells = new List<Vector2Int>();
        private bool _telegraphFallbackWarned; // 배선 누락 경고 1회 제한(매 발동 스팸 방지)

        public void SetTelegraphCells(IReadOnlyList<Vector2Int> cells)
        {
            if (grid == null || _tileSet == null || cells == null) return;
            // **전용 채움 타일을 쓴다.** 다른 채널 타일을 빌리면 그쪽 저작 의도에 종속된다 —
            // `placeableTile`(슬랩)은 자체 색이 회색(0.80)이라 tint 를 곱하면 색이 죽어 "어두운
            // dim" 으로만 보이고, `rangeTile` 은 격자 outline 이라 면적이 없어 주변시로 안 읽힌다.
            // `telegraphTile` 은 흰색 solid + TileFlags.None 이라 아래 tint 가 원색으로 실린다.
            // 폴백은 "안 보이는 것보다 낫다" 수준의 열화 경로다.
            var tile = _tileSet.telegraphTile;
            if (tile == null)
            {
                // 폴백은 남기되(예고가 아예 안 뜨면 회피 불가 = 불공정) **한 번은 시끄럽게** 알린다.
                // 실제로 이 조용한 열화가 "색이 안 먹는다" 로 위장해 색·정렬·머티리얼·스프라이트를
                // 차례로 의심하게 만들었다 — 원인은 tileSet 하나의 배선 누락이었다.
                // ⚠ tileSet 은 여러 개다(예: Generated/ 아래 실사용본). **전부** 배선해야 한다.
                if (!_telegraphFallbackWarned)
                {
                    _telegraphFallbackWarned = true;
                    Debug.LogWarning($"[TilemapMapView] '{_tileSet.name}' 에 telegraphTile 이 없어 " +
                        "placeable/range 타일로 폴백한다 — 그 타일들은 자체 색이 있어 예고 tint 가 죽는다. " +
                        "TileSetData.telegraphTile 을 배선하라.", this);
                }
                tile = _tileSet.placeableTile != null ? _tileSet.placeableTile : _tileSet.rangeTile;
            }
            if (tile == null) return;
            ClearTelegraphCells();
            EnsureTelegraphTilemap();
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                _telegraphTilemap.SetTile(ToCell(cell), tile);
                _telegraphCells.Add(cell);
            }
        }

        public void ClearTelegraphCells()
        {
            if (_telegraphCells.Count == 0) return;
            if (_telegraphTilemap != null)
                foreach (var cell in _telegraphCells) _telegraphTilemap.SetTile(ToCell(cell), null);
            _telegraphCells.Clear();
        }

        private void EnsureTelegraphTilemap()
        {
            if (_telegraphTilemap != null) return;
            if (grid == null) return;
            var go = new GameObject("LandingTelegraphTiles");
            go.transform.SetParent(grid.transform, false);
            // zone(-0.03) 위, range(-0.05) 아래 — 예고는 배치 프리뷰에 가려지지 않아야 한다.
            go.transform.localPosition = new Vector3(0f, 0f, -0.045f);
            _telegraphTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _telegraphTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            // placeable(9998/-13) 바로 위 — 예고는 배치 하이라이트에 가리면 안 된다.
            // ⚠ 이 값을 바꾸면 `SetPlacementHighlightAboveUnits` 의 같은 쌍도 함께 바꿔야 한다.
            r.sortingOrder = _highlightAbove ? 9999 : -13;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (overlayTilemap != null) // 검증된 반투명 tint 경로 재사용
            {
                var or = overlayTilemap.GetComponent<TilemapRenderer>();
                if (or != null) r.sharedMaterial = or.sharedMaterial;
            }
            _telegraphTilemap.color = landingTelegraphColor;
        }

        // 생성 시 1회 대입만 하면 **Play 중 인스펙터 튜닝이 안 먹는다** — 타일맵 GameObject 는 맵
        // 리빌드까지 살아남으므로 첫 생성 때의 색이 굳는다. range 가 `ApplyRangeTint()` 를 매 프레임
        // 호출하는 것과 같은 이유로 여기도 매 프레임 반영한다(셀이 있을 때만 — 평소 비용 0).
        private void ApplyTelegraphTint()
        {
            if (_telegraphTilemap == null) return;
            if (_telegraphTilemap.color != landingTelegraphColor)
                _telegraphTilemap.color = landingTelegraphColor;
        }

        private void EnsureZoneTilemap()
        {
            if (_zoneTilemap != null) return;
            if (grid == null) return;
            var go = new GameObject("AllyZoneTiles");
            go.transform.SetParent(grid.transform, false);
            // z-fight 방지 + 깊이 순서: effect(-15) 위, placeable(-0.04)/range(-0.05) 아래.
            go.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            _zoneTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _zoneTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            r.sortingOrder = _highlightAbove ? 9997 : -14;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (overlayTilemap != null) // 검증된 반투명 tint 경로 재사용(range/placeable 과 동일)
            {
                var or = overlayTilemap.GetComponent<TilemapRenderer>();
                if (or != null) r.sharedMaterial = or.sharedMaterial;
            }
            _zoneTilemap.color = allyZoneColor;
        }

        // placement-eligible-tile-highlight unit 1 — 배치 가능 셀 밝은 하이라이트(전용 타일맵, range 와 분리).
        private void EnsurePlaceableTilemap()
        {
            if (_placeableTilemap != null) return;
            if (grid == null) return;
            var go = new GameObject("PlacementHighlightTiles");
            go.transform.SetParent(grid.transform, false);
            // z-fight 방지: ground(TileShadowReceive = depth write)와 coplanar 면 카메라 이동 중 자글거림.
            // grid 는 90°X 회전 → local -Z 가 카메라 쪽(world +Y). 살짝 띄워 깊이 평면 분리(셀 정렬 영향 없음).
            go.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            _placeableTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _placeableTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            r.sortingOrder = _highlightAbove ? 9998 : -13; // 정적 −13(effect −15 위·range −12 아래) / 드래그 상승 9998.
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (overlayTilemap != null) // 검증된 반투명 tint 경로 재사용(range 와 동일).
            {
                var or = overlayTilemap.GetComponent<TilemapRenderer>();
                if (or != null) r.sharedMaterial = or.sharedMaterial;
            }
        }

        // 배치 가능 셀 집합을 은은한 fill+림으로 밝힌다. 균일 tint(per-cell 색 없음). 색/알파는 Update() 소유.
        public void SetPlacementHighlight(IReadOnlyList<Vector2Int> placeable)
        {
            if (grid == null || _tileSet == null || _tileSet.placeableTile == null || placeable == null) return;
            EnsurePlaceableTilemap();
            if (!_placeableActive)
            {
                _placeableActive = true;
                _placeableShowTime = Time.unscaledTime;
                var c0 = _tileSet.placeableColor; c0.a = 0f;
                _placeableTilemap.color = c0; // 첫 프레임 흰 불투명 번쩍 방지(Update 가 알파 세팅 전)
            }
            _placeableTilemap.ClearAllTiles();
            for (int i = 0; i < placeable.Count; i++)
            {
                var cell = placeable[i];
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                _placeableTilemap.SetTile(ToCell(cell), _tileSet.placeableTile);
            }
            // 리프레시(_placeableActive 유지)면 showTime 안 리셋 → 재페이드 없음.
        }

        public void ClearPlacementHighlight()
        {
            if (_placeableTilemap != null) _placeableTilemap.ClearAllTiles();
            _placeableActive = false;
        }

        // defender-footprint unit 2 — footprint 고스트 + 주변 배치불가 컨텍스트 레이어.
        // per-cell 색이 필요해 placeable(균일 tint)과 별개다. 채움 타일은 telegraphTile(흰 solid +
        // TileFlags.None — 아래 SetColor 가 원색으로 실린다), 폴백은 placeableTile(색이 죽지만
        // 안 보이는 것보단 낫다 — SetTelegraphCells 의 폴백 규약과 동일).
        private Tilemap _ghostTilemap;
        private readonly List<Vector2Int> _ghostCells = new List<Vector2Int>();

        private void EnsureGhostTilemap()
        {
            if (_ghostTilemap != null) return;
            if (grid == null) return;
            var go = new GameObject("FootprintGhostTiles");
            go.transform.SetParent(grid.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.05f); // ground 와 깊이 평면 분리(z-fight)
            _ghostTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _ghostTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            r.sortingOrder = _highlightAbove ? 10001 : -11; // SetPlacementHighlightAboveUnits 의 표와 동일
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (overlayTilemap != null) // 검증된 반투명 tint 경로 재사용(placeable 과 동일)
            {
                var or = overlayTilemap.GetComponent<TilemapRenderer>();
                if (or != null) r.sharedMaterial = or.sharedMaterial;
            }
        }

        public void SetGhostCells(IReadOnlyList<Vector2Int> cells, IReadOnlyList<Color> colors)
        {
            if (grid == null || _tileSet == null || cells == null || colors == null || colors.Count != cells.Count)
                return;
            var tile = _tileSet.telegraphTile != null ? _tileSet.telegraphTile : _tileSet.placeableTile;
            if (tile == null) return;
            ClearGhostCells();
            EnsureGhostTilemap();
            if (_ghostTilemap == null) return;
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                var tc = ToCell(cell);
                _ghostTilemap.SetTile(tc, tile);
                _ghostTilemap.SetTileFlags(tc, TileFlags.None);
                _ghostTilemap.SetColor(tc, colors[i]);
                _ghostCells.Add(cell);
            }
        }

        public void ClearGhostCells()
        {
            if (_ghostTilemap == null || _ghostCells.Count == 0) return;
            _ghostTilemap.ClearAllTiles();
            _ghostCells.Clear();
        }

        // defender-footprint unit 2 rev 2 — 전역 배치불가 고스트가 **사거리 표시 칸을 비켜 가기**
        // 위한 read seam. 사거리 링 위에 빨강/노랑이 얹히면 링이 붉게 물들어 사거리 읽기가
        // 흐려진다(사용자 피드백 2026-08-28) — 겹친 칸은 고스트 쪽이 양보한다.
        public bool IsPlacementRangeCell(Vector2Int cell) => _rangeCells.Contains(cell);

        // first-run-tutorial unit 1 — 배치 **불가** 칸 하이라이트. EnsurePlaceableTilemap 과 같은 관용구.
        private void EnsureBlockedTilemap()
        {
            if (_blockedTilemap != null) return;
            if (grid == null) return;
            var go = new GameObject("PlacementBlockedTiles");
            go.transform.SetParent(grid.transform, false);
            // placeable(-0.04)과 겹치지 않게 한 겹 더 띄운다 — 두 하이라이트를 동시에 켜는 구간이 있다.
            go.transform.localPosition = new Vector3(0f, 0f, -0.045f);
            _blockedTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _blockedTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            r.sortingOrder = _highlightAbove ? 9998 : -13;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (overlayTilemap != null) // placeable 과 같은 검증된 반투명 tint 경로.
            {
                var or = overlayTilemap.GetComponent<TilemapRenderer>();
                if (or != null) r.sharedMaterial = or.sharedMaterial;
            }
        }

        // 배치 불가 셀 집합을 칠한다. blockedTile 미할당이면 조용히 no-op(placeable 과 같은 규약).
        public void SetBlockedHighlight(IReadOnlyList<Vector2Int> blocked)
        {
            if (grid == null || _tileSet == null || _tileSet.blockedTile == null || blocked == null) return;
            EnsureBlockedTilemap();
            if (!_blockedActive)
            {
                _blockedActive = true;
                _blockedShowTime = Time.unscaledTime;
                var c0 = _tileSet.blockedColor; c0.a = 0f;
                _blockedTilemap.color = c0; // 첫 프레임 흰 불투명 번쩍 방지
            }
            _blockedTilemap.ClearAllTiles();
            for (int i = 0; i < blocked.Count; i++)
            {
                var cell = blocked[i];
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                _blockedTilemap.SetTile(ToCell(cell), _tileSet.blockedTile);
            }
        }

        public void ClearBlockedHighlight()
        {
            if (_blockedTilemap != null) _blockedTilemap.ClearAllTiles();
            _blockedActive = false;
        }

        // grid 권위 cell→world. BoardSpace.ToView 와 동일 셀중심(+0.5) 수식. 바닥 z-fight 회피용 미세 +Y lift.
        public Vector3 CellCenterToWorld(float cellX, float cellY)
        {
            if (grid == null) return new Vector3(cellX, 0f, cellY);
            Vector3 world = grid.transform.TransformPoint(
                grid.CellToLocalInterpolated(new Vector3(cellX + 0.5f, cellY + 0.5f, 0f)));
            world.y += PropGroundLift;
            return world;
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        // GeneratedMap 셀 (x, y) → Tilemap cell (x, y, 0). 변환 헬퍼 단일 지점.
        private static Vector3Int ToCell(int2 cell) => new Vector3Int(cell.x, cell.y, 0);
        private static Vector3Int ToCell(Vector2Int cell) => new Vector3Int(cell.x, cell.y, 0);
    }
}
