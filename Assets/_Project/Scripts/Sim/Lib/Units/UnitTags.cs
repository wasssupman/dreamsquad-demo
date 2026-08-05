namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/4 — 진영. 구 `Faction` 이식.
    /// ⚠ **비트 플래그다** — 판정은 `((int)value & (int)Faction.X) != 0` 이고,
    /// `AttackState.targetMask` 도 이 비트들의 조합이다. 값을 바꾸면 저작 마스크가 전부 어긋난다.
    /// </summary>
    public enum Faction : int
    {
        None = 0,
        Defender = 1 << 0,
        Enemy = 1 << 1,
        BlockingHazard = 1 << 2,
    }

    public struct FactionTag
    {
        public Faction value;
    }

    /// 배치된 방어유닛이 점유한 칸. 구 `DefenderTile` 이식.
    public struct DefenderTile
    {
        public SimInt2 cell;
    }

    /// <summary>
    /// 배치 대기 — **아직 판에 서지 않았다.** 구 `PendingDeployment` 이식(빈 태그).
    /// 필드/오라 계열이 전부 이 태그를 제외 조건으로 쓴다(on-place 오라와 같은 규칙).
    /// </summary>
    public struct PendingDeployment { }

    /// 공격(적) 유닛 표식. 구 `AttackUnitTag` 이식.
    public struct AttackUnitTag { }
}
