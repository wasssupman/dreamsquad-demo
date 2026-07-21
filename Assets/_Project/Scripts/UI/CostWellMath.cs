using UnityEngine;

namespace Wassup.UI
{
    /// <summary>
    /// tray-cost-well unit 0 — 코스트 물통의 채움 비율 산식.
    ///
    /// 물통은 CostRuntime.Current 의 소수부(= 다음 1코스트까지의 진행률)다.
    /// 단 max 에서는 리젠이 멈춰 소수부가 0 이므로, 그대로 그리면 "만땅"이
    /// "빈 통"으로 보인다. 그 분기가 이 클래스의 존재 이유다.
    /// </summary>
    public static class CostWellMath
    {
        /// <summary>
        /// 소수부 되감김 판정 임계(unit 2 의 "자연 충전 vs 외부 획득" 구분).
        ///
        /// CostRuntime.AddCost 는 Mathf.Min(_max, _current + amount) 라 정수를
        /// 더해도 결과가 다른 binade 로 넘어가면 소수부가 1 ULP 하향 드리프트한다.
        /// epsilon 없이 `fill &lt; prevFill` 로 비교하면 AddCost(1) 의 약 10% 가
        /// "자연 충전"으로 오분류된다(실례: 4.30 + 1 → 0.300000012 → 0.299999952).
        /// </summary>
        public const float FillEpsilon = 1e-4f;

        /// <summary>물통 채움 비율 [0,1]. max 도달은 소수부와 무관하게 가득으로 읽는다.</summary>
        public static float WellFill(float current, float max)
        {
            if (max <= 0f) return 0f;
            if (current >= max) return 1f;
            return Mathf.Clamp01(current - Mathf.Floor(current));
        }

        /// <summary>
        /// 상단에 표시할 정수 코스트. CostRuntime.CurrentInt 와 정확히 같은 값을 낸다
        /// (뷰가 런타임 인스턴스 없이도 계산할 수 있어야 테스트가 성립한다).
        /// 방어 클램프를 넣지 않는 이유: _current 는 음수가 될 수 없고(TrySpend 가
        /// 부족을 막고 ResetToStart 가 Clamp(0, max)), 클램프하면 등가가 깨진다.
        /// </summary>
        public static int DisplayInt(float current) => Mathf.FloorToInt(current);
    }
}
