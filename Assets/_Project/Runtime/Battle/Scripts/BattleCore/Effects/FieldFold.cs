using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Effects
{
    // battle-core-rebuild unit 6b — **겹친 장판의 승자를 순회 순서에 맡기지 않는다**(F23).
    //
    // 존 장판도 아군 버프 장도 「안에 있으면 매 틱 재발행」이라 겹치면 같은 슬롯을 여러 번
    // 쓴다. 슬롯은 마지막 쓴 값을 들므로 **승자가 순회 순서**가 되는데, 옛 전투에서는 그
    // 순서가 만료의 swap-back 으로 런타임에 뒤섞였다 — 배율이 다른 장판 두 장이 겹치면
    // 승자가 무작위였다. 리스트로 바뀌어도 규율은 같다: **한 번만 쓰되 가장 강한 값으로.**
    //
    // ⚠ 「가장 강한」의 방향은 **버킷이 정한다.** `ModifierAuthoring` 이 올리는 버프(배율 ≥ 1)를
    // 가산으로, 깎는 디버프(배율 < 1)를 곱셈으로 보내기 때문에 버킷 안에서는 방향이 하나다:
    //   · 가산 — 실리는 값이 «배율 − 1» 이라 **클수록** 세다.
    //   · 곱셈 — 실리는 값이 배율 그대로라 **작을수록** 세다(0.5 가 0.8 보다 세다).
    // 두 버킷을 한 자로 재려 들면 감속 장판 둘이 겹칠 때 약한 쪽이 이긴다.
    public static class FieldFold
    {
        /// <summary>같은 슬롯을 노리는 두 값 중 가장 강한 것.</summary>
        public static float Strongest(CombineOp op, float a, float b)
            => op == CombineOp.Multiplicative ? math.min(a, b) : math.max(a, b);
    }

    // 존의 재발행 여유 — **밸런스 값이 아니라 프레임워크 상한**이다(F36).
    //
    // 옛 아군 장판은 `0.5초` 였고 그 근거는 Unity 의 `Maximum Allowed Timestep`(0.3333) 이었다:
    // 규칙은 「재발행 지속 > 최대 틱 델타」이고, 히칭 프레임 한 번에 만료가 방금 건 값을 넘어
    // 깎으면 그 프레임만 장판 안 전원이 기본 스탯으로 돌아간다(조용하고 자가치유돼서 버그로
    // 안 보이고 「버프가 일정하지 않다」로만 느껴진다).
    //
    // **고정 틱에서는 그 근거가 바뀐다.** 델타가 `1/60` 하나뿐이고 정지·슬로모는 틱 발행률이라
    // 「큰 델타 한 번」이 원리적으로 없다. 남는 위험은 부동소수 반올림뿐이므로 여유는 «틱 몇
    // 개»로 센다 — 3틱이면 `Remaining` 이 한 번의 만료 뒤에도 2틱 남아 반올림이 넘볼 수 없다.
    //
    // ⚠ 그래서 **「나가면 곧 풀린다」의 여유가 0.5초에서 0.05초로 줄어든다.** 라이브 영향은
    // 0 이다(아군 버프 장을 까는 자가 unit 7 이라 오늘 생산자가 없다). 체감을 되돌리고 싶으면
    // 그것은 프레임워크 상한이 아니라 **저작값**이므로 카드 저작에 여유 필드를 연다.
    public static class FieldRefresh
    {
        /// <summary>여유를 재는 단위 = 틱 수. 2 가 하한이고 1 을 더해 반올림을 덮는다.</summary>
        public const int TickMargin = 3;

        /// <summary>그 틱 길이에서의 재발행 지속(초).</summary>
        public static float Seconds(float dt) => dt * TickMargin;
    }
}
