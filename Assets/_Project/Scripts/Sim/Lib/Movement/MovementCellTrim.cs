using Wassup.Sim.Effects;

namespace Wassup.Sim.Movement
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/2 — 셀 트림. 구 `MovementCellTrim` 이식.
    /// 오라클: `MovementCellTrimTests` · `MovementCellTrimApplyTests` · `FillWalkMaskTests`.
    ///
    /// 모든 이동 모드(flow follow · aggro chase · patrol)를 **walk 타일 위에 묶는 단일 지점**이다.
    /// </summary>
    public static class MovementCellTrim
    {
        /// <summary>
        /// clamp 된 위치가 `currentCell` **안에 확실히** 있게 하는 인셋.
        /// `WorldToCell` 이 0.5 를 올려 반올림하므로, 이 오프셋이 없으면 정확히 `±0.5*tileSize`
        /// 인 위치가 인접(막힌) 셀로 매핑돼 다음 프레임의 트림 불변식(`current != target`)이 깨진다.
        /// </summary>
        private const float BoundaryEpsilon = 1e-3f;

        /// <summary>
        /// BFS 소비자용 walk 마스크(1 = 걸을 수 있음). "무엇이 걸을 수 있는 칸인가" =
        /// `IsWallCell` + 장애물 합성이고, 그 합성은 <see cref="Apply"/> 안에 이미 있다.
        /// 그리드 전체로 돌리는 코드가 두 시스템에 복제돼 있었으므로 여기 모았다 —
        /// 벽 판정이 바뀔 때 고칠 곳이 하나여야 한다.
        ///
        /// `outMask` 는 `gridSize.x * gridSize.y` 길이여야 한다(호출자 책임).
        /// </summary>
        public static void FillWalkMask(in FlowFieldSingleton field, bool hasObstacles,
                                        in ObstacleSingleton obstacles, byte[] outMask)
        {
            for (int y = 0; y < field.gridSize.y; y++)
            for (int x = 0; x < field.gridSize.x; x++)
            {
                var cell = new SimInt2(x, y);
                bool wall = IsWallCell(cell, in field)
                            || (hasObstacles && obstacles.blockedCells.Contains(cell));
                outMask[GridMath.CellIndex(cell, field.gridSize)] = wall ? (byte)0 : (byte)1;
            }
        }

        /// <summary>
        /// 경계 밖 = 벽. **골 셀은 벽 예외**다 — 골은 `flow == 0` 이라 아래 zero-flow 규칙에
        /// 걸리는데, 예외로 빼지 않으면 적이 골 밖으로 clamp 돼 누수가 영영 안 난다.
        /// 그 외에는 **zero-flow 가 곧 벽**이다.
        /// </summary>
        public static bool IsWallCell(SimInt2 cell, in FlowFieldSingleton field)
        {
            if (cell.x < 0 || cell.x >= field.gridSize.x ||
                cell.y < 0 || cell.y >= field.gridSize.y)
                return true;
            if (field.IsGoalCell(cell)) return false;
            return SimMath.LengthSq(field.flow[GridMath.CellIndex(cell, field.gridSize)]) < 1e-6f;
        }

        /// 셀 경계 안으로 clamp. 경계는 `origin` 만큼 오프셋돼 보드-로컬 공간에서 불변식이 성립한다.
        public static SimVec3 ClampToBoundary(SimVec3 desired, SimInt2 currentCell, float tileSize,
                                              SimVec3 origin = default)
        {
            float half = tileSize * 0.5f - BoundaryEpsilon;
            float centerX = origin.x + currentCell.x * tileSize;
            float centerZ = origin.z + currentCell.y * tileSize;
            return new SimVec3(
                SimMath.Clamp(desired.x, centerX - half, centerX + half),
                desired.y,
                SimMath.Clamp(desired.z, centerZ - half, centerZ + half));
        }

        /// <summary>
        /// 프레임당 XZ 변위 상한(**0.9 × tile**). <see cref="Apply"/> 의 단일 목적 셀 검사는
        /// "한 프레임에 최대 인접 셀" 을 전제하는데, dt 스파이크·강한 임펄스가 그 전제를 깨고
        /// 벽을 건너뛰는 **터널링**을 만든다. 트림 직전에 부른다.
        /// </summary>
        public static SimVec3 ClampDisplacement(SimVec3 current, SimVec3 desired, float tileSize)
        {
            float dx = desired.x - current.x;
            float dz = desired.z - current.z;
            float maxD = tileSize * 0.9f;
            float lsq = dx * dx + dz * dz;
            if (lsq <= maxD * maxD) return desired;
            float scale = maxD / SimMath.Sqrt(lsq);
            return new SimVec3(current.x + dx * scale, desired.y, current.z + dz * scale);
        }

        /// <summary>
        /// `desired` 가 벽(zero-flow) 또는 장애물 셀로 넘어가면 `currentCell` 경계로 clamp 한다.
        /// **같은 셀 안의 이동은 통과**시킨다(그게 대부분의 프레임이다).
        /// </summary>
        public static SimVec3 Apply(SimVec3 desired, SimInt2 currentCell, in FlowFieldSingleton field,
                                    bool hasObstacles, in ObstacleSingleton obstacles)
        {
            SimInt2 targetCell = GridMath.WorldToCell(desired, field.tileSize, field.gridSize,
                                                      origin: field.origin);
            if (currentCell.Equals(targetCell)) return desired;
            bool isWall = IsWallCell(targetCell, in field);
            if (!isWall && hasObstacles) isWall = obstacles.blockedCells.Contains(targetCell);
            return isWall
                ? ClampToBoundary(desired, currentCell, field.tileSize, origin: field.origin)
                : desired;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/2 — zero-flow 셀의 복구 방향. 구 `FlowRecovery` 이식.
    /// 임펄스 등으로 flow 가 zero 인 셀에 밀려났을 때 4-이웃 중 `dist` 가 **더 작은 최소 이웃**
    /// 방향을 고른다. 반환 zero = 더 나은 이웃 없음(고립) — 호출자가 정지를 택한다.
    ///
    /// ⚠ 호출자가 goal field / defender field **어느 쪽 `dist` 로도** 부른다(사냥 분기에서
    /// 배열이 바뀌는 지점이라 회귀 대상이다). 오라클: `FlowRecoveryTests`.
    /// ⚠ 이웃 검사 순서 `(+x, -x, +y, -y)` 와 **strict `<`** 가 동률 판정을 정한다.
    /// </summary>
    public static class FlowRecovery
    {
        public static SimVec2 RecoveryDir(SimInt2 cell, int[] dist, SimInt2 gridSize)
        {
            int idx = GridMath.CellIndex(cell, gridSize);
            SimVec2 dir = SimVec2.Zero;
            int best = dist[idx];
            SimInt2 nb;
            int d;
            nb = cell + new SimInt2(1, 0);  if (nb.x < gridSize.x) { d = dist[GridMath.CellIndex(nb, gridSize)]; if (d < best) { best = d; dir = new SimVec2(1, 0); } }
            nb = cell + new SimInt2(-1, 0); if (nb.x >= 0)         { d = dist[GridMath.CellIndex(nb, gridSize)]; if (d < best) { best = d; dir = new SimVec2(-1, 0); } }
            nb = cell + new SimInt2(0, 1);  if (nb.y < gridSize.y) { d = dist[GridMath.CellIndex(nb, gridSize)]; if (d < best) { best = d; dir = new SimVec2(0, 1); } }
            nb = cell + new SimInt2(0, -1); if (nb.y >= 0)         { d = dist[GridMath.CellIndex(nb, gridSize)]; if (d < best) { best = d; dir = new SimVec2(0, -1); } }
            return dir;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/2 — 코너 엣지-허깅 측면 복원. 구 `LateralRecenter` 이식.
    /// 진행방향 수직(perp) 성분을 **dead-band 밖일 때만** 셀 중심선 쪽으로 당긴다.
    /// 안쪽으로만 움직이므로(|perp| 감소) 벽 침투가 없고, 밴드 안(스폰 분산 범위)은 보존된다.
    /// 오라클: `LateralRecenterTests`.
    /// </summary>
    public static class LateralRecenter
    {
        /// <summary>
        /// 스폰 분산폭(기본 0.2)보다 크고 코너 드리프트(실측 0.29~0.49)보다 작게 —
        /// 직진 분산은 보존하고 코너만 교정한다. 게임플레이 값이 아니라 이동 품질 상수다.
        /// </summary>
        public const float DeadbandFraction = 0.25f;
        /// `rate = RateK · speed`(속도 비례). **상수 rate 금지** — 빠른 적이 엣지를 거리상 더 오래 탄다.
        public const float RateK = 0.4f;

        public static SimVec3 Compute(SimVec3 current, SimInt2 cell, SimVec2 flowDir,
                                      float speed, float dt, float tileSize, SimVec3 origin)
        {
            SimVec2 perpAxis = SpawnSpread.Perpendicular(flowDir);
            SimVec3 center = GridMath.CellToWorldCenter(cell, tileSize, current.y, origin);
            var off = new SimVec2(current.x - center.x, current.z - center.z);
            float perp = SimMath.Dot(off, perpAxis);
            float adp = SimMath.Abs(perp);
            float deadband = DeadbandFraction * tileSize;
            if (adp <= deadband) return SimVec3.Zero;   // 밴드 안 — 분산 보존
            // 밴드 **가장자리까지만** 당긴다(0 관통·오버슈트 방지). 코너 드리프트는 deadband 에 정착.
            float step = SimMath.Min(RateK * speed * dt, adp - deadband);
            SimVec2 xz = -SimMath.Sign(perp) * perpAxis * step;
            return new SimVec3(xz.x, 0f, xz.y);
        }
    }
}
