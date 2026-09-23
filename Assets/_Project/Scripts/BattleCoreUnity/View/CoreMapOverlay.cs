using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Units;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.Core;
using Wassup.Presentation;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 5b — **판 위에 그리는 것.** 옛 `TilemapMapView`(1,608줄)에서
    // 오버레이 몫만 가져왔다(바닥 페인팅은 스테이지 프리팹이, 평면 선언은 `CoreBoardPlane` 이
    // 이미 소유한다 — 옛 뷰가 셋을 겸하던 것을 5a 에서 쪼갰다).
    //
    // 그리는 것 넷:
    //   ① 격자 — 「칸이 있다」를 말한다. 디오라마 바닥에는 칸 선이 없다.
    //   ② 배치 가이드 — 이 유닛을 **놓을 수 있는 칸**들.
    //   ③ 고스트 — 지금 손가락이 가리키는 footprint(초록/빨강).
    //   ④ 사거리 링 + 사정권 표식 — 「여기 놓으면 저기까지 닿는다」.
    //
    // ⚠ **판정을 한 줄도 갖지 않는다.**
    //   · 「놓을 수 있나」 = `PlacementService.Judge` 호출.
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
        [SerializeField] private Color _guideColor = new Color(0.45f, 0.92f, 0.5f, 0.16f);
        [SerializeField] private Color _ghostOkColor = new Color(0.45f, 0.92f, 0.5f, 0.55f);
        [SerializeField] private Color _ghostBadColor = new Color(0.95f, 0.32f, 0.3f, 0.5f);
        [Tooltip("가이드를 다시 칠하는 주기(초). 0 = 매 프레임.")]
        [SerializeField, Min(0f)] private float _guideRepaintSeconds = 0.15f;

        [Header("사거리")]
        [SerializeField] private Color _ringColor = new Color(0.55f, 0.95f, 1f, 0.85f);
        [SerializeField, Min(0.005f)] private float _ringWidth = 0.05f;
        [SerializeField, Min(8)] private int _ringSegments = 64;
        [SerializeField] private Color _markColor = new Color(1f, 0.72f, 0.25f, 0.8f);

        [Tooltip("보드 평면 법선(카메라 쪽) 띄움. 바닥과의 z-fighting 회피 전용.")]
        [SerializeField, Min(0f)] private float _surfaceOffset = 0.05f;

        private readonly List<SpriteRenderer> _guideCells = new List<SpriteRenderer>(64);
        private readonly List<SpriteRenderer> _ghostCells = new List<SpriteRenderer>(8);
        private readonly List<SpriteRenderer> _marks = new List<SpriteRenderer>(16);
        private readonly List<Vector3> _ringPoints = new List<Vector3>(80);

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

        // ── 배치 입력이 미는 것 ───────────────────────────────────────────────
        //
        // **단방향 push** 다(카메라 bounds 와 같은 규약). 오버레이가 입력에서 당겨오면
        // 「지금 뭘 끌고 있나」의 주인이 둘이 된다.
        private int _dragDefIndex = -1;
        private int2 _dragAnchor;
        private bool _dragValid;
        private bool _hasDrag;

        /// <summary>드래그 중인 유닛과 그 앵커를 알린다. 매 프레임 불러도 된다.</summary>
        public void ShowPlacement(int defIndex, int2 anchor, bool valid)
        {
            if (defIndex != _dragDefIndex) _nextGuideRepaint = 0f;   // 유닛이 바뀌면 가이드를 즉시 다시 칠한다
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
        }

        private void LateUpdate()
        {
            if (_driver == null || !_driver.Running || !BoardSpace.IsConfigured) return;

            if (_showGrid && !_gridBuilt) BuildGrid();
            if (_grid != null) _grid.enabled = _showGrid;

            if (!_hasDrag || _dragDefIndex < 0)
            {
                SetCount(_guideCells, 0);
                SetCount(_ghostCells, 0);
                SetCount(_marks, 0);
                if (_ring != null) _ring.enabled = false;
                _guideDefIndex = -1;
                return;
            }

            PaintGuide();
            PaintGhost();
            PaintRange();
        }

        // ── ② 배치 가이드 ────────────────────────────────────────────────────
        //
        // 「놓을 수 있는 칸」을 코어에 **칸마다 묻는다.** 비싸 보이지만 이것이 계약이다 —
        // 싸게 하려고 「지형만 보면 되겠지」로 줄이면 그 순간 화면과 판정이 갈린다.
        // 대신 **주기를 늦춘다**: 답이 바뀌는 사건(코스트 재생·쿨 만료)은 초 단위라
        // 매 프레임 물을 이유가 없다.
        private void PaintGuide()
        {
            if (_guideDefIndex == _dragDefIndex && Time.unscaledTime < _nextGuideRepaint) return;
            _guideDefIndex = _dragDefIndex;
            _nextGuideRepaint = Time.unscaledTime + _guideRepaintSeconds;

            var placement = _driver.Match.Placement;
            var size = _driver.GridSize;
            var def = _driver.Definition;
            if (_dragDefIndex >= def.Units.Length) return;

            int w = math.max(1, def.Units[_dragDefIndex].FootprintWidth);
            int h = math.max(1, def.Units[_dragDefIndex].FootprintHeight);

            int used = 0;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                var anchor = new int2(x, y);
                if (placement.Judge(_dragDefIndex, anchor) != RejectReason.None) continue;
                used = PaintFootprint(_guideCells, used, anchor, w, h, _guideColor);
            }
            SetCount(_guideCells, used);
        }

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
            if (_dragDefIndex >= def.Units.Length) return;
            ref var unit = ref def.Units[_dragDefIndex];

            int w = math.max(1, unit.FootprintWidth);
            // 원점은 **발밑**이다 — 사거리 원점·몸 원·자기중심 폭심이 전부 이 점이다
            // (베이스 통일). 앵커의 기하 중심이 아니다.
            float ts = _driver.TileSize;
            var foot = new float3((_dragAnchor.x + (w - 1) * 0.5f) * ts, 0f, _dragAnchor.y * ts);

            // 반지름은 **판정이 아니라 치수**다: 판정이 `d ≤ range + selfBody + targetBody`
            // 이므로 그 선은 `range + selfBody` 에 그린다(상대 몸은 상대마다 다르다).
            float radiusTiles = unit.AttackRange + unit.BodyRadiusTiles;
            if (radiusTiles <= 0f) { if (_ring != null) _ring.enabled = false; SetCount(_marks, 0); return; }

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

            // 「이놈이 맞는다」 표식. **판정은 `AttackReach.InReach` 하나**다 — 여기서
            // 거리를 다시 재면 제약 13 위반이고, 그 어긋남은 리터럴도 심볼도 아니라
            // grep 이 못 잡는다.
            int used = 0;
            var units = _driver.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || u.Faction != Faction.EnemyUnit) continue;
                if (!AttackReach.InReach(foot, u.Position, unit.AttackRange, ts,
                                         unit.BodyRadiusTiles, u.HitRadius)) continue;

                var sr = Rent(_marks, used++, BoardSortOrder.RangeTargetMarkOrder);
                sr.sprite = MarkSprite();
                Tint(sr, _markColor);
                sr.transform.position = ViewOf(u.Position);
                sr.transform.rotation = PlaneRotation();
                sr.transform.localScale = Vector3.one * (ts * 0.7f);
            }
            SetCount(_marks, used);
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
        }
    }
}
