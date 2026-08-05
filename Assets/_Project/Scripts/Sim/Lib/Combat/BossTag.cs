namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-D — 보스 표식. 구 `Wassup.Battle.Combat.BossTag` 이식(빈 태그).
    ///
    /// **18-D 가 여는 이유**: `CcApplySystem` 이 보스 CC 면역을 판정하려면 이 태그를 읽어야 한다.
    /// Combat 맥락 소유이고 Effects 는 **읽기만** 한다 — 맥락 경계(제약 2의 후계)를 폴더가 표시한다.
    /// Combat 의 나머지는 18-F~18-I 가 채운다.
    /// </summary>
    public struct BossTag { }
}
