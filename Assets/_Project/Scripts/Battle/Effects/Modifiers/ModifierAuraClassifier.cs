namespace Wassup.Battle.Effects
{
    // unit-buff-debuff-aura Unit 0 — 순 버프/디버프 판정(순수 함수). ModifierStats(집계 캐시)를
    // base identity 와 비교해 두 bool(buffed/debuffed, 독립)로 분류한다. BattleBridge reconcile 이
    // 이 결과로 Buffed/Debuffed 오라를 Ensure 한다. 아키텍처-blind: in ModifierStats(POD unmanaged)만
    // 받고 EntityManager/View 불요 — EditMode 로 검증(CLAUDE.md 제약 10).
    //
    // 스탯별 buff 방향이 다르다:
    //  · damageMul/attackSpeedMul/moveSpeedMul : >1 buff, <1 debuff
    //  · dmgTakenMul : 역방향 — <1 buff(피해 감소), >1 debuff(피해 증가)
    //  · regenPerSec : base 0, 비음수 클램프(ModifierStatsAggregateSystem) → >0 buff 전용, 디버프 없음
    //  · damageVsCcMul : 판정 제외 — "CC 걸린 적 대상"에만 작동하는 조건부 배율이라 상시 오라는 상태 오도
    public static class ModifierAuraClassifier
    {
        public const float Eps = 1e-4f;

        public static void Classify(in ModifierStats s, out bool buffed, out bool debuffed)
        {
            buffed =
                s.damageMul      > 1f + Eps ||
                s.attackSpeedMul > 1f + Eps ||
                s.moveSpeedMul   > 1f + Eps ||
                s.dmgTakenMul    < 1f - Eps ||   // 역방향: 피해 감소 = 버프
                s.regenPerSec    > Eps;          // base 0 → 양수면 버프 (디버프 방향 없음)

            debuffed =
                s.damageMul      < 1f - Eps ||
                s.attackSpeedMul < 1f - Eps ||
                s.moveSpeedMul   < 1f - Eps ||
                s.dmgTakenMul    > 1f + Eps;      // 역방향: 피해 증가 = 디버프
        }
    }
}
