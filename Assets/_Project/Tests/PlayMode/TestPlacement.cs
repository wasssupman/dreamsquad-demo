using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // defender-deploy-phase — 테스트용 즉시 배치. 라이브 `PlaceDefenderAs` 는 배치 페이즈(모션 길이만큼 Deploying)를 타므로
    // «배치 직후 활성»을 전제로 짜인 테스트는 이 헬퍼로 착지+활성화까지 한 번에 한다(동기 진입점 ActivateDeployedDefender).
    // 배치 페이즈 자체를 검증하는 테스트(DropDismountTest 등)는 이걸 쓰지 않는다.
    public static class TestPlacement
    {
        public static bool PlaceActive(BattleBridge bridge, int x, int y, DefenderUnitData unit)
        {
            if (bridge == null || !bridge.PlaceDefenderAs(x, y, unit)) return false;
            var cell = new Vector2Int(x, y);
            if (bridge.TryGetDefenderAt(cell, out Entity entity) && entity != Entity.Null)
                bridge.ActivateDeployedDefender(cell, entity);
            return true;
        }
    }
}
