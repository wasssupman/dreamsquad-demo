using System.Collections.Generic;

namespace Wassup.BattleCore.Effects
{
    // battle-core-rebuild unit 6c — **몸에 붙는 오라가 켜져야 하나**의 순수 판정.
    // 옛 `Wassup.Battle.Effects.ModifierAuraClassifier`(dreamcatcher-empower-aura)의 salvage 다.
    //
    // 옮긴 것은 규칙 둘이다:
    //   · **출처 필터** — 슬롯의 꼬리표(`ModifierOrigin`)만 본다. 병합 키(`SlotTag`)가 아니다.
    //   · **net 편차** — 그 출처의 슬롯들을 접은 값이 기준(배율 1, 재생 0)에서 벗어나야 켜진다.
    //     같은 출처가 올리고 내리는 두 슬롯을 들 수 있으므로(서로 상쇄), 「슬롯이 있다」만으로는
    //     켜지 않는다.
    //
    // 달라진 것 하나: 옛 회수는 같은 슬롯을 배율 1 로 **중화**했으므로 「중화된 슬롯」이 판정의 큰
    // 몫이었다. 6a 가 회수를 **슬롯 삭제**로 바꿨으므로 그 경우는 이제 「슬롯이 없다」이고, 판정은
    // 그만큼 단순해졌다. 그래도 net 편차 축은 남긴다(위의 상쇄 때문).
    //
    // ⚠ `DamageVsCcMul`·`MaxHealthMul` 은 **판정 제외**다(옛 규칙 그대로). 앞은 대상 조건부라
    // 몸에 붙은 오라로 읽히면 거짓이고, 뒤는 체감이 체력 바로 이미 보인다.
    //
    // 아키텍처-blind: 슬롯 목록(plain)만 받는다 — 뷰가 부르고, EditMode 가 검증한다(제약 10).
    public static class ModifierAuraClassifier
    {
        public const float Eps = 1e-4f;

        /// <summary>
        /// `origin` 출처의 스탯 슬롯이 **실제로 무언가를 바꾸고 있나**. 슬롯이 없으면 false,
        /// 있어도 접은 값이 기준에서 안 벗어나면 false.
        /// </summary>
        public static bool HasActiveModifier(IReadOnlyList<ModifierSlot> slots, ModifierOrigin origin)
        {
            if (slots == null || slots.Count == 0) return false;

            float dAdd = 0f, dMul = 1f, dOver = 0f; bool dHasOver = false;
            float aAdd = 0f, aMul = 1f, aOver = 0f; bool aHasOver = false;
            float tAdd = 0f, tMul = 1f, tOver = 0f; bool tHasOver = false;
            float rAdd = 0f, rMul = 1f, rOver = 0f; bool rHasOver = false;
            float mAdd = 0f, mMul = 1f, mOver = 0f; bool mHasOver = false;
            bool any = false;

            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.Origin != origin) continue;
                any = true;
                switch (s.Key.Stat)
                {
                    case StatKind.DamageMul: Accumulate(s.Key.Op, s.Magnitude, ref dAdd, ref dMul, ref dOver, ref dHasOver); break;
                    case StatKind.AttackSpeedMul: Accumulate(s.Key.Op, s.Magnitude, ref aAdd, ref aMul, ref aOver, ref aHasOver); break;
                    case StatKind.DmgTakenMul: Accumulate(s.Key.Op, s.Magnitude, ref tAdd, ref tMul, ref tOver, ref tHasOver); break;
                    case StatKind.RegenPerSec: Accumulate(s.Key.Op, s.Magnitude, ref rAdd, ref rMul, ref rOver, ref rHasOver); break;
                    case StatKind.MoveSpeedMul: Accumulate(s.Key.Op, s.Magnitude, ref mAdd, ref mMul, ref mOver, ref mHasOver); break;
                    // DamageVsCcMul · MaxHealthMul : 판정 제외(헤더)
                }
            }
            if (!any) return false;

            // 재생은 기준이 0 이다(배율 스탯은 1).
            float rNet = rHasOver ? rOver : rAdd * rMul;
            return DeviatesFromOne(Net(dHasOver, dOver, dAdd, dMul))
                || DeviatesFromOne(Net(aHasOver, aOver, aAdd, aMul))
                || DeviatesFromOne(Net(tHasOver, tOver, tAdd, tMul))
                || DeviatesFromOne(Net(mHasOver, mOver, mAdd, mMul))
                || rNet > Eps || rNet < -Eps;
        }

        /// <summary>드림캐쳐가 건 스탯이 살아 있나 — 옛 이름의 짝(강화 오라).</summary>
        public static bool HasActiveDreamcatcherModifier(IReadOnlyList<ModifierSlot> slots)
            => HasActiveModifier(slots, ModifierOrigin.Dreamcatcher);

        /// <summary>
        /// 그 출처의 슬롯이 **하나라도** 있나(net 편차를 안 본다). 번아웃처럼 회수가 만료뿐이고
        /// 크기가 언제나 기준 밖인 출처의 표식이 쓴다 — 옛 판정도 「남은 시간 &gt; 0 인 슬롯 존재」였다.
        /// </summary>
        public static bool HasAnyFromOrigin(IReadOnlyList<ModifierSlot> slots, ModifierOrigin origin)
        {
            if (slots == null) return false;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Origin == origin && slots[i].Remaining > 0f) return true;
            return false;
        }

        private static void Accumulate(CombineOp op, float magnitude, ref float add, ref float mul,
                                       ref float over, ref bool hasOver)
        {
            if (op == CombineOp.Multiplicative) mul *= magnitude;
            else if (op == CombineOp.Additive) add += magnitude;
            else { over = magnitude > over ? magnitude : over; hasOver = true; }
        }

        // 방향 보존용 net(클램프 불요). override 우선, 아니면 (1+add)×mul — 코어 결합식과 같은 모양.
        private static float Net(bool hasOver, float over, float add, float mul)
            => hasOver ? over : (1f + add) * mul;

        private static bool DeviatesFromOne(float v) => v > 1f + Eps || v < 1f - Eps;
    }
}
