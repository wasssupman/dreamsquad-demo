// salvaged from Assets/_Project/Scripts/Battle/Effects/Modifiers/{ModifierMath, ModifierStatsAggregateSystem}.cs
//   (battle-core-rebuild unit 6a) — 결합식과 클램프 경계는 **그대로**다.
// 이식 시 바뀐 것: 경계 상수가 집계 시스템 안이 아니라 여기 산다(소비처가 `ModifierSet` 하나).
using Unity.Mathematics;

namespace Wassup.BattleCore.Effects
{
    // 「슬롯들이 하나의 값으로 접힌다」의 산식.
    //
    // `clamp((1 + Σadd) × Πmul, 바닥, 천장)`. 출처가 다른 곱셈 모디파이어는 무한히 겹칠 수
    // 있으므로(병합 키에 출처가 있다) 경계가 없으면 디버프 곱이 값을 0 으로 썩히고 버프 곱이
    // 발산한다 — 이 클램프가 그 둘만 막는다(평범한 한 겹은 경계 근처에도 안 간다).
    //
    // ⚠ **비율 합성은 float 그대로다**(00_synthesis §C-3). 고정소수점 전환은 모든 저작
    // 수치의 재조정을 부르므로 새 코어도 현행을 유지한다.
    public static class ModifierMath
    {
        // 경계 넷은 **근거를 아는 상수**다(제약 6 의 예외 — 6a 완료 기준이 「상수로 남되 근거
        // 주석 동반」을 명시했다). SO 저작화는 후속 후보로 남아 있다.

        /// <summary>일반 배율 바닥 — 최대 −80%.</summary>
        public const float MulStatFloor = 0.2f;

        /// <summary>일반 배율 천장 — 최대 +400%.</summary>
        public const float MulStatCeil = 5f;

        /// <summary>
        /// 이동 배율 바닥. 저작된 감속은 여기 안 닿는다(얼음 ×0.4). **전면 정지가 필요해지면
        /// 배율 0 이 아니라 전용 플래그로 만든다** — 그 값은 이 바닥에 먼저 걸린다.
        /// </summary>
        public const float MoveMulFloor = 0.15f;

        public const float MoveMulCeil = 3f;

        /// <summary>최대 체력 전용 바닥 — 라스트런 ×0.1 이 일반 바닥(0.2)에 걸리면 안 된다.</summary>
        public const float MaxHealthMulFloor = 0.05f;

        /// <summary>
        /// `override` 가 있으면 그것이 이긴다(그쪽도 클램프한다 — 저작 값이 정책을 못 넘는다).
        /// 없으면 `(1 + Σadd) × Πmul`.
        /// </summary>
        public static float CombineMul(bool hasOver, float over, float add, float mul,
                                       float floor, float ceil)
        {
            float raw = hasOver ? over : (1f + add) * mul;
            return math.clamp(raw, floor, ceil);
        }

        /// <summary>스탯별 경계. 분기를 소비처에 흩지 않기 위한 단일 표다.</summary>
        public static void BoundsOf(StatKind stat, out float floor, out float ceil)
        {
            switch (stat)
            {
                case StatKind.MoveSpeedMul:
                    floor = MoveMulFloor;
                    ceil = MoveMulCeil;
                    return;
                case StatKind.MaxHealthMul:
                    floor = MaxHealthMulFloor;
                    ceil = MulStatCeil;
                    return;
                default:
                    floor = MulStatFloor;
                    ceil = MulStatCeil;
                    return;
            }
        }
    }
}
