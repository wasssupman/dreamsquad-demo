using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // bomb-thrower-defender unit 0 — 착지 셀 결정 순수 함수 (plain in→out, 제약 10).
    // cardinalDir = DeployedFacing.value(이미 cardinal 단위 int2 — snap 불필요).
    // off-grid(맵 밖) 착지면 valid=false → 호출처(AttackSystem)가 발사 스킵(계약 4).
    // 산식은 얇지만 grid-edge off-by-one 은 이동/타겟팅의 전형적 회귀 지점이라
    // 순수+EditMode 로 (c) 가치를 남긴다.
    public static class BombLanding
    {
        public static void ResolveCell(int2 casterCell, int2 cardinalDir, int tilesN,
            int2 gridSize, out int2 cell, out bool valid)
        {
            cell = casterCell + cardinalDir * tilesN;
            valid = cell.x >= 0 && cell.x < gridSize.x
                 && cell.y >= 0 && cell.y < gridSize.y;
        }
    }
}
