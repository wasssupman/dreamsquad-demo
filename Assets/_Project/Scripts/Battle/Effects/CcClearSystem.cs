using Unity.Burst;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // combat-action-lock unit 3 — wake-on-hit 소비자. DamageApplicationSystem(Units)이
    // 넣은 clear 요청을 소비해 해당 entity 의 CcEffect 버퍼에서 그 kind 를 제거한다.
    // DamageApplicationSystem 직후 정렬 → 당 프레임 wake(다음 프레임 지연 없음).
    // OnUpdate 는 매니지드 유지(CcApplySystem 선례 일치). 사실 GetBuffer/Exists/HasBuffer +
    // RemoveAtSwapBack(버퍼 내용 변경, 구조변경 아님)은 Burst 가능하나, 선례 통일 위해 미버스트.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(Wassup.Battle.Units.DamageApplicationSystem))]
    public partial struct CcClearSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CcClearRequestsSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var queue = SystemAPI.GetSingleton<CcClearRequestsSingleton>().queue;
            var em = state.EntityManager;
            while (queue.TryDequeue(out var req))
            {
                if (!em.Exists(req.entity)) continue;          // lethal-hit 로 파괴됐을 수 있음
                if (!em.HasBuffer<CcEffect>(req.entity)) continue;
                var buf = em.GetBuffer<CcEffect>(req.entity);
                for (int i = buf.Length - 1; i >= 0; i--)
                    if (buf[i].kind == req.kind) buf.RemoveAtSwapBack(i);
            }
        }
    }
}
