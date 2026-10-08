// salvaged from Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierAuthoring.cs
//   (battle-core-rebuild unit 6a) — 분류 규칙과 상한 산식은 **그대로**다.
namespace Somnia.Battle.BattleCore.Effects
{
    // 저작 배율 → (버킷, 값). **한 곳에서만 한다**(2026-07-03 사용자 결정).
    //
    // 올리는 버프(배율 ≥ 1)는 **가산**이라 여러 겹이 `(1 + Σadd)` 로 합쳐지고, 깎는 디버프는
    // **곱셈**이라 수확체감이 난다. 한 겹의 값은 어느 쪽이든 같다: `(1 + (m−1)) == m`, `1 × m == m`.
    public static class ModifierAuthoring
    {
        public static void FromMultiplier(float multiplier, out CombineOp op, out float magnitude)
        {
            if (multiplier >= 1f)
            {
                op = CombineOp.Additive;
                magnitude = multiplier - 1f;
            }
            else
            {
                op = CombineOp.Multiplicative;
                magnitude = multiplier;
            }
        }

        /// <summary>
        /// 「1회분 배율 × 최대 중첩」을 **누적 상한**으로 환산한다.
        ///
        /// ⚠ `−1` 이 이 함수의 존재 이유다 — 위 `FromMultiplier` 가 버프를 가산 버킷으로 보내서
        /// 슬롯에 실리는 값이 배율이 아니라 «배율 − 1» 이다. 상한만 배율 기준으로 계산하면
        /// **조용히 한 스택만큼 어긋난다.**
        ///
        /// 0 = 누적 안 함(= 기존 덮어쓰기). 최대 중첩 1 도 0 과 같다 — 1회분이 곧 상한이라
        /// 두 번째 적용이 자기 값에서 멈춘다.
        /// </summary>
        public static float StackCap(float multiplier, int maxStacks)
            => maxStacks > 0 && multiplier > 1f ? (multiplier - 1f) * maxStacks : 0f;
    }
}
