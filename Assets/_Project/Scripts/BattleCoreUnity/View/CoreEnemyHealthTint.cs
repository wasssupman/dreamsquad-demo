using UnityEngine;
using Somnia.Battle.Data;

namespace Somnia.Battle.BattleCoreUnity.View
{
    // battle-core-rebuild unit 8a2 행 5 — **적 저체력 틴트**의 산식(옛 `BattleBridge.EvaluateEnemyHealthTint` `:4084-4099` +
    // 호출부 `:3863`). 규칙이 아니라 그림이다 — 값은 SO(`HealthDisplayStyle.EvaluateTint` 의 그라디언트), 산식만 여기.
    //
    // 옛 규칙 그대로 두 갈래:
    //   · 표시 모드가 **통합 머리 위**(`UnitHealthPresentationMode.UnifiedOverhead`)면 틴트는 흰색 — 체력은 머리 위 바가 말하고
    //     몸을 물들이지 않는다(옛 `unifiedOverhead ? Color.white : …`). **라이브 저작(`CharacterViewConfig.asset`)이 이 모드다.**
    //   · 레거시 모드면 체력비(최대 0 이면 0 — 옛 `Health.ComputeRatio`)로 그라디언트를 평가한다. 스타일이 없으면 흰색.
    // 순수 함수(제약 10 — 분기가 있고 뷰 두 백엔드가 같은 값을 받아야 한다) — EditMode Assets lane 에서 잰다.
    public static class CoreEnemyHealthTint
    {
        /// <summary>체력비(0~1). 최대가 0 이하면 0(빈사) — 옛 `Health.ComputeRatio` 와 같은 값.</summary>
        public static float Ratio(float health, float max) => max > 0f ? Mathf.Clamp01(health / max) : 0f;

        public static Color Resolve(UnitHealthPresentationMode mode, HealthDisplayStyle style, float health, float max)
        {
            if (mode == UnitHealthPresentationMode.UnifiedOverhead || style == null) return Color.white;
            return style.EvaluateTint(Ratio(health, max));
        }
    }
}
