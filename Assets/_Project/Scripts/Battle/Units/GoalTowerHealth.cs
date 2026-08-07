using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // goal-tower-siege unit 0 — 골 안정도의 **정본**(싱글턴). 골이 여럿이어도 체력은 한 풀이다.
    //
    // 이벤트 큐가 아니라 싱글턴인 이유: 안정도는 프레임 내 원샷 사건이 아니라 **상태**다.
    // 브리지가 폴링해 기존 공개 API(GoalStabilityCurrent/Max)로 서빙하므로, 체력바와
    // 점수 tie-break 는 정본이 브리지에서 ECS 로 옮겨온 것을 모른다.
    //
    // 유일한 writer 는 GoalTowerDamageSystem 이다.
    public struct GoalTowerHealth : IComponentData
    {
        public float value;
        public float max;

        // 0~1 비율. max<=0 → 0. 순수 + Burst-safe (Health.ComputeRatio 와 같은 규약).
        public static float ComputeRatio(float value, float max)
            => max > 0f ? math.clamp(value / max, 0f, 1f) : 0f;

        // 이번 프레임 누적 피해를 풀에서 깎는다. 0 바닥(음수 금지) — 오버킬 초과분은 버린다.
        // 순수 함수라 EditMode 로 결정론 검증 가능(제약 10).
        public static float ApplyDamage(float current, float taken)
        {
            float next = current - (taken > 0f ? taken : 0f);
            return next > 0f ? next : 0f;
        }
    }
}
