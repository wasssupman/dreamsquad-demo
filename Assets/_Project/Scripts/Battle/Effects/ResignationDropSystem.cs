// season-gimmick-clockout unit 8 (rework) — 룰 1 재설계: 강제 퇴근(10초 타이머)을 폐기하고,
// 배치 defender 가 (원인 불문) 사망하면 그 배치 타일에 사직서(Resignation)를 드랍한다.
// 강제로 배치 유닛이 사라지는 감성 문제 해소 — 유닛은 자연 사망 시에만 사직서를 남긴다.
//
// UnitLifecycleSystem 이 죽은 defender 를 파괴하기 "전"에 관찰해야 하므로 그 앞에서 돈다.
// DeadTag 는 사망 프레임에 붙고 같은 프레임 UnitLifecycleSystem 이 파괴하므로 defender 당 1회만
// 관측된다(중복 드랍 없음). ClockOutGimmickConfig 부재(기믹 비활성) 시 미가동(self-gate).
// 사망은 전투(running) 중에만 발생하므로 별도 running-gate 불필요.
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(DamageApplicationSystem))]
    [UpdateAfter(typeof(HealthDeathSystem))]
    [UpdateBefore(typeof(UnitLifecycleSystem))]
    public partial struct ResignationDropSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ClockOutGimmickConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 사망 태그가 붙은 defender — 파괴 직전 그 배치 타일에 사직서 스폰(Effects 소유 아키타입).
            // 사망 셀은 DefenderFootprint(RO, Units 소유 → 읽기 허용). destroy 전이라 참조 유효.
            foreach (var tile in
                     SystemAPI.Query<RefRO<DefenderFootprint>>()
                              .WithAll<DeadTag, DefenderUnitTag>())
            {
                var letter = ecb.CreateEntity();
                ecb.AddComponent(letter, new Resignation { cell = tile.ValueRO.anchor });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
