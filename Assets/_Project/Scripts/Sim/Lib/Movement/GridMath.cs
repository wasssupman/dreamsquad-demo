namespace Wassup.Sim.Movement
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/2 — 위치. 구 `Unity.Transforms.LocalTransform` 대응.
    ///
    /// **`Position` 만 옮겼다.** 구 `LocalTransform` 은 `Rotation`·`Scale` 도 갖지만 sim 코드가
    /// 그 둘을 **쓰지 않는다**(`quaternion`·`float4x4` 사용 0 — 계획서 §수학 소유권 실측).
    /// 회전은 프레젠테이션의 facing 이고, `Scale` 은 상태 해시의 **제외 축**이다(P5).
    ///
    /// ⚠ **18-K 에 남기는 질문**: 트레이스가 `LocalTransform` 을 통째로 찍으므로 `Rotation` 이
    /// 상태 라인에 들어간다(P5 는 `Scale` 만 제외했다). sim 이 회전을 쓰지 않으므로 그 값은
    /// 스폰 시점 이후 불변이고, **비교기가 그 필드를 공급할지 제외할지는 18-K 의 결정**이다.
    /// 여기서 회전 타입을 발명하지 않는 이유: 쓰는 코드가 0 이고(제약 8), 필요하면 emitter 를
    /// 소유한 18-K 가 그때 정확한 모양을 안다.
    /// </summary>
    public struct SimTransform
    {
        public SimVec3 Position;

        public static SimTransform FromPosition(SimVec3 p) => new SimTransform { Position = p };
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/2 — 보드 격자 ↔ 월드 변환. 구 `GridMath` 이식.
    /// 오라클: `GridMathTests`.
    /// </summary>
    public static class GridMath
    {
        /// <summary>
        /// 격자 안으로 **접어 넣는다**(clamp). `origin` 은 보드 월드 원점(Tilemap 모드 = 0).
        /// </summary>
        public static SimInt2 WorldToCell(SimVec3 worldPos, float tileSize, SimInt2 gridSize,
                                          SimVec3 origin = default)
        {
            SimInt2 cell = WorldToCellUnclamped(worldPos, tileSize, origin);
            return new SimInt2(
                SimMath.Clamp(cell.x, 0, gridSize.x - 1),
                SimMath.Clamp(cell.y, 0, gridSize.y - 1));
        }

        /// <summary>
        /// clamp 하지 않는 셀 계산. <see cref="WorldToCell"/> 은 격자 안으로 접기 때문에
        /// "보드 밖인가" 를 물을 수 없다 — 보드 밖을 **거절**해야 하는 경로가 이걸 쓴다.
        ///
        /// ⚠ 라운딩은 `floor(x + 0.5)` = **half-away-from-zero-on-positive** 다.
        /// `math.round`(짝수 반올림)로 바꾸면 2.5 가 3 이 아니라 2 로 가서 셀 하나가 밀린다.
        /// 라운딩 규칙은 이 함수 하나에만 둔다(위가 이걸 감싼다).
        /// </summary>
        public static SimInt2 WorldToCellUnclamped(SimVec3 worldPos, float tileSize, SimVec3 origin = default)
        {
            SimVec3 local = worldPos - origin;
            return new SimInt2(
                (int)SimMath.Floor(local.x / tileSize + 0.5f),
                (int)SimMath.Floor(local.z / tileSize + 0.5f));
        }

        /// ⚠ `origin.y` 와 인자 `y` 가 **둘 다** 더해진다(구 구현 그대로).
        public static SimVec3 CellToWorldCenter(SimInt2 cell, float tileSize, float y = 0f,
                                                SimVec3 origin = default)
            => origin + new SimVec3(cell.x * tileSize, y, cell.y * tileSize);

        public static int CellIndex(SimInt2 cell, SimInt2 gridSize) => cell.y * gridSize.x + cell.x;

        /// `cmax(abs(a - b))` — 대각선 거리가 1 이다.
        public static int ChebyshevDistance(SimInt2 a, SimInt2 b)
            => SimMath.Max(SimMath.Abs(a.x - b.x), SimMath.Abs(a.y - b.y));

        /// half-away-from-zero — `math.round` 의 짝수 반올림을 피한다.
        public static int RangeToTiles(float r) => (int)(r + 0.5f);
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/2 — 진행방향의 단위 수직벡터. 구 `SpawnSpread` 에서
    /// **이 함수만** 옮겼다 — `LateralRecenter` 의 유일한 의존이고, 나머지(스폰 레인 분산)는
    /// 스폰 시점 = Bridge 소유라 sim 규칙이 아니다. 필요해지면 그때 옮긴다(제약 8).
    /// </summary>
    public static class SpawnSpread
    {
        /// 0 입력은 `(1,0)` 기준으로 폴백한다.
        public static SimVec2 Perpendicular(SimVec2 flowDir)
        {
            SimVec2 d = SimMath.NormalizeSafe(flowDir, new SimVec2(1f, 0f));
            return new SimVec2(-d.y, d.x);
        }
    }
}
