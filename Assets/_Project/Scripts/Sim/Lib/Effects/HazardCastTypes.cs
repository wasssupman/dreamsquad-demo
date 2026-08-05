namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/1 — 캐스트가 낳는 해저드의 종류. 구 `HazardCastKind` 이식.
    /// ⚠ append-only.
    /// </summary>
    public enum HazardCastKind : byte
    {
        None = 0,
        /// 밟으면 효과가 걸리는 장판.
        Zone = 1,
        /// 길을 막는 구조물(체력을 갖는다).
        Blocking = 2,
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/1 — 해저드 캐스트 상태. 구 `HazardCastState` 이식.
    ///
    /// ⚠ 이 캐스터들은 **`attackRange` 가 0** 이라 공격 루프의 RESOLVE 에 도달하지 못한다.
    /// 그래서 캐스트 성사를 별도 사건으로 내보내야 AttackN 트리거가 돈다 —
    /// <see cref="Wassup.Sim.Combat.CastEvent"/> 가 그 통로다.
    /// </summary>
    public struct HazardCastState
    {
        public float range;
        public float cooldownDuration;
        public float cooldownRemaining;
        /// `Faction` 비트 조합.
        public int targetMask;
        public int dataIndex;
        public HazardCastKind kind;
        public int footprintWidth;
        public int footprintHeight;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/1 — 해저드 스폰 요청. 구 `HazardSpawnRequest` 이식.
    /// sim 이 "무엇을 어디에" 만 정하고 실제 생성은 소비 지점(18-K)이 한다.
    /// </summary>
    public struct HazardSpawnRequest
    {
        public HazardCastKind kind;
        public int dataIndex;
        public SimInt2 centerCell;
        public int width;
        public int height;
        public SimEntityId caster;
        public SimEntityId target;
    }
}
