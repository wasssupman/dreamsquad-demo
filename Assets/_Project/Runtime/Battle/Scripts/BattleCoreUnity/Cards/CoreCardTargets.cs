using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCoreUnity.View;
using Somnia.Battle.Core;
using Somnia.Battle.Presentation;

namespace Somnia.Battle.BattleCoreUnity.Cards
{
    // battle-core-rebuild unit 7c — **손끝 → 대상** 기하(← 옛 브리지의 화면 조회 몫: `EnumerateDefenderScreenRects` ·
    // `TryPickDefenderAtScreen` · `ScreenDistanceToRect` · `TryPickNearestEnemy` · `TryScreenToCellStrict` ·
    // `TryGetTileScreenCenter` · `TryGetUnitScreenRect` · `TryGetUnitViewAnchor`). 브리지가 사라지므로 **그 질문을 가진 쪽**
    // (손패 입력)이 유닛 뷰 풀과 코어 읽기 모델을 직접 본다.
    //
    // ⚠ **판정 0.** 여기서 답하는 것은 「손가락이 어느 몸·어느 칸 위에 있나」뿐이다. 그 몸에 붙을 수 있나(`WouldAttach`),
    // 그 카드가 적을 겨누나(`TargetsEnemies`)는 코어에 묻는다 — 이 클래스에 자격 판정이 들어오면 「붙는데 무효」가 돌아온다.
    public sealed class CoreCardTargets
    {
        private readonly BattleDriver _driver;
        private readonly CoreUnitViewPool _units;
        private readonly System.Func<Camera> _camera;

        public CoreCardTargets(BattleDriver driver, CoreUnitViewPool units, System.Func<Camera> camera)
        {
            _driver = driver;
            _units = units;
            _camera = camera;
        }

        public Camera Camera => _camera != null ? _camera() : Camera.main;
        public BattleDriver Driver => _driver;

        /// <summary>점 → 렉트 거리(안쪽 0). 픽 자석과 락온 히스테리시스가 공유한다(옛 `ScreenDistanceToRect` 그대로).</summary>
        public static float ScreenDistanceToRect(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(Mathf.Max(r.xMin - p.x, 0f), p.x - r.xMax);
            float dy = Mathf.Max(Mathf.Max(r.yMin - p.y, 0f), p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>판 위 방어유닛(배치 중 포함 — 부착 대상은 코어가 가린다)의 화면 렉트. 순서 = 월드 목록(`SimEntityId` 오름차순).</summary>
        public void EnumerateDefenderScreenRects(List<(SimEntityId id, Rect rect)> into)
        {
            into.Clear();
            var cam = Camera;
            if (_driver == null || !_driver.Running || _units == null || cam == null) return;
            var units = _driver.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Defender || u.Dead) continue;
                if (TryGetUnitScreenRect(u.Id, out var r)) into.Add((u.Id, r));
            }
        }

        public bool TryGetUnitScreenRect(SimEntityId id, out Rect rect)
        {
            rect = default;
            var cam = Camera;
            if (cam == null || _units == null) return false;
            if (_units.TryGet(id, out var view)) return view.TryGetScreenRect(cam, out rect);
            if (_units.TryGetQuad(id, out var quad)) return quad.TryGetScreenRect(cam, out rect);
            return false;
        }

        public bool TryGetUnitView(SimEntityId id, out CoreUnitView view)
        {
            view = null;
            return _units != null && _units.TryGet(id, out view) && view != null;
        }

        /// <summary>그 유닛이 지금 그려지는 자리(view). 흡수 비행이 매 프레임 따라간다.</summary>
        public bool TryGetUnitViewPosition(SimEntityId id, out Vector3 pos)
        {
            pos = default;
            return _units != null && _units.TryResolveViewPosition(id, useAnchor: false, out pos);
        }

        /// <summary>
        /// 손끝 아래 방어유닛(옛 규칙 그대로): ① 렉트(+패딩) 안이면 **앞면 우선**, 같은 면이면 중심 가까운 쪽. ② 아무 렉트도
        /// 안 품으면 자석 반경 안의 **가장 가까운** 렉트 — 자석은 `magnetFilter` 가 오면 그 집합(부착 유효 유닛)만.
        /// </summary>
        public bool TryPickDefender(Vector2 screen, out SimEntityId picked, float paddingPx = 0f, float magnetPx = 0f,
                                    HashSet<SimEntityId> magnetFilter = null)
        {
            picked = SimEntityId.None;
            var cam = Camera;
            if (_driver == null || !_driver.Running || cam == null || _units == null) return false;
            int bestOrder = int.MinValue, bestMagnetOrder = int.MinValue;
            float bestCenter = float.MaxValue, bestMagnet = float.MaxValue;
            var grid = _driver.GridSize;
            var units = _driver.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Defender || u.Dead) continue;
                if (!TryGetUnitScreenRect(u.Id, out var rect)) continue;
                if (paddingPx > 0f)
                {
                    rect.xMin -= paddingPx; rect.xMax += paddingPx;
                    rect.yMin -= paddingPx; rect.yMax += paddingPx;
                }
                // 앞면 = 렌더 정렬(발밑 칸의 정렬 값) — 뷰가 그리는 순서와 같은 사상이다.
                var cell = Somnia.Battle.BattleCore.Map.GridMath.WorldToCellUnclamped(u.Position, _driver.TileSize);
                int order = BoardSortOrder.Compute(grid, cell.x, cell.y);
                if (rect.Contains(screen))
                {
                    float dc = (rect.center - screen).sqrMagnitude;
                    if (order > bestOrder || (order == bestOrder && dc < bestCenter))
                    {
                        bestOrder = order; bestCenter = dc; picked = u.Id;
                    }
                }
                else if (bestOrder == int.MinValue && magnetPx > 0f)
                {
                    if (magnetFilter != null && !magnetFilter.Contains(u.Id)) continue;
                    float d = ScreenDistanceToRect(rect, screen);
                    if (d > magnetPx) continue;
                    bool closer = d < bestMagnet - 0.5f;
                    bool tieFront = Mathf.Abs(d - bestMagnet) <= 0.5f && order > bestMagnetOrder;
                    if (closer || tieFront) { bestMagnet = d; bestMagnetOrder = order; picked = u.Id; }
                }
            }
            return picked.IsEntity;
        }

        /// <summary>
        /// 손끝에서 반경(칸) 안의 **가장 가까운 적**(동거리는 낮은 id — 옛 규칙). 표식이 이미 있나는 여기서 안 본다
        /// (`WouldAttach` 가 `DuplicateState` 로 답한다).
        /// </summary>
        public bool TryPickNearestEnemy(Vector2 screen, float radiusTiles, out SimEntityId enemy)
        {
            enemy = SimEntityId.None;
            if (_driver == null || !_driver.Running || !TryScreenToSim(screen, out var sim)) return false;
            float maxSq = radiusTiles * _driver.TileSize;
            maxSq *= maxSq;
            float bestSq = maxSq;
            var units = _driver.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Enemy || u.Dead) continue;
                float dx = u.Position.x - sim.x, dz = u.Position.z - sim.z;
                float sq = dx * dx + dz * dz;
                if (sq < bestSq || (sq == bestSq && enemy.IsEntity && u.Id.Value < enemy.Value))
                {
                    bestSq = sq;
                    enemy = u.Id;
                }
            }
            return enemy.IsEntity;
        }

        /// <summary>
        /// 손끝 → 칸, **판 밖이면 거절**(옛 `TryScreenToCellStrict` — 관대한 판은 격자 clamp 때문에 판 밖에서도 가장자리
        /// 칸을 돌려줘 「판 밖 = 취소」가 사문화된다).
        /// </summary>
        public bool TryScreenToCellStrict(Vector2 screen, out int2 cell)
        {
            cell = default;
            if (_driver == null || !_driver.Running || !TryScreenToSim(screen, out var sim)) return false;
            cell = Somnia.Battle.BattleCore.Map.GridMath.WorldToCellUnclamped(sim, _driver.TileSize);
            return _driver.Match.Map.InBounds(cell);
        }

        public bool TryGetCellScreenCenter(int2 cell, out Vector2 screen)
        {
            screen = default;
            var cam = Camera;
            if (cam == null || _driver == null || !_driver.Running) return false;
            var p = cam.WorldToScreenPoint((Vector3)BoardSpace.ToView(_driver.Match.Map.CenterOf(cell)));
            screen = new Vector2(p.x, p.y);
            return p.z > 0f;
        }

        public Vector3 CellViewCenter(int2 cell)
            => _driver != null && _driver.Running ? (Vector3)BoardSpace.ToView(_driver.Match.Map.CenterOf(cell)) : Vector3.zero;

        private bool TryScreenToSim(Vector2 screen, out float3 sim)
        {
            sim = default;
            var cam = Camera;
            if (cam == null || !BoardSpace.IsConfigured) return false;
            var ray = cam.ScreenPointToRay(screen);
            var plane = BoardSpace.RaycastPlane();
            if (!plane.Raycast(ray, out float enter)) return false;
            sim = BoardSpace.ToSim(ray.GetPoint(enter));
            return true;
        }
    }
}
