using Unity.Burst;
using Unity.Entities;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct CcApplySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyCcEventsSingleton>();
        }

        // OnUpdate is not Burst-compiled because GetBuffer is not Burst-eligible.
        // Mirrors EffectTickSystem.cs's deliberate non-Burst pattern.
        public void OnUpdate(ref SystemState state)
        {
            var queue = SystemAPI.GetSingleton<EnemyCcEventsSingleton>().queue;
            // boss-jjangssen unit 3 — 보스 면역. 모든 CC 생산자(AttackSystem·ProjectileHitSystem·
            // ZoneApplySystem·StackModifierTickSystem)가 이 큐로 수렴하므로 **부여 시점 1곳**에서
            // 막으면 끝난다. IsLocked 판정 쪽(이동·공격 락·변위·상태FX·wake-on-hit)에 넣으면
            // 무시 지점이 6곳 이상이라 회귀 표면이 훨씬 커진다. BossTag(Combat 소유)는 RO 읽기.
            var bossLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Combat.BossTag>(isReadOnly: true);

            while (queue.TryDequeue(out var evt))
            {
                if (!state.EntityManager.Exists(evt.target))
                    continue;
                if (state.EntityManager.HasComponent<Wassup.Battle.Units.StructureTag>(evt.target))
                    continue;
                if (!state.EntityManager.HasBuffer<CcEffect>(evt.target))
                    continue;
                if (bossLookup.HasComponent(evt.target)
                    && CcActionLock.IsBossImmune(evt.effect.kind))
                    continue;

                var buffer = state.EntityManager.GetBuffer<CcEffect>(evt.target);
                // 병합 정책은 CcEffectMerge 단일 소스(EffectSpawner 와 공유).
                CcEffectMerge.Apply(ref buffer, evt.effect);
            }
        }
    }
}
