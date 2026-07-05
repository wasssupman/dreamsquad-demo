using Unity.Entities;
using Unity.Transforms;

namespace Wassup.Battle
{
    // 전투 시뮬레이션 전체(Units/Movement/Combat/Effects)를 감싸는 부모 그룹.
    //
    // time-manager Unit 1/2 — 흩어져 있던 24개 배틀 시스템을 이 그룹 하위로 모아, 그룹에
    // 붙는 BattleScaledRateManager(Unit 2) 한 지점에서 정지/슬로우모를 제어한다.
    // 그룹 내부 시스템들의 기존 [UpdateBefore/After] 상대 순서는 전부 배틀 시스템끼리라
    // 그대로 유효하다.
    //
    // 이동/투사체 시스템이 LocalTransform 를 write 하므로 TransformSystemGroup 앞에서
    // 돌아 같은 프레임에 LocalToWorld → 렌더로 반영되게 한다(재타겟 전 실효 순서 보존,
    // 1프레임 stale 방지).
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class BattleSimGroup : ComponentSystemGroup
    {
        // time-manager Unit 2 — 이 그룹의 시간 진행을 BattleTimeScale 로 제어한다.
        // 값이 없으면(BattleTimeScale singleton 미생성) 정상 속도로 동작.
        protected override void OnCreate()
        {
            base.OnCreate();
            RateManager = new BattleScaledRateManager();
        }
    }
}
