namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/2 arm C/1 — 폭탄 착지 셀 산출. 구 `BombLanding` 이식.
    /// 오라클: `SimBombLandingTests`(구 `BombLandingTests` 복제).
    ///
    /// ⚠ **격자 밖을 clamp 하지 않는다** — `GridMath.WorldToCell` 과 정반대다. 폭탄맨이 판
    /// 가장자리에서 밖을 보고 있으면 그 발사는 **일어나지 않아야** 하고, clamp 하면 가장자리
    /// 셀에 조용히 떨어진다. `valid` 가 그 거절을 나르며, 거절된 프레임은 공격 사건도 아니다
    /// (`AttackN` 카운트가 돌지 않는다 — attack-decoupling 계약 2).
    /// </summary>
    public static class BombLanding
    {
        public static void ResolveCell(SimInt2 casterCell, SimInt2 cardinalDir, int tilesN,
            SimInt2 gridSize, out SimInt2 cell, out bool valid)
        {
            cell = new SimInt2(casterCell.x + cardinalDir.x * tilesN,
                               casterCell.y + cardinalDir.y * tilesN);
            valid = cell.x >= 0 && cell.x < gridSize.x
                 && cell.y >= 0 && cell.y < gridSize.y;
        }
    }
}
