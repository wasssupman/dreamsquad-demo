using Unity.Burst;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // goal-tower-siege unit 0 — 골 타워 피해를 **공유 풀**에 적립하고 각 타워에 미러한다.
    //
    // **[UpdateBefore(DamageApplicationSystem)] 이 이 설계의 핵심이다.** 타워의
    // IncomingDamage 를 그 시스템보다 먼저 소비(=버퍼를 비움)하므로:
    //
    //   (a) DamageApplicationSystem 이 타워 Health 를 건드리지 않는다 → 개별 타워가
    //       DeadTag 를 받아 UnitLifecycleSystem 에 파괴되는 경로가 아예 없다.
    //   (b) "타워 Health 의 감소분을 역산해 풀에서 깎는" 델타 계산이 필요 없다. 초안은
    //       그 방식이었고, write-back 이 value=pool 이라 다음 프레임의 (max − value) 가
    //       **누적 결손**이 되어 pool' = 2·pool − max 로 발산했다(골 2개면 3·pool − 2·max).
    //       첫 피격 후 5~7프레임에 허위 패배가 났다. 여기서는 받은 값을 그대로 더한다.
    //
    // 부수 효과: 타워 피격은 데미지 폰트를 만들지 않는다(그 발화도 DamageApplicationSystem
    // 에 있다). 타워는 유닛이 아니므로 의도된 결과다.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(DamageApplicationSystem))]
    public partial struct GoalTowerDamageSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GoalTowerHealth>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var poolRW = SystemAPI.GetSingletonRW<GoalTowerHealth>();

            float taken = 0f;
            foreach (var damage in SystemAPI.Query<DynamicBuffer<IncomingDamage>>().WithAll<GoalTowerTag>())
            {
                for (int i = 0; i < damage.Length; i++) taken += damage[i].amount;
                damage.Clear();
            }

            if (taken > 0f)
                poolRW.ValueRW.value = GoalTowerHealth.ApplyDamage(poolRW.ValueRO.value, taken);

            // 미러는 매 프레임 되쓴다 — 피해가 없어도 타워가 늦게 스폰되거나 풀이 외부에서
            // 리셋된 경우를 따라오게 한다. 쓰기 비용은 골 개수(1~2)라 무시 가능.
            float value = poolRW.ValueRO.value;
            float max = poolRW.ValueRO.max;
            foreach (var health in SystemAPI.Query<RefRW<Health>>().WithAll<GoalTowerTag>())
            {
                health.ValueRW.value = value;
                health.ValueRW.max = max;
            }
        }
    }
}
