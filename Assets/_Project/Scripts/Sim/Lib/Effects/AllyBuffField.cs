namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/4 — 아군 버프 장판(캐리어 컴포넌트). 구 `AllyBuffField` 이식.
    ///
    /// 멤버십은 스냅샷이 아니라 **매 프레임 재발행**이다 — 벗어나면 재발행이 끊겨 모디파이어가
    /// <see cref="ApplySec"/> 안에 자연 소멸한다("갱신이 곧 회수").
    /// </summary>
    public struct AllyBuffField
    {
        /// <summary>
        /// 모디파이어 슬롯 네임스페이스: on-place=0 · 시너지=1 · 효과타일=2 ·
        /// **스킬 아군 버프=3** · 드림캐쳐=100+. 전용 슬롯이라 배치 오라(0)와 합산되고,
        /// 같은 장판의 반복 갱신은 refresh 다.
        /// </summary>
        public const ushort StackId = 3;

        /// <summary>
        /// 재발행 duration. 구 sim 은 이 상수를 `EffectSpawner`(Bridge 측 스포너)에 뒀는데,
        /// 신 sim 에서는 **규칙이 쓰는 값이므로 규칙 옆에** 둔다.
        ///
        /// ⚠ **항상 이 값이어야 한다.** `ModifierApplySystem` 의 refresh 가
        /// `remaining = max(old, new)` 라서 한 번이라도 스킬 지속시간(예: 8초)으로 걸면 이후
        /// 갱신이 그 값을 내릴 수 없고, 장판을 벗어나도 8초간 버프가 남는다 —
        /// 장판화가 없애려던 스냅샷 동작으로 회귀한다. 스킬 지속시간은 캐리어 수명에만 쓴다.
        /// </summary>
        public const float ApplySec = 0.5f;

        public SimInt2 centerCell;
        public int tileRange;
        public StatKind stat;
        /// 배율 그대로(예: ×2.0). op/magnitude 분류는 `SimModifierAuthoring.FromMultiplier` 가 단독 소유.
        public float magnitude;
        /// 캐리어 수명. 감쇠는 `EffectTickSystem`(18-E 범위 밖 — 별도 조각) 몫이다.
        public float remaining;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/4 — 방어유닛 지향 필드. 구 `DefenderFieldSingleton` 이식.
    /// **소비자는 보스뿐**이고(`MovementSystem` 의 hunting 분기) 소유자는 #7 이다.
    /// 방어유닛 0 이면 전 셀 `int.MaxValue` → 소비자가 자동으로 기존 goal 마칭으로 폴백한다.
    /// </summary>
    public struct DefenderFieldSingleton
    {
        /// 1 = walkable, 0 = blocked. 이 필드의 BFS 입력이다(Bridge 가 맵에서 굽는다).
        public byte[] walkMask;
        /// 최근접 "방어유닛 사거리 내 walk 셀" 로 향하는 단위 방향.
        public SimVec2[] flow;
        /// 소스까지 BFS cost. 방어유닛 0 / 도달불가 = `int.MaxValue`.
        public int[] dist;
        public SimInt2 gridSize;
        public float tileSize;
        public SimVec3 origin;

        public bool IsCreated => walkMask != null && flow != null && dist != null;
    }
}
