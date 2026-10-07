using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Skills;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Trigger;
using SimEntityId = Wassup.BattleCore.SimEntityId;
using Wassup.Core;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 5b — **판 위에 그리는 것.** 옛 `TilemapMapView`(1,608줄)에서
    // 오버레이 몫만 가져왔다(바닥 페인팅은 스테이지 프리팹이, 평면 선언은 `CoreBoardPlane` 이
    // 이미 소유한다 — 옛 뷰가 셋을 겸하던 것을 5a 에서 쪼갰다).
    //
    // 그리는 것(번호는 추가 순):
    //   ① 격자 — 「칸이 있다」를 말한다. 디오라마 바닥에는 칸 선이 없다.
    //   ② 배치 가이드 — 이 유닛을 **놓을 수 없는 칸**들, 이유별 2색(사용자 결정 2026-09-23).
    //   ③ 고스트 — 지금 손가락이 가리키는 footprint(초록/빨강).
    //   ④ 사거리 링 + 사정권 표식 — 「여기 놓으면 저기까지 닿는다」.
    //   ⑤ 공격 도형 가이드 — 방향 유닛(부채꼴·띠)이 **지금 물 적** 쪽으로 「같이 맞는 범위」
    //      (directional-attack-shape unit 6 — 6c 후속에서 이식 누락을 메웠다).
    //   ④′ 사거리 칸 채움 — 「어느 칸이 사거리 안인가」의 논리 집합(`IsPlacementRangeCell`) + 링 안 채움 한 겹(unit 8a2).
    //   ⑧ 궁극기 착지 예고 — **전용 채널**(배치·카드 채널과 공유하지 않는다, unit 8a2).
    //   ⑨ 효과 타일 칸 — 판마다 한 번, 코어가 뽑은 칸을 그 종류의 저작 타일로(unit 8a2).
    //
    // ⚠ **판정을 한 줄도 갖지 않는다.**
    //   · 「놓을 수 있나」 = `PlacementService` 호출. 고스트는 `Judge`(그 유닛을 그 앵커에),
    //     가이드는 `CellStateAt`(그 **칸**의 상태) — **다른 질문이지 다른 자가 아니다**.
    //     가이드를 유닛으로 물으면 답이 끌고 있는 footprint 만큼 부풀고(민코프스키 합),
    //     자원까지 섞이면 코스트 재생마다 보드가 통째로 깜빡인다.
    //   · 「닿나」 = `AttackReach.InReach` 호출. 제약 13 이 금지하는 것이 정확히 여기서
    //     이중 루프로 모양을 다시 그리는 것이다 — 그러면 「밝은 칸인데 안 때린다」가 되고,
    //     그건 가장 나쁜 종류의 버그다(화면이 규칙을 **틀리게** 가르친다).
    //   링의 **반지름**은 판정이 아니라 그 판정을 그리는 치수라 여기서 곱한다.
    [DisallowMultipleComponent]
    public sealed class CoreMapOverlay : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Header("격자")]
        [SerializeField] private bool _showGrid = true;
        [SerializeField] private Color _gridColor = new Color(1f, 1f, 1f, 0.09f);
        [SerializeField, Min(0.002f)] private float _gridWidth = 0.02f;

        [Header("배치 가이드")]
        // ⚠ **룩을 여기서 지어내지 않는다.** 못 놓는 칸의 그림·두 색·페이드는 전부 이 타일셋이
        // 정본이다: `blockedSprite`(흰 solid) · `blockedColor`(지형·프랍) · `occupiedColor`(유닛
        // 점유) · `placeableFadeInDuration`. 정적(펄스 없음)이고 초록은 안 쓴다 — 초록은 고스트
        // (hover)의 것이다. 비어 있으면 가이드를 **안 그린다** — 임시 색을 코드에 두면 그게
        // 다음 사람의 정본이 된다.
        [Tooltip("못 놓는 칸의 스프라이트·두 색·페이드 저작. 비면 가이드를 그리지 않는다.")]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("_tileSet")] private BoardOverlayStyle _style;
        [SerializeField] private Color _ghostOkColor = new Color(0.45f, 0.92f, 0.5f, 0.55f);
        [SerializeField] private Color _ghostBadColor = new Color(0.95f, 0.32f, 0.3f, 0.5f);
        [Tooltip("가이드를 다시 칠하는 주기(초). 0 = 매 프레임.")]
        [SerializeField, Min(0f)] private float _guideRepaintSeconds = 0.15f;

        [Header("사거리")]
        [SerializeField] private Color _ringColor = new Color(0.55f, 0.95f, 1f, 0.85f);
        [SerializeField, Min(0.005f)] private float _ringWidth = 0.05f;
        [SerializeField, Min(8)] private int _ringSegments = 64;

        [Tooltip("보드 평면 법선(카메라 쪽) 띄움. 바닥과의 z-fighting 회피 전용.")]
        [SerializeField, Min(0f)] private float _surfaceOffset = 0.05f;

        private readonly List<SpriteRenderer> _guideCells = new List<SpriteRenderer>(64);
        private readonly List<SpriteRenderer> _ghostCells = new List<SpriteRenderer>(8);
        private readonly List<SpriteRenderer> _marks = new List<SpriteRenderer>(16);
        private readonly List<Vector3> _ringPoints = new List<Vector3>(80);
        // ④′ 사거리 칸 채움(unit 8a2 행 8 — rule-holders T3·T13). 「어느 칸이 사거리 안인가」의 **논리 집합**과 링 안 채움.
        private readonly HashSet<int2> _rangeCells = new HashSet<int2>();
        private int _rangeCellsDef = -1;
        private int2 _rangeCellsAnchor;
        private MeshRenderer _rangeFill;
        private Mesh _rangeFillMesh;

        private LineRenderer _grid;
        private LineRenderer _ring;
        private Sprite _cellSprite;
        private Sprite _markSprite;
        private Texture2D _cellTex;
        private Texture2D _markTex;
        private Material _material;
        private MaterialPropertyBlock _mpb;
        private Camera _camera;

        private bool _gridBuilt;
        private float _nextGuideRepaint;
        private int _guideDefIndex = -1;
        private int _guideUsed;
        // 페이드인 기준 시각(unscaledTime). 집는 **순간** 잡고 드롭까지 안 건드린다 —
        // 가이드를 다시 칠할 때(점유 변화) 리셋하면 판이 주기적으로 깜빡인다.
        private float _guideShownAt = -1f;
        // 칸마다 「유닛이 막았나(true) / 지형·프랍이 막았나(false)」. 색을 가르는 유일한 축이다.
        private readonly List<bool> _guideOccupied = new List<bool>(64);

        // ⑤ 공격 도형 가이드 — 채움·테 두 장(알파가 다르다). 메시는 도형 키가 바뀔 때만 다시 굽는다.
        private MeshRenderer _shapeFill, _shapeRim;
        private Mesh _shapeFillMesh, _shapeRimMesh;
        private ShapeMarkSpec _shapeKey;
        private bool _shapeKeyValid;
        private bool _shapeMatMissing;   // 머티리얼 실패 경고 1회 게이트(옛 `_shapeGuideMatMissing` 과 같은 규약)

        // ── 배치 입력이 미는 것 ───────────────────────────────────────────────
        //
        // **단방향 push** 다(카메라 bounds 와 같은 규약). 오버레이가 입력에서 당겨오면
        // 「지금 뭘 끌고 있나」의 주인이 둘이 된다.
        private int _dragDefIndex = -1;
        private int2 _dragAnchor;
        private bool _dragValid;
        private bool _hasDrag;

        /// <summary>오버레이 룩 저작(스프라이트 · 색 · 페이드). 테스트가 「룩이 데이터에서 나오나」를 묻는 창구이기도 하다.</summary>
        public BoardOverlayStyle Style => _style;

        /// <summary>드래그 중인 유닛과 그 앵커를 알린다. 매 프레임 불러도 된다.</summary>
        public void ShowPlacement(int defIndex, int2 anchor, bool valid)
        {
            if (defIndex != _dragDefIndex) _nextGuideRepaint = 0f;   // 유닛이 바뀌면 가이드를 즉시 다시 칠한다
            if (!_hasDrag) _guideShownAt = Time.unscaledTime;        // 집는 순간 = 페이드 시작
            _dragDefIndex = defIndex;
            _dragAnchor = anchor;
            _dragValid = valid;
            _hasDrag = true;
        }

        /// <summary>드래그가 끝났다. 가이드·고스트·링을 전부 내린다.</summary>
        public void HidePlacement()
        {
            _hasDrag = false;
            _dragDefIndex = -1;
            _guideShownAt = -1f;
            ClearRangeCells();
        }

        /// <summary>
        /// unit 8a2 행 8 — **그 칸이 지금 배치 사거리 안인가**(옛 `TilemapMapView.IsPlacementRangeCell` `:1537` · T13 read seam).
        /// 채움이 투명해도(링이 있으면 알파 0) 이 집합은 계속 참이다 — 「어느 칸이 사거리 안인가」를 묻는 소비자가 있다
        /// (자리 고스트가 사거리 칸을 비켜 가는 것 등). 판정이 아니다: 칸 집합은 표준 잡몹을 가정한 **배치 안내**다(T3).
        /// </summary>
        public bool IsPlacementRangeCell(int2 cell) => _rangeCells.Contains(cell);

        /// <summary>테스트 창구 — 사거리 칸 수 · 링 안 채움이 켜져 있나와 그 색.</summary>
        public int PlacementRangeCellCount => _rangeCells.Count;

        public bool TryGetRangeFill(out Color color)
        {
            color = default;
            if (_rangeFill == null || !_rangeFill.enabled) return false;
            color = _rangeFill.sharedMaterial != null ? _rangeFill.sharedMaterial.color : default;
            return true;
        }

        private void ClearRangeCells()
        {
            _rangeCells.Clear();
            _rangeCellsDef = -1;
            if (_rangeFill != null && _rangeFill.enabled) _rangeFill.enabled = false;
        }

        // ── ⑥ 카드 조준(unit 7c) — 손패 드래그가 미는 것 ─────────────────────────
        //
        // 옛 브리지 범위 채널(`RangeDisplayOwner`)의 카드 몫 둘: **부착 범위 링**(`SetAttachPreview` — host 몸 중심, 락온 유닛을 따라간다)
        // 과 **액티브 칸 조준**(`SetSkillAimRange`/`SetSkillAimCells` — 조준 칸 중심 원 · 반경 0 이면 그 칸들). 둘은 한 화면에 안 뜨므로
        // 채널 하나다. **배치 드래그가 살아 있으면 양보한다**(옛 H-2 — 배치 링을 훔치면 다음 칸 이동까지 사라진다).
        //
        // ⚠ **반경을 여기서 재지 않는다**(구현 4 · 제약 13). 부착 링 = `RangeSpec.RadiusWithOrigin(host 몸)` — 판정과 같은 매핑
        // (`SkillMath.TryOriginRadius`)을 **부르기만** 한다. 대상 몸은 더하지 않는다(대상 그림자가 링에 닿으면 걸린다 = 판정식과 동치).
        // 조준 원의 반경은 드래그 슬롯이 같은 함수로 낸 값이다. 스타일은 저작(`DreamcatcherFocusConfig.attachRangeStyle` ·
        // `BoardOverlayStyle.aimRingStyle` — 옛 두 채널의 값 그대로).
        // unit 7d — `Telegraph` = 낙하탄 착탄 예고(옛 `PinSkillTelegraph` — 옛 범위 채널의 `SkillTelegraph` 몫). 같은 채널이라
        // 「마지막에 쓴 자가 이긴다」·「반납은 주인만」이 옛 `SetRangeOwner`/`ClearRange` 규칙 그대로다.
        private enum AreaKind : byte { None = 0, Attach = 1, AimRing = 2, AimCells = 3, Telegraph = 4 }
        private AreaKind _area;
        private SimEntityId _areaHost = SimEntityId.None;
        private SimEntityId _telegraphId = SimEntityId.None;
        private RangeSpec _areaSpec = RangeSpec.None;
        private RangeRingStyle _areaStyle;
        private float3 _areaCenter;
        private float _areaRadius;
        private readonly List<int2> _aimCellList = new List<int2>(2);
        private readonly List<SpriteRenderer> _aimCells = new List<SpriteRenderer>(2);
        private LineRenderer _areaRing;
        private MeshRenderer _areaFill;
        private Mesh _areaFillMesh;
        private readonly List<Vector3> _areaPoints = new List<Vector3>(80);

        /// <summary>부착 범위 링을 그 host 에 건다(락온 전환 순간에만 불린다 — 추종은 여기서 매 프레임).</summary>
        public void ShowAttachRange(SimEntityId host, RangeSpec spec, RangeRingStyle style)
        {
            if (!host.IsEntity || spec.Shape == RangeShape.None || spec.RadiusTiles <= 0f) { HideAttachRange(); return; }
            _area = AreaKind.Attach;
            _areaHost = host;
            _areaSpec = spec;
            _areaStyle = style;
        }

        public void HideAttachRange()
        {
            if (_area == AreaKind.Attach) ClearArea();
        }

        /// <summary>액티브 칸 조준 — 조준 칸 중심의 원(반경은 호출부가 판정과 같은 함수로 낸 값).</summary>
        public void ShowAimRing(float3 centerSim, float radiusTiles)
        {
            if (radiusTiles <= 0f || _style == null) { HideAim(); return; }
            _area = AreaKind.AimRing;
            _areaCenter = centerSim;
            _areaRadius = radiusTiles;
            _areaStyle = _style.aimRingStyle;
        }

        /// <summary>액티브 칸 조준 — 칸 집합(반경 0 · 포탈 입구+출구 후보). 옛 `SetSkillAimCells`.</summary>
        public void ShowAimCells(List<int2> cells)
        {
            _area = AreaKind.AimCells;
            _aimCellList.Clear();
            if (cells != null) _aimCellList.AddRange(cells);
        }

        /// <summary>
        /// unit 7d — 착탄 예고 링. 반경은 호출부가 사건 값(`ProjectileSpawned.AreaTiles`)으로 `CoreDrawRadius` 를 지나 낸 값이다.
        /// `projectile` = 그 탄 — 반납은 **그 탄의 착탄·소멸**만 한다(옛: 남의 착탄이 예고를 지우면 안 된다).
        /// </summary>
        public void ShowTelegraph(SimEntityId projectile, float3 centerSim, float radiusTiles)
        {
            if (radiusTiles <= 0f || _style == null) return;
            _area = AreaKind.Telegraph;
            _telegraphId = projectile;
            _areaCenter = centerSim;
            _areaRadius = radiusTiles;
            _areaStyle = _style.aimRingStyle;
        }

        public void HideTelegraph(SimEntityId projectile)
        {
            if (_area == AreaKind.Telegraph && _telegraphId == projectile) ClearArea();
        }

        /// <summary>테스트 창구 — 지금 예고 중인 탄(없으면 None).</summary>
        public SimEntityId TelegraphProjectile => _area == AreaKind.Telegraph ? _telegraphId : SimEntityId.None;

        public void HideAim()
        {
            if (_area == AreaKind.AimRing || _area == AreaKind.AimCells) ClearArea();
        }

        /// <summary>테스트 창구 — 지금 떠 있는 카드 링의 반경(칸)과 중심(sim). 없으면 false.</summary>
        public bool TryGetCardArea(out float radiusTiles, out float3 centerSim)
        {
            radiusTiles = _areaRadius; centerSim = _areaCenter;
            return _areaRing != null && _areaRing.enabled;
        }

        public int ActiveAimCellCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _aimCells.Count; i++) if (_aimCells[i] != null && _aimCells[i].enabled) n++;
                return n;
            }
        }

        // ── ⑨ 효과 타일 칸(unit 8a2 행 1 — rule-holders T15) ──────────────────────────────
        //
        // 옛 `BattleBridge.AddEffectTile`(`:9075`) → `TilemapMapView.SetEffectTile`(`:1025`)의 후계. **어느 칸이 효과 타일인가**를
        // 판 위에 그린다. 소유는 코어다(`PlacementService.ArmedEffectTiles` — 판 시작에 한 번 뽑고 판 내내 안 바뀐다, 칸 소비
        // 없음). 여기는 「보이는 곳」만 — 판마다 한 번 칠한다(소비·회복 사건이 없어 구독할 것이 없다).
        // 그림 = 그 종류의 저작 스프라이트(`EffectTileData.overlaySprite`) · 머티리얼 = 테마 `effectTileMaterial`(펄스) —
        // 둘 다 `MatchViewAssets` 가 정의표 줄과 같은 순회로 나른다. 정렬 = `BoardSortOrder.EffectTileOrder`(옛 −15).
        private readonly List<SpriteRenderer> _effectCells = new List<SpriteRenderer>(4);
        private readonly List<int2> _effectCellList = new List<int2>(4);
        private BattleMatch _effectPaintedFor;

        /// <summary>테스트 창구 — 칠한 효과 타일 칸 수.</summary>
        public int EffectTileCellCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _effectCells.Count; i++) if (_effectCells[i] != null && _effectCells[i].enabled) n++;
                return n;
            }
        }

        /// <summary>테스트 창구 — 칠한 효과 타일 칸(칠한 순서).</summary>
        public bool TryGetEffectTileCell(int index, out int2 cell, out Sprite sprite)
        {
            cell = default; sprite = null;
            if (index < 0 || index >= _effectCellList.Count || index >= _effectCells.Count) return false;
            cell = _effectCellList[index];
            sprite = _effectCells[index] != null ? _effectCells[index].sprite : null;
            return _effectCells[index] != null && _effectCells[index].enabled;
        }

        private void PaintEffectTilesOnce()
        {
            var match = _driver.Match;
            if (match == null || ReferenceEquals(match, _effectPaintedFor)) return;
            _effectPaintedFor = match;

            var cells = match.Placement.ArmedEffectTiles;
            var assets = _driver.ViewAssets;
            _effectCellList.Clear();
            int used = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                var data = assets.EffectTile(match.Placement.EffectTileKindAt(cells[i]));
                // 저작 그림이 없으면 **안 그린다**(절차적 사각을 지어내면 그게 다음 사람의 정본이 된다 — 가이드와 같은 규약).
                var sprite = data != null ? data.overlaySprite : null;
                if (sprite == null) continue;

                while (_effectCells.Count <= used)
                {
                    var go = new GameObject($"{name}_effectTile_{_effectCells.Count}");
                    go.transform.SetParent(transform, false);
                    _effectCells.Add(go.AddComponent<SpriteRenderer>());
                }
                var sr = _effectCells[used++];
                sr.sprite = sprite;
                sr.sortingOrder = BoardSortOrder.EffectTileOrder;
                var mat = assets.EffectTileMaterial;
                sr.sharedMaterial = mat != null ? mat : Material();
                sr.color = Color.white;                    // 펄스 머티리얼은 정점색을 읽는다(흰 스프라이트 — 색은 머티리얼·틴트가 입힌다)
                if (mat == null) Tint(sr, Color.white);   // 오버레이 기본 머티리얼은 프로퍼티 블록 색을 읽는다
                sr.transform.position = ViewOf(CellCenterSim(cells[i]));
                sr.transform.rotation = PlaneRotation();
                float w = sprite.bounds.size.x;
                sr.transform.localScale = Vector3.one * (_driver.TileSize / (w > 1e-5f ? w : 1f));   // 한 칸을 덮는다
                sr.enabled = true;
                _effectCellList.Add(cells[i]);
            }
            SetCount(_effectCells, used);
        }

        // ── ⑧ 궁극기 착지 예고(unit 8a2 행 2 — rule-holders T16·T17) ────────────────────────
        //
        // 옛 `BattleBridge.UltimateLeap.cs:87 ShowLandingTelegraph` → `TilemapMapView.SetTelegraphRing`(`:690`)의 후계.
        // 매체는 **원 링 하나**다(점 + 거리 — 옛 2026-09-07 사용자 지시 「타일말고 점기준으로」). 칸을 열거하지 않는다.
        //
        // ⚠⚠ **채널은 «전용» 이다**(T16). 위 카드 채널(`_area`)도 배치 링도 공유하지 않는다 — 예고 중에 유닛을 빼고 다시
        // 놓는 것이 이 스킬의 놀이라, 배치 프리뷰가 예고를 지우거나 예고가 배치 링을 지우면 안 된다(옛 `:641-649`).
        // 그래서 `_hasDrag` 에 양보하지 않고, 반납은 **그 도약자의 강하**만 한다.
        // 반경 = 슬램 칸 수 + 칸 반폭(자리형 — 보스의 몸을 읽지 않는다, 옛 `CenteredRingRadius`). 호출부가 사건 값으로 낸다.
        // 색 = 저작(`LeapVisualConfig.LandingTelegraphColor`) — 알파는 채움 세기, 선은 불투명(옛 `:699-703`).
        private SimEntityId _landingLeaper = SimEntityId.None;
        private float3 _landingCenter;
        private float _landingRadius;
        private Color _landingColor;
        private LineRenderer _landingRing;
        private MeshRenderer _landingFill;
        private Mesh _landingFillMesh;
        private bool _landingFillWarned;
        private readonly List<Vector3> _landingPoints = new List<Vector3>(80);

        /// <summary>착지 예고를 건다(궁극기 이탈 순간). 동시 예고는 없다(궁극기는 생존당 1회 — T16 비고) — 마지막이 이긴다.</summary>
        public void ShowLandingTelegraph(SimEntityId leaper, float3 centerSim, float radiusTiles, Color color)
        {
            if (!leaper.IsEntity || radiusTiles <= 0f) return;
            _landingLeaper = leaper;
            _landingCenter = centerSim;
            _landingRadius = radiusTiles;
            _landingColor = color;
        }

        /// <summary>그 도약자의 예고를 내린다(강하 확정 순간 — 강하 연출 끝까지 남기면 「아직 피할 수 있다」는 거짓 신호).</summary>
        public void HideLandingTelegraph(SimEntityId leaper)
        {
            if (_landingLeaper == leaper) _landingLeaper = SimEntityId.None;
        }

        /// <summary>테스트 창구 — 지금 떠 있는 착지 예고(도약자 · 중심 sim · 반경 칸). 없으면 false.</summary>
        public bool TryGetLandingTelegraph(out SimEntityId leaper, out float3 centerSim, out float radiusTiles)
        {
            leaper = _landingLeaper; centerSim = _landingCenter; radiusTiles = _landingRadius;
            return !_landingLeaper.IsNone && _landingRing != null && _landingRing.enabled;
        }

        private void PaintLandingTelegraph()
        {
            // 도약자가 판에서 사라졌으면(강하 전 소멸 — 궁극기는 무적이라 드물다) 예고를 남기지 않는다.
            if (!_landingLeaper.IsNone && !_driver.IsAlive(_landingLeaper)) _landingLeaper = SimEntityId.None;
            if (_landingLeaper.IsNone)
            {
                if (_landingRing != null && _landingRing.enabled) _landingRing.enabled = false;
                if (_landingFill != null && _landingFill.enabled) _landingFill.enabled = false;
                return;
            }
            if (_landingRing == null)
                _landingRing = CreateLine("LandingTelegraphRing", _ringWidth, BoardSortOrder.RangeRingOrder, _landingColor);
            var line = _landingColor; line.a = 1f;   // 선은 불투명 — 알파는 채움의 몫(옛 `:699-703`)
            BuildRing(_landingPoints, _landingCenter, _landingRadius);
            _landingRing.positionCount = _landingPoints.Count;
            for (int i = 0; i < _landingPoints.Count; i++) _landingRing.SetPosition(i, _landingPoints[i]);
            _landingRing.startColor = _landingRing.endColor = line;
            _landingRing.enabled = true;

            // T17 — 채움을 못 그리면 **한 번은 시끄럽게** 알린다(예고가 안 보이면 회피 불가 = 불공정). 선은 계속 그린다.
            if (!EnsureDiscFill("LandingTelegraphFill", ref _landingFill, ref _landingFillMesh))
            {
                if (!_landingFillWarned)
                {
                    _landingFillWarned = true;
                    Debug.LogWarning("[CoreMapOverlay] 착지 예고 채움 머티리얼을 만들 수 없다(RuntimeMaterials 미배선) — "
                                     + "링 선만 그린다. 예고가 옅으면 회피가 어렵다.", this);
                }
                return;
            }
            FillDisc(_landingFill, _landingFillMesh, _landingCenter, _landingPoints, _landingColor);
        }

        // 원 둘레 점(view, 보드 평면 + 띄움). 링 셋(배치·카드·착지 예고)이 **같은 사상**으로 짓는다.
        private void BuildRing(List<Vector3> points, float3 centerSim, float radiusTiles)
        {
            float ts = _driver.TileSize;
            Vector3 lift = SurfaceLift();
            points.Clear();
            for (int i = 0; i <= _ringSegments; i++)
            {
                float a = i / (float)_ringSegments * math.PI * 2f;
                var p = new float3(centerSim.x + math.cos(a) * radiusTiles * ts, 0f,
                                   centerSim.z + math.sin(a) * radiusTiles * ts);
                points.Add((Vector3)BoardSpace.ToView(p) + lift);
            }
        }

        // 원 안 채움 = 같은 둘레의 부채 메시(선과 채움이 **정의상 같은 곡선**이다 — 칸 계단이 원을 사각형처럼 보이게 하지 않는다).
        private void FillDisc(MeshRenderer fill, Mesh mesh, float3 centerSim, List<Vector3> rim, Color color)
        {
            var center = (Vector3)BoardSpace.ToView(new float3(centerSim.x, 0f, centerSim.z)) + SurfaceLift();
            var verts = new Vector3[rim.Count + 1];
            verts[0] = center;
            for (int i = 0; i < rim.Count; i++) verts[i + 1] = rim[i];
            var tris = new int[(rim.Count - 1) * 3];
            for (int i = 0; i < rim.Count - 1; i++)
            {
                tris[i * 3] = 0; tris[i * 3 + 1] = i + 2; tris[i * 3 + 2] = i + 1;
            }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            Wassup.Rendering.RuntimeMaterialFactory.ApplyColor(fill.sharedMaterial, color);
            fill.enabled = true;
        }

        private bool EnsureDiscFill(string n, ref MeshRenderer fill, ref Mesh mesh)
        {
            if (fill != null) return true;
            var mat = Wassup.Rendering.RuntimeMaterialFactory.CreateTransparent(Color.white);
            if (mat == null) return false;
            var go = new GameObject($"{name}_{n}");
            go.transform.SetParent(transform, false);
            mesh = new Mesh { name = n };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            fill = go.AddComponent<MeshRenderer>();
            fill.sharedMaterial = mat;
            fill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fill.receiveShadows = false;
            fill.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            fill.sortingOrder = BoardSortOrder.RangeRingOrder - 1;
            fill.enabled = false;
            return true;
        }

        private void ClearArea()
        {
            _area = AreaKind.None;
            _areaHost = SimEntityId.None;
            _telegraphId = SimEntityId.None;
            _aimCellList.Clear();
        }

        private void PaintCardArea()
        {
            bool ring = false, cells = false;
            if (!_hasDrag)
            {
                switch (_area)
                {
                    case AreaKind.Attach:
                    {
                        var u = _driver.Find(_areaHost);
                        // 생존 술어 = 판 위에 있다(사망 모션 중 시체 위에 링을 남기지 않는다 — 옛 `CanDrawAttachPreviewFor`).
                        if (u == null || u.Dead) { ClearArea(); break; }
                        _areaCenter = u.Position;
                        _areaRadius = _areaSpec.RadiusWithOrigin(u.HitRadius);
                        ring = _areaRadius > 0f;
                        break;
                    }
                    case AreaKind.AimRing: ring = true; break;
                    case AreaKind.Telegraph: ring = true; break;
                    case AreaKind.AimCells: cells = _aimCellList.Count > 0; break;
                }
            }
            if (ring) DrawAreaRing(); else HideAreaRing();
            if (cells && _style != null)
            {
                int used = 0;
                for (int i = 0; i < _aimCellList.Count; i++)
                {
                    var sr = Rent(_aimCells, used++, BoardSortOrder.PlacementHighlightOrder);
                    Tint(sr, _style.rangeColor);
                    sr.transform.position = ViewOf(CellCenterSim(_aimCellList[i]));
                    sr.transform.rotation = PlaneRotation();
                    sr.transform.localScale = Vector3.one * _driver.TileSize;
                }
                SetCount(_aimCells, used);
            }
            else SetCount(_aimCells, 0);
        }

        private void DrawAreaRing()
        {
            if (!EnsureAreaRenderers()) return;
            float ts = _driver.TileSize;
            Vector3 lift = SurfaceLift();
            _areaPoints.Clear();
            for (int i = 0; i <= _ringSegments; i++)
            {
                float a = i / (float)_ringSegments * math.PI * 2f;
                var p = new float3(_areaCenter.x + math.cos(a) * _areaRadius * ts, 0f,
                                   _areaCenter.z + math.sin(a) * _areaRadius * ts);
                _areaPoints.Add((Vector3)BoardSpace.ToView(p) + lift);
            }
            _areaRing.positionCount = _areaPoints.Count;
            for (int i = 0; i < _areaPoints.Count; i++) _areaRing.SetPosition(i, _areaPoints[i]);
            var line = _areaStyle.color; line.a = _areaStyle.lineAlpha;
            _areaRing.startColor = _areaRing.endColor = line;
            _areaRing.enabled = true;

            // 채움 = 같은 원의 부채 메시(작은 반경에선 채움이 주신호다 — 옛 D1).
            var center = (Vector3)BoardSpace.ToView(new float3(_areaCenter.x, 0f, _areaCenter.z)) + lift;
            var verts = new Vector3[_areaPoints.Count + 1];
            verts[0] = center;
            for (int i = 0; i < _areaPoints.Count; i++) verts[i + 1] = _areaPoints[i];
            var tris = new int[(_areaPoints.Count - 1) * 3];
            for (int i = 0; i < _areaPoints.Count - 1; i++)
            {
                tris[i * 3] = 0; tris[i * 3 + 1] = i + 2; tris[i * 3 + 2] = i + 1;
            }
            _areaFillMesh.Clear();
            _areaFillMesh.vertices = verts;
            _areaFillMesh.triangles = tris;
            _areaFillMesh.RecalculateBounds();
            var fill = _areaStyle.color; fill.a = _areaStyle.fillAlpha;
            Wassup.Rendering.RuntimeMaterialFactory.ApplyColor(_areaFill.sharedMaterial, fill);
            _areaFill.enabled = true;
        }

        private void HideAreaRing()
        {
            if (_areaRing != null && _areaRing.enabled) _areaRing.enabled = false;
            if (_areaFill != null && _areaFill.enabled) _areaFill.enabled = false;
        }

        private bool EnsureAreaRenderers()
        {
            if (_areaRing == null) _areaRing = CreateLine("CardAreaRing", _ringWidth, BoardSortOrder.RangeRingOrder, _ringColor);
            if (_areaFill != null) return true;
            var mat = Wassup.Rendering.RuntimeMaterialFactory.CreateTransparent(_ringColor);
            if (mat == null) return false;   // 링만 그린다(머티리얼 미배선 — 도형 가이드와 같은 규약)
            var go = new GameObject($"{name}_CardAreaFill");
            go.transform.SetParent(transform, false);
            _areaFillMesh = new Mesh { name = "CardAreaFill" };
            go.AddComponent<MeshFilter>().sharedMesh = _areaFillMesh;
            _areaFill = go.AddComponent<MeshRenderer>();
            _areaFill.sharedMaterial = mat;
            _areaFill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _areaFill.receiveShadows = false;
            _areaFill.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _areaFill.sortingOrder = BoardSortOrder.RangeRingOrder - 1;
            _areaFill.enabled = false;
            return true;
        }

        private void LateUpdate()
        {
            if (_driver == null || !_driver.Running || !BoardSpace.IsConfigured) return;

            if (_showGrid && !_gridBuilt) BuildGrid();
            if (_grid != null) _grid.enabled = _showGrid;
            PaintEffectTilesOnce();    // 판마다 한 번 — 드래그와 무관하게 늘 보인다
            PaintCardArea();
            PaintLandingTelegraph();   // 전용 채널 — 드래그에 양보하지 않는다(T16)

            if (!_hasDrag || _dragDefIndex < 0)
            {
                SetCount(_guideCells, 0);
                _guideUsed = 0;
                SetCount(_ghostCells, 0);
                SetCount(_marks, 0);
                if (_ring != null) _ring.enabled = false;
                HideShapeGuide();
                _guideDefIndex = -1;
                ClearRangeCells();
                return;
            }

            PaintGuide();
            PaintGhost();
            PaintRange();
        }

        // ── ② 배치 가이드 — **못 놓는 칸**(사용자 결정 2026-09-23) ──────────────
        //
        // 칠하는 것은 「놓을 수 없는 칸」이고, 그 이유를 **두 색으로 가른다**:
        //   · 지형·프랍이 막았다 → `blockedColor`  — 내가 어떻게 할 수 없는 칸
        //   · 유닛이 서 있다   → `occupiedColor` — 치우거나 기다리면 열리는 칸
        // 플레이어가 배우는 것이 다르기 때문에 색이 다르다. 놓을 수 있는 칸은 **안 칠한다** —
        // 빈 땅이 곧 「여기 된다」이고, 그래야 화면에서 움직이지 않는 면이 답이 된다.
        //
        // ⚠ 묻는 것은 **칸의 상태**(`CellStateAt`)이지 「이 유닛을 여기 둘 수 있나」가 아니다.
        // 후자로 물으면 답이 **끌고 있는 유닛의 footprint 만큼 부푼다**(민코프스키 합) —
        // 2×2 가 선 자리에 2×2 를 끌면 점유가 3×3 으로 보이고, 화면이 「이미 배치된 유닛의
        // 타일이 원래보다 크다」고 거짓말한다(사용자 플레이 2차). 자원·쿨·상한이 섞이지
        // 않는 것도 같은 이유로 공짜다 — 칸의 상태는 그것들을 모른다.
        // 그 답은 **여전히 코어의 것**이다 — 뷰는 상태를 읽기만 하고 판정하지 않는다.
        // 「그 유닛을 여기 놓을 수 있나」는 고스트가 `Judge` 로 따로 묻는다.
        private void PaintGuide()
        {
            var sprite = GuideSprite();
            if (sprite == null) { SetCount(_guideCells, 0); _guideUsed = 0; return; }

            if (_guideDefIndex != _dragDefIndex || Time.unscaledTime >= _nextGuideRepaint)
            {
                _guideDefIndex = _dragDefIndex;
                _nextGuideRepaint = Time.unscaledTime + _guideRepaintSeconds;
                RebuildGuideCells(sprite);
            }

            // 색·알파는 **매 프레임** 민다 — 페이드인이 돌아야 하고(정적이지만 등장은 페이드),
            // Play 중 저작 튜닝도 그대로 보여야 한다.
            ApplyGuideTint();
        }

        private void RebuildGuideCells(Sprite sprite)
        {
            var placement = _driver.Match.Placement;
            var size = _driver.GridSize;

            _guideOccupied.Clear();
            int used = 0;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                var state = placement.CellStateAt(new int2(x, y));
                if (state == PlacementService.CellState.Free) continue;   // 빈 땅이 곧 「여기 된다」
                bool occupied = state == PlacementService.CellState.Occupied;

                var sr = Rent(_guideCells, used++, BoardSortOrder.PlacementHighlightOrder);
                sr.sprite = sprite;
                sr.transform.position = ViewOf(CellCenterSim(new int2(x, y)));
                sr.transform.rotation = PlaneRotation();
                sr.transform.localScale = Vector3.one * _driver.TileSize;
                _guideOccupied.Add(occupied);
            }
            SetCount(_guideCells, used);
            _guideUsed = used;
        }

        // 최종 색 = 저작 틴트 × 페이드. 스프라이트는 흰색 solid 라 색은 저작에서만 온다.
        private void ApplyGuideTint()
        {
            if (_guideUsed <= 0) return;
            float fade = _style.placeableFadeInDuration > 0f && _guideShownAt >= 0f
                ? Mathf.Clamp01((Time.unscaledTime - _guideShownAt) / _style.placeableFadeInDuration)
                : 1f;
            var blocked = Multiply(_style.blockedColor, fade);
            var occupied = Multiply(_style.occupiedColor, fade);
            for (int i = 0; i < _guideUsed && i < _guideCells.Count; i++)
                Tint(_guideCells[i], i < _guideOccupied.Count && _guideOccupied[i] ? occupied : blocked);
        }

        private static Color Multiply(Color authored, float fade)
            => new Color(authored.r, authored.g, authored.b, authored.a * fade);

        // 저작이 없으면 **안 그린다.** 폴백으로 절차적 사각을 깔면 그게 다음 사람의 정본이 된다.
        private Sprite GuideSprite() => _style != null ? _style.blockedSprite : null;

        // ── ③ 고스트 ─────────────────────────────────────────────────────────
        private void PaintGhost()
        {
            var def = _driver.Definition;
            if (_dragDefIndex >= def.Units.Length) { SetCount(_ghostCells, 0); return; }
            int w = math.max(1, def.Units[_dragDefIndex].FootprintWidth);
            int h = math.max(1, def.Units[_dragDefIndex].FootprintHeight);
            int used = PaintFootprint(_ghostCells, 0, _dragAnchor, w, h,
                                      _dragValid ? _ghostOkColor : _ghostBadColor);
            SetCount(_ghostCells, used);
        }

        private int PaintFootprint(List<SpriteRenderer> pool, int at, int2 anchor, int w, int h, Color color)
        {
            for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                var sr = Rent(pool, at++, BoardSortOrder.DragPreviewOrder);
                Tint(sr, color);
                sr.transform.position = ViewOf(CellCenterSim(new int2(anchor.x + dx, anchor.y + dy)));
                sr.transform.rotation = PlaneRotation();
                sr.transform.localScale = Vector3.one * _driver.TileSize;
            }
            return at;
        }

        // ── ④ 사거리 링 + 사정권 표식 ────────────────────────────────────────
        private void PaintRange()
        {
            var def = _driver.Definition;
            if (_dragDefIndex >= def.Units.Length) { HideShapeGuide(); return; }
            ref var unit = ref def.Units[_dragDefIndex];

            int w = math.max(1, unit.FootprintWidth);
            // 원점은 **발밑**이다 — 사거리 원점·몸 원·자기중심 폭심이 전부 이 점이다
            // (베이스 통일). 앵커의 기하 중심이 아니다.
            float ts = _driver.TileSize;
            var foot = new float3((_dragAnchor.x + (w - 1) * 0.5f) * ts, 0f, _dragAnchor.y * ts);

            // 반지름은 **판정이 아니라 치수**다: 판정이 `d ≤ range + selfBody + targetBody`
            // 이므로 그 선은 `range + selfBody` 에 그린다(상대 몸은 상대마다 다르다).
            float radiusTiles = unit.AttackRange + unit.BodyRadiusTiles;
            if (radiusTiles <= 0f)
            {
                if (_ring != null) _ring.enabled = false;
                SetCount(_marks, 0);
                HideShapeGuide();
                ClearRangeCells();
                return;
            }

            EnsureRing();
            _ring.enabled = true;
            _ringPoints.Clear();
            Vector3 lift = SurfaceLift();
            for (int i = 0; i <= _ringSegments; i++)
            {
                float a = i / (float)_ringSegments * math.PI * 2f;
                var p = new float3(foot.x + math.cos(a) * radiusTiles * ts, 0f,
                                   foot.z + math.sin(a) * radiusTiles * ts);
                _ringPoints.Add((Vector3)BoardSpace.ToView(p) + lift);
            }
            _ring.positionCount = _ringPoints.Count;
            for (int i = 0; i < _ringPoints.Count; i++) _ring.SetPosition(i, _ringPoints[i]);
            PaintRangeFill(in unit, foot, w);

            // 「이놈이 맞는다」 표식. **판정은 `AttackReach.InReach` 하나**다 — 여기서
            // 거리를 다시 재면 제약 13 위반이고, 그 어긋남은 리터럴도 심볼도 아니라
            // grep 이 못 잡는다.
            //
            // ⑤ 가이드의 «지금 물 적»도 **이 후보 집합**에서 고른다(옛 `BattleBridge.cs:8141-8174` —
            // 마크와 가이드가 한 루프였다). 순위 = 최근접, 동거리는 낮은 `SimId` — 코어
            // `NearestTargeting.RanksBefore`(= 옛 `NearestTargeting.RanksBefore`, 옛 `:8172`)를 **부르기만** 한다.
            // 거리는 발밑→대상 XZ 제곱(옛 `AttackSystem.DistanceSqToTarget`, `:8166`). 배치 전이라 락·도발·
            // 우선 클래스·최전방은 없다(옛 unit 6 규칙).
            //
            // **후보 자격은 옛 `BattleBridge.cs:8141-8158` 그대로**이고, 전부 코어의 기존 진입점을 **부르기만** 한다
            // (5b 가 「적 진영 + 생존」으로 좁혀 옮긴 드리프트를 6c 후속에서 되돌렸다):
            //   · 마스크 = `TargetDefaults.ResolveDefender`(옛 `DefenderTargetDefaults.Resolve`, `:8138`) — 적 거점도 후보다
            //   · 통행 층 = `LayerBits.CanTarget`(옛 `:8149`) — 지상 전용 근접은 비행 적을 못 본다. 대상 층은 코어 후보
            //     스냅샷과 같은 읽기(`CombatPhase.BuildCandidates` — 이동 상태가 없으면 0 = 무필터)
            //   · 제외 = `Unit.IsTargetable`(옛 `:8151` 도약 이탈 제외의 후계 — 코어 후보 스냅샷과 같은 술어)
            //   · 지원형(아군 마스크 — 힐러)은 마크도 가이드도 없다(옛 `:8062` `!unit.targetAllies`).
            int mask = TargetDefaults.ResolveDefender(unit.TargetFactions);
            byte atkLayers = (byte)unit.Attack.TargetLayers;
            bool attacksFoes = (mask & Factions.AnyEnemy) != 0;
            // 마크는 배치가 **유효할 때만** 보인다 — 무효일 땐 고스트의 빨강과 시간으로 갈린다(옛
            // `TilemapMapView.ApplyTargetMarkVisibility` `:811-816`). 가이드는 그 스위치를 안 탄다(옛 것도 그랬다).
            bool showMarks = _dragValid && _style != null;
            bool guideHas = false;
            var guideBest = default(NearestTargeting.Candidate);
            float3 guidePos = default;
            int used = 0;
            var units = _driver.Units;
            for (int i = 0; attacksFoes && i < units.Count; i++)
            {
                var u = units[i];
                if (!u.IsTargetable()) continue;
                if (((int)u.Faction & mask) == 0) continue;
                if (!LayerBits.CanTarget(atkLayers, u.Move != null ? u.Move.TraversalLayers : LayerBits.None)) continue;
                if (!AttackReach.InReach(foot, u.Position, unit.AttackRange, ts,
                                         unit.BodyRadiusTiles, u.HitRadius)) continue;

                if (showMarks)
                {
                    var sr = Rent(_marks, used++, BoardSortOrder.RangeTargetMarkOrder);
                    sr.sprite = MarkSprite();
                    Tint(sr, _style.rangeTargetMarkColor);   // 옛 `TilemapMapView.cs:766` — 코드 색 리터럴 없음(제약 6)
                    sr.transform.position = ViewOf(u.Position);
                    sr.transform.rotation = PlaneRotation();
                    sr.transform.localScale = Vector3.one * (ts * 0.7f);
                }

                float dx = u.Position.x - foot.x, dz = u.Position.z - foot.z;
                var cand = new NearestTargeting.Candidate { SqDist = dx * dx + dz * dz, SimId = u.Id.Value };
                if (!guideHas || NearestTargeting.RanksBefore(in cand, in guideBest))
                { guideBest = cand; guidePos = u.Position; guideHas = true; }
            }
            SetCount(_marks, used);

            if (guideHas) PaintShapeGuide(in unit.Attack, foot, guidePos, radiusTiles);
            else HideShapeGuide();   // 사거리 안 적이 없으면 없다 — 기본 방향은 없다(「방향은 타겟이 정한다」)
        }

        // ── ④′ 사거리 칸 채움(unit 8a2 행 8 — T3·T13) ────────────────────────────────
        //
        // 옛 `TilemapMapView.SetPlacementRange`(`:1226-1277`) + `RangeFillAlpha`(`:1185`) + `ApplyRingTint`(`:1140-1175`)의 후계.
        // **링과 채움을 한 곳이 그린다**(T13 — 옛 뷰가 칸 채움과 링 셰이더 내부 채움을 따로 칠하다 「채움이 두 겹」이 됐다).
        //   · 칸 집합 = 판정과 **같은 본체**(`AttackReach.InReach`) · 대상 = 표준 잡몹 몸(`CoreDrawRadius.StandardTargetBodyTiles` — T3:
        //     칸은 크기를 표현 못 해 링보다 최대 0.25칸 바깥까지 들어가는 것을 **감수한다**) · 원점 = 발밑 · 앵커 칸 자신은 빼다
        //     (옛 `includeCenter = false`). 앵커·유닛이 바뀔 때만 다시 센다(옛: 셀 변경 시에만 페인트).
        //   · 칸의 채움 알파 = **링이 있으면 0**(옛 `RangeFillAlpha` — 칸 계단이 원을 사각형처럼 보이게 한다). 배치 사거리에는 링이
        //     언제나 있으므로 칸을 그릴 렌더러를 두지 않고 **논리 집합만** 든다(투명한 칸 = 안 그린 칸).
        //   · 대신 **링 안을 채운다**(옛 링 셰이더 `_FillAlpha = rangeFillAlphaUnderRing`) — 선과 채움이 정의상 같은 곡선이다.
        //     채움 색 = 링 선의 색상(옛 사용자 조건 2 「선과 채움은 같은 색」 — 선 색이 새 오버레이 저작 `_ringColor` 이므로 그 RGB),
        //     알파 = 타일셋 `rangeFillAlphaUnderRing`.
        private void PaintRangeFill(in UnitDef unit, float3 foot, int footprintWidth)
        {
            if (_rangeCellsDef != _dragDefIndex || !_rangeCellsAnchor.Equals(_dragAnchor))
            {
                _rangeCellsDef = _dragDefIndex;
                _rangeCellsAnchor = _dragAnchor;
                _rangeCells.Clear();
                float ts = _driver.TileSize;
                var size = _driver.GridSize;
                float offX = math.abs((footprintWidth - 1) * 0.5f);
                int scan = (int)math.ceil(unit.AttackRange + unit.BodyRadiusTiles
                                          + CoreDrawRadius.StandardTargetBodyTiles + offX) + 1;
                for (int dx = -scan; dx <= scan; dx++)
                for (int dz = -scan; dz <= scan; dz++)
                {
                    if (dx == 0 && dz == 0) continue;   // 앵커 칸(유닛 자리)은 비운다
                    var cell = new int2(_dragAnchor.x + dx, _dragAnchor.y + dz);
                    if (cell.x < 0 || cell.y < 0 || cell.x >= size.x || cell.y >= size.y) continue;
                    if (!AttackReach.InReach(foot, CellCenterSim(cell), unit.AttackRange, ts,
                                             unit.BodyRadiusTiles, CoreDrawRadius.StandardTargetBodyTiles)) continue;
                    _rangeCells.Add(cell);
                }
            }

            if (_style == null || !EnsureDiscFill("RangeFill", ref _rangeFill, ref _rangeFillMesh)) return;
            var c = _ringColor;
            c.a = _style.rangeFillAlphaUnderRing;
            FillDisc(_rangeFill, _rangeFillMesh, foot, _ringPoints, c);
        }

        // ── ⑤ 공격 도형 가이드 ────────────────────────────────────────────────
        //
        // 옛 `TilemapMapView.SetShapeGuide`(`:878-925`) + 호출부 `BattleBridge.cs:8176-8183` 의 이식.
        // 치수는 **전부 정의표에서** 온다 — 뷰는 도달을 다시 재지 않는다:
        //   · 원점 = 발밑(`foot` — 링과 같은 점, 옛 `:8178` 의 `center + markBase`)
        //   · 길이/반경 = `range + 내 몸`(링과 같은 값, 옛 `:8180` `attackRange + BodyRadiusTiles`).
        //     대상의 몸은 **그리지 않는다**(제약 13 — 상대마다 다르다 · directional-attack-shape/7:52)
        //   · 각 = bake 역산(`ShapeMarkSpec.AngleDegOf`, 옛 `:8180`) · 반폭 = bake `halfWidth`, 테 폭 하한
        //     (옛 `TilemapMapView.cs:900` 의 `Max(halfWidth, 테 폭)`) — 참격 자국과 **같은 역산**(`CoreVfxSpawner.ShapeMarkOf`)
        //   · 방향 = 발밑 → 대상(옛 `:8179`). 같은 자리(방향 0)면 숨긴다(옛 `:884`)
        //   · Omni 는 없다(옛 `:8176` `!IsOmni`) — 원 링이 전부다
        // 색 = 마크 색(`BoardOverlayStyle.rangeTargetMarkColor`, 옛 `:919`) × 저작 알파 둘 · 정렬 = 링·타일 위, 마크 아래
        // (`PlacementShapeGuideOrder`, 옛 `:978`).
        private void PaintShapeGuide(in AttackDef attack, float3 foot, float3 targetPos, float radiusTiles)
        {
            if (attack.ShapeKind == Wassup.BattleCore.Combat.AttackShapeBaked.OmniKind || _style == null) { HideShapeGuide(); return; }

            Vector3 originView = (Vector3)BoardSpace.ToView(foot);
            Vector3 dirView = (Vector3)BoardSpace.ToView(targetPos) - originView;
            if (dirView.sqrMagnitude < 1e-6f) { HideShapeGuide(); return; }

            // 메시는 **뷰 단위**로 굽는다. 타일 한 칸이 뷰에서 얼마인지는 링이 쓰는 같은 사상(`ToView`)으로
            // 잰다 — 그래야 바깥 호가 링 원과 정확히 겹친다(옛 unit 6 「바깥 호 = 링」).
            float ts = _driver.TileSize;
            float viewTile = ((Vector3)BoardSpace.ToView(foot + new float3(ts, 0f, 0f)) - originView).magnitude;
            var baked = new Wassup.BattleCore.Combat.AttackShapeBaked
            {
                kind = (byte)attack.ShapeKind, sinHalf = attack.ShapeSinHalf,
                cosHalf = attack.ShapeCosHalf, halfWidth = attack.ShapeHalfWidth,
            };
            var spec = CoreVfxSpawner.ShapeMarkOf(in baked, radiusTiles, viewTile);
            bool band = spec.kind == Wassup.Data.AttackShapeBaked.BandKind;
            if (!band && (spec.angleDeg <= 0f || spec.angleDeg >= 360f)) { HideShapeGuide(); return; }   // 옛 `:882`

            if (!EnsureShapeGuide()) return;
            if (!_shapeKeyValid || !spec.Equals(_shapeKey))
            {
                float r = spec.lengthTiles * spec.cellSize;
                float rim = ShapeMeshBuilder.DefaultRimWidthTiles * spec.cellSize;
                if (band)
                {
                    float hw = spec.halfWidthTiles * spec.cellSize;   // 테 폭 하한은 `FromBaked` 가 이미 걸었다
                    ShapeMeshBuilder.BuildBand(_shapeFillMesh, hw, r);
                    ShapeMeshBuilder.BuildBandOutline(_shapeRimMesh, hw, r, rim);
                }
                else
                {
                    ShapeMeshBuilder.BuildFan(_shapeFillMesh, spec.angleDeg, r);
                    ShapeMeshBuilder.BuildFanOutline(_shapeRimMesh, spec.angleDeg, r, rim);
                }
                _shapeKey = spec;
                _shapeKeyValid = true;
            }

            // 메시 관습: XY 평면 · +Y = 찌르는 방향. 로컬 +Z 를 보드 법선에, +Y 를 대상 방향에 맞춘다.
            var rot = Quaternion.LookRotation(BoardSpace.RaycastPlane().normal, dirView);
            var pos = originView + SurfaceLift();
            _shapeFill.transform.SetPositionAndRotation(pos, rot);
            _shapeRim.transform.SetPositionAndRotation(pos, rot);

            var c = _style.rangeTargetMarkColor;
            c.a = _style.rangeShapeGuideFillAlpha;
            Wassup.Rendering.RuntimeMaterialFactory.ApplyColor(_shapeFill.sharedMaterial, c);
            c.a = _style.rangeShapeGuideRimAlpha;
            Wassup.Rendering.RuntimeMaterialFactory.ApplyColor(_shapeRim.sharedMaterial, c);
            if (!_shapeFill.enabled) _shapeFill.enabled = true;
            if (!_shapeRim.enabled) _shapeRim.enabled = true;
        }

        private void HideShapeGuide()
        {
            if (_shapeFill != null && _shapeFill.enabled) _shapeFill.enabled = false;
            if (_shapeRim != null && _shapeRim.enabled) _shapeRim.enabled = false;
        }

        /// <summary>테스트 창구 — 지금 켜진 사정권 표식 수.</summary>
        public int ActiveMarkCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _marks.Count; i++) if (_marks[i] != null && _marks[i].enabled) n++;
                return n;
            }
        }

        /// <summary>테스트 창구 — 켜진 표식 하나의 뷰 위치와 색(프로퍼티 블록에 민 값 그대로).</summary>
        public bool TryGetMark(int index, out Vector3 viewPos, out Color color)
        {
            viewPos = default; color = default;
            int n = 0;
            for (int i = 0; i < _marks.Count; i++)
            {
                var sr = _marks[i];
                if (sr == null || !sr.enabled) continue;
                if (n++ != index) continue;
                viewPos = sr.transform.position;
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                sr.GetPropertyBlock(_mpb);
                color = _mpb.GetColor(CoreOverlayMaterial.BaseColorId);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 테스트 창구 — 지금 도형 가이드가 떠 있나, 떠 있으면 그 도형(종류·각·반폭·길이 칸). 그리는 쪽이 쓰는
        /// 값 그대로다(메시 정점을 역산하지 않는다).
        /// </summary>
        public bool TryGetShapeGuide(out ShapeMarkSpec spec, out Vector3 originView, out Vector3 dirView)
        {
            spec = _shapeKey; originView = default; dirView = default;
            if (_shapeFill == null || !_shapeFill.enabled || !_shapeKeyValid) return false;
            originView = _shapeFill.transform.position;
            dirView = _shapeFill.transform.up;
            return true;
        }

        // 머티리얼은 `RuntimeMaterialFactory`(always-included) 에서 파생한다 — `Shader.Find` 금지. 채움·테의 알파가
        // 달라 한 장을 공유하지 않는다(옛 `EnsureShapeGuide` 와 같다). 실패는 1회 경고 뒤 조용히 빠진다.
        private bool EnsureShapeGuide()
        {
            if (_shapeFill != null) return true;
            if (_shapeMatMissing) return false;
            var c = _style.rangeTargetMarkColor;
            var fillMat = Wassup.Rendering.RuntimeMaterialFactory.CreateTransparent(c);
            var rimMat = Wassup.Rendering.RuntimeMaterialFactory.CreateTransparent(c);
            if (fillMat == null || rimMat == null)
            {
                _shapeMatMissing = true;
                if (fillMat != null) Destroy(fillMat);
                if (rimMat != null) Destroy(rimMat);
                Debug.LogWarning("[CoreMapOverlay] 공격 도형 가이드 머티리얼을 만들 수 없다(RuntimeMaterials 미배선) — 가이드 생략.", this);
                return false;
            }
            _shapeFill = MakeShapeRenderer("ShapeGuideFill", fillMat, out _shapeFillMesh);
            _shapeRim = MakeShapeRenderer("ShapeGuideRim", rimMat, out _shapeRimMesh);
            return true;
        }

        private MeshRenderer MakeShapeRenderer(string n, Material mat, out Mesh mesh)
        {
            var go = new GameObject($"{name}_{n}");
            go.transform.SetParent(transform, false);
            mesh = new Mesh { name = n };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.sortingOrder = BoardSortOrder.PlacementShapeGuideOrder;
            mr.enabled = false;
            return mr;
        }

        // ── ① 격자 ───────────────────────────────────────────────────────────
        //
        // 선 하나로 그린다(세로줄 → 가로줄을 이어 붙인 지그재그). 줄마다 렌더러를 만들면
        // 20×12 맵에서 34개가 되고, 그만큼 정렬 대상이 늘어 다음 사람이 「오버레이가 무겁다」를
        // 만나게 된다.
        private void BuildGrid()
        {
            var size = _driver.GridSize;
            if (size.x <= 0 || size.y <= 0) return;
            _gridBuilt = true;

            EnsureGridLine();
            float ts = _driver.TileSize;
            Vector3 lift = SurfaceLift();
            var pts = new List<Vector3>((size.x + size.y + 2) * 2);

            // 칸 **경계**는 셀 중심에서 반 칸 밖이다(셀 N 의 중심이 정수 N).
            float x0 = -0.5f * ts, x1 = (size.x - 0.5f) * ts;
            float z0 = -0.5f * ts, z1 = (size.y - 0.5f) * ts;

            for (int x = 0; x <= size.x; x++)
            {
                float wx = (x - 0.5f) * ts;
                bool up = (x & 1) == 0;
                pts.Add((Vector3)BoardSpace.ToView(new float3(wx, 0f, up ? z0 : z1)) + lift);
                pts.Add((Vector3)BoardSpace.ToView(new float3(wx, 0f, up ? z1 : z0)) + lift);
            }
            for (int y = 0; y <= size.y; y++)
            {
                float wz = (y - 0.5f) * ts;
                bool right = (y & 1) == 0;
                pts.Add((Vector3)BoardSpace.ToView(new float3(right ? x0 : x1, 0f, wz)) + lift);
                pts.Add((Vector3)BoardSpace.ToView(new float3(right ? x1 : x0, 0f, wz)) + lift);
            }

            _grid.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) _grid.SetPosition(i, pts[i]);
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

        private float3 CellCenterSim(int2 cell)
            => new float3(cell.x * _driver.TileSize, 0f, cell.y * _driver.TileSize);

        private Vector3 ViewOf(float3 sim) => (Vector3)BoardSpace.ToView(sim) + SurfaceLift();

        private Quaternion PlaneRotation() => Quaternion.LookRotation(BoardSpace.RaycastPlane().normal);

        // 보드 평면 법선을 카메라 쪽으로 정렬해 그만큼 띄운다(화면상 위치 불변, 깊이만 분리).
        private Vector3 SurfaceLift()
        {
            if (_surfaceOffset <= 0f) return Vector3.zero;
            Vector3 n = BoardSpace.RaycastPlane().normal;
            if (EnsureCamera() && Vector3.Dot(n, _camera.transform.forward) > 0f) n = -n;
            return n * _surfaceOffset;
        }

        private bool EnsureCamera()
        {
            if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            return _camera != null;
        }

        private SpriteRenderer Rent(List<SpriteRenderer> pool, int index, int order)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject($"{name}_cell_{pool.Count}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = CellSprite();
                sr.sharedMaterial = Material();
                sr.sortingOrder = order;
                pool.Add(sr);
            }
            var r = pool[index];
            r.sortingOrder = order;
            if (r.sprite == null) r.sprite = CellSprite();
            if (!r.enabled) r.enabled = true;
            return r;
        }

        // 스프라이트의 색은 **프로퍼티 블록**으로 민다(`CoreOverlayMaterial` 헤더 참조).
        private void Tint(SpriteRenderer sr, Color color)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            sr.GetPropertyBlock(_mpb);
            _mpb.SetColor(CoreOverlayMaterial.BaseColorId, color);
            sr.SetPropertyBlock(_mpb);
        }

        private static void SetCount(List<SpriteRenderer> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
                if (pool[i] != null && pool[i].enabled) pool[i].enabled = false;
        }

        private void EnsureGridLine()
        {
            if (_grid != null) return;
            _grid = CreateLine("Grid", _gridWidth, BoardSortOrder.AimArrowOrder, _gridColor);
        }

        private void EnsureRing()
        {
            if (_ring != null) return;
            _ring = CreateLine("RangeRing", _ringWidth, BoardSortOrder.RangeRingOrder, _ringColor);
        }

        private LineRenderer CreateLine(string n, float width, int order, Color color)
        {
            var go = new GameObject($"{name}_{n}");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            line.widthMultiplier = width;
            line.sortingOrder = order;
            line.sharedMaterial = Material();
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return line;
        }

        // 오버레이 전부가 **머티리얼 하나**를 공유한다(색은 정점색이 정한다).
        private Material Material()
            => _material != null ? _material : (_material = CoreOverlayMaterial.Create());

        // 칸 하나를 덮는 사각(가장자리만 살짝 눅인다 — 완전한 사각은 격자와 붙어 읽힌다).
        private Sprite CellSprite()
        {
            if (_cellSprite != null) return _cellSprite;
            _cellTex = BuildTex(32, (u, v) =>
            {
                float dx = Mathf.Abs(u - 0.5f) * 2f, dy = Mathf.Abs(v - 0.5f) * 2f;
                float d = Mathf.Max(dx, dy);
                return Mathf.Clamp01((0.94f - d) / 0.12f);
            });
            _cellSprite = Sprite.Create(_cellTex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
            return _cellSprite;
        }

        // 발밑 마크 — 가운데가 빈 고리.
        private Sprite MarkSprite()
        {
            if (_markSprite != null) return _markSprite;
            _markTex = BuildTex(48, (u, v) =>
            {
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                return Mathf.Exp(-Mathf.Pow((r - 0.72f) / 0.16f, 2f));
            });
            _markSprite = Sprite.Create(_markTex, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 48f);
            return _markSprite;
        }

        private static Texture2D BuildTex(int n, System.Func<float, float, float> alpha)
        {
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                float v = n > 1 ? y / (float)(n - 1) : 0.5f;
                for (int x = 0; x < n; x++)
                {
                    float u = n > 1 ? x / (float)(n - 1) : 0.5f;
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v)));
                }
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        private void OnDestroy()
        {
            if (_cellTex != null) Destroy(_cellTex);
            if (_markTex != null) Destroy(_markTex);
            if (_cellSprite != null) Destroy(_cellSprite);
            if (_markSprite != null) Destroy(_markSprite);
            if (_material != null) Destroy(_material);
            if (_shapeFill != null && _shapeFill.sharedMaterial != null) Destroy(_shapeFill.sharedMaterial);
            if (_shapeRim != null && _shapeRim.sharedMaterial != null) Destroy(_shapeRim.sharedMaterial);
            if (_shapeFillMesh != null) Destroy(_shapeFillMesh);
            if (_areaFill != null && _areaFill.sharedMaterial != null) Destroy(_areaFill.sharedMaterial);
            if (_areaFillMesh != null) Destroy(_areaFillMesh);
            if (_shapeRimMesh != null) Destroy(_shapeRimMesh);
            if (_landingFill != null && _landingFill.sharedMaterial != null) Destroy(_landingFill.sharedMaterial);
            if (_landingFillMesh != null) Destroy(_landingFillMesh);
            if (_rangeFill != null && _rangeFill.sharedMaterial != null) Destroy(_rangeFill.sharedMaterial);
            if (_rangeFillMesh != null) Destroy(_rangeFillMesh);
        }
    }
}
