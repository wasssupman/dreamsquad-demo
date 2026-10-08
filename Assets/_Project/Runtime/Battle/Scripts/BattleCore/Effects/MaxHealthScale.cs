// salvaged from Assets/_Project/Scripts/Battle/Units/Health.cs (`ScaleMax`)
//   + Battle/Units/MaxHealthScaleSystem.cs (battle-core-rebuild unit 6a).
// 이식 시 바뀐 것: `appliedMul` 캐시를 **안 옮겼다** — 이 함수가 멱등이라(같은 배율로 다시
//   적용해도 결과가 같다) 캐시는 비용 절약일 뿐 규칙이 아니었고, 캐시가 있으면 「기준값과
//   캐시가 갈리는」 두 번째 상태가 생긴다.
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Effects
{
    // 최대 체력 배율의 적용. **Effects 가 배율을 정하고 체력은 한 곳만 쓴다.**
    //
    // ⚠ 기준은 **항상 스폰 시점 원본**(`Unit.BaseMaxHealth`)이다. 현재 최대치에 곱하면
    // 누적 오염이 난다. 그리고 **복원에 무료 회복이 없다** — 배율이 1 로 돌아와도 현재
    // 체력은 안 올라간다(축소 때 잘린 것은 잘린 채다).
    public static class MaxHealthScale
    {
        /// <summary>최대 체력의 바닥. 1 HP 아래로는 안 내려간다(배율이 0 이어도 죽지 않는다).</summary>
        public const float MinMaxHealth = 1f;

        public static void Apply(float value, float baseMax, float mul,
                                 out float newValue, out float newMax)
        {
            newMax = math.max(MinMaxHealth, baseMax * mul);
            newValue = math.min(value, newMax);
        }
    }
}
