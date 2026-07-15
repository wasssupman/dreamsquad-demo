using Unity.Collections;

namespace Wassup.Battle.Effects
{
    // dreamcatcher-empower-aura — 드림캐쳐가 적용한 스탯 모디파이어가 활성인 유닛 판정(순수 함수).
    //
    // 트리거 = "드림캐쳐 출처(ModifierOrigin.Dreamcatcher) 스탯 모디파이어가 유닛에 활성인가".
    //   출처 판별은 슬롯의 origin 태그(unit 1 프레임워크). stackId/handle 휴리스틱 폐기.
    // revoke 는 slot 삭제가 아니라 같은 slot 에 mult=1.0 중립화(origin=Dreamcatcher 유지) →
    //   net 이 identity 면 비활성(false). 그래서 origin 필터 + net 편차를 함께 본다.
    //
    // 아키텍처-blind: NativeArray<StatModifierSlot>(POD)만 받고 EntityManager/View 불요 —
    // EditMode 로 검증(CLAUDE.md 제약 10). DamageVsCcMul/MaxHealthMul 은 판정 제외(조건부·비체감).
    public static class ModifierAuraClassifier
    {
        public const float Eps = 1e-4f;

        public static bool HasActiveDreamcatcherModifier(NativeArray<StatModifierSlot> slots)
        {
            float dAdd = 0f, dMul = 1f, dOver = 0f; bool dHasOver = false;
            float aAdd = 0f, aMul = 1f, aOver = 0f; bool aHasOver = false;
            float tAdd = 0f, tMul = 1f, tOver = 0f; bool tHasOver = false;
            float rAdd = 0f, rMul = 1f, rOver = 0f; bool rHasOver = false;
            float mAdd = 0f, mMul = 1f, mOver = 0f; bool mHasOver = false;
            bool anyDc = false;

            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s.header.origin != ModifierOrigin.Dreamcatcher) continue; // 드림캐쳐 출처만
                anyDc = true;
                switch (s.stat)
                {
                    case StatKind.DamageMul: Accumulate(s.op, s.magnitude, ref dAdd, ref dMul, ref dOver, ref dHasOver); break;
                    case StatKind.AttackSpeedMul: Accumulate(s.op, s.magnitude, ref aAdd, ref aMul, ref aOver, ref aHasOver); break;
                    case StatKind.DmgTakenMul: Accumulate(s.op, s.magnitude, ref tAdd, ref tMul, ref tOver, ref tHasOver); break;
                    case StatKind.RegenPerSec: Accumulate(s.op, s.magnitude, ref rAdd, ref rMul, ref rOver, ref rHasOver); break;
                    case StatKind.MoveSpeedMul: Accumulate(s.op, s.magnitude, ref mAdd, ref mMul, ref mOver, ref mHasOver); break;
                    // DamageVsCcMul / MaxHealthMul : 판정 제외
                }
            }
            if (!anyDc) return false;

            // net 이 base 에서 벗어나야 활성(revoke=identity → 비활성). mul 스탯 base 1, regen base 0.
            float rNet = rHasOver ? rOver : rAdd * rMul;
            return DeviatesFromOne(Net(dHasOver, dOver, dAdd, dMul))
                || DeviatesFromOne(Net(aHasOver, aOver, aAdd, aMul))
                || DeviatesFromOne(Net(tHasOver, tOver, tAdd, tMul))
                || DeviatesFromOne(Net(mHasOver, mOver, mAdd, mMul))
                || rNet > Eps || rNet < -Eps;
        }

        private static void Accumulate(CombineOp op, float magnitude, ref float add, ref float mul, ref float over, ref bool hasOver)
        {
            if (op == CombineOp.Multiplicative) mul *= magnitude;
            else if (op == CombineOp.Additive) add += magnitude;
            else { over = magnitude > over ? magnitude : over; hasOver = true; }
        }

        // 방향 보존용 net(클램프 불요). override 우선, else (1+add)*mul.
        private static float Net(bool hasOver, float over, float add, float mul) => hasOver ? over : (1f + add) * mul;

        private static bool DeviatesFromOne(float v) => v > 1f + Eps || v < 1f - Eps;
    }
}
