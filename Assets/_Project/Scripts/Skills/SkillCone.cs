namespace Wassup.Skills
{
    // skill-layer-migration unit 8 — 콘(부채꼴) 판정의 자리. `TileAoe.IsInCone` 에서 이사했다.
    //
    // unified-effect-layer unit 7 — **콘 판정 `IsInCone` 은 은퇴했다.** 몸 없는 중심 거리로 길이를 다시 자르고
    // 각도를 대상 중심점으로만 봐 제약 13(도달 산식 하나)을 어겼다. 화염 브레스는 이제 후보 질의의 원
    // (`RangeMetric.SelfArea`) AND `SkillMath.SectorGate`(다른 방향 도형과 같은 게이트)로 잰다
    // (`Concrete/ConeBreathSkill`). 남은 것은 방향 도형 호출부가 같이 쓰는 «같은 자리» 임계 하나다.
    public static class SkillCone
    {
        // «같은 자리» 임계(월드 거리²). 0.01 월드 유닛 = 타일 1개 기준 1% — 셀 판정을
        // 흔들지 않으면서 방향 계산이 의미를 잃는 구간만 잡는다(`AttackReach.InReachShaped`).
        public const float SameSpotEpsSq = 1e-4f;
    }
}
