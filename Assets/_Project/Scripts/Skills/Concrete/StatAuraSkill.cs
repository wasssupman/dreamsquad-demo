namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 2b — 반경 안 대상에게 스탯 모디파이어를 TTL 로 얹는다.
    //
    // **한 구현이 셋을 덮는다.** 예전엔 「아군 이동속도」 하나가 concrete 를 통째로
    // 차지했는데, 실제로 다른 것은 축 둘뿐이었다 — **누구에게**(아군/상대)와
    // **무슨 스탯**. 나머지(퍼센트→배율 변환·반경·TTL·효과 없으면 무연출)는 전부 같다.
    // 그래서 로직은 여기 하나만 두고 파생은 그 두 축만 선언한다(디스패처 seam 과 같은 형태).
    //
    // ⚠ 해제는 **TTL 만료 하나뿐**이다(계약 5). 반경 이탈도 host 사망도 회수하지 않는다 —
    // 회수는 「제거」가 아니라 같은 병합 키로 항등을 재발행하는 중립화라, 그 축을 열면
    // 병합 키 계약이 따라와야 한다. 오늘 어느 소비자도 그것을 요구하지 않는다.
    public abstract class StatAuraSkill : ISkill
    {
        public abstract int SkillId { get; }

        // 파생이 선언하는 두 축.
        protected abstract bool TargetsAllies { get; }
        // `null` = 저작이 정한다. 값이 있으면 **저작을 무시하고 고정**한다 —
        // 「이동속도 오라」처럼 payload 이름이 이미 스탯을 말하는 경우가 그것이다.
        protected virtual SkillStatKind? FixedStat => null;

        // ⚠ **출처는 병합 키의 일부다.** 파생이 자기 것을 선언한다 — 하나로 묶으면
        // 보스 채찍과 배치 오라가 같은 키를 공유해 서로를 덮는다.
        protected virtual SkillModifierOrigin ModifierOrigin => SkillModifierOrigin.OnPlace;

        // ⚠ **결합 버킷도 파생 축이다**(skill-layer-migration unit 4d 에서 발견).
        // 처음엔 셋 다 `Multiplicative` 로 묶었는데 그건 보스 채찍의 레거시였고,
        // **배치 오라 둘의 레거시는 가산이었다**(`EnqueueStatModifier` → `FromMultiplier`).
        // 버킷이 다르면 다른 버프와 쌓이는 방식이 달라진다 — 가디언 오라(+30%) 위에
        // 카드 +70% 를 얹으면 가산은 2.0, 곱셈은 2.21 이다.
        // `CrackedGrail_RevokeNeutralizesBothAdditiveEffects` 가 그 차이를 잡았다.
        //
        // 배치 오라가 `FromAuthoredMultiplier` 인 것이 우연한 일치가 아니다 — 레거시가
        // **같은 번역 함수**를 썼으므로 이 값을 쓰는 한 감소 배율(궁수의 0.6)까지
        // 자동으로 같은 버킷에 간다.
        protected virtual SkillCombineOp CombineOp => SkillCombineOp.FromAuthoredMultiplier;

        // 자기 자신도 받나. 보스 채찍은 **뺀다**(기존 계약), 가디언은 **넣는다**
        // (레거시 arm 의 명시 결정 — "자율성 · 더 강한 피드백"). 상대 오라는 애초에
        // 자기가 후보 풀에 없어 이 축이 의미 없다.
        protected virtual bool IncludesSelf => false;

        private const int MaxTargets = 64;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new AuraParams(p);
            // degenerate 저작(배율 1.0 / TTL 없음)은 조용히 소모한다 — 발동은 일어났고
            // 아무 일도 안 한 것이 저작의 결과다.
            if (a.PercentDelta == 0f || a.Ttl <= 0f) return;

            var hostPos = ctx.Position(caster.Unit);
            var buf = new SkillEntityId[MaxTargets];
            // ⚠ 후보 게이트가 진영에 따라 다르다. 상대 오라만 통행 층을 본다 —
            // 「내가 못 때리는 층」을 감속시킬 수는 없기 때문이다(도발과 같은 판단).
            var allyFilter = CandidateFilter.ExcludeDead;
            if (!IncludesSelf) allyFilter |= CandidateFilter.ExcludeSelf;
            int n = TargetsAllies
                ? ctx.Allies(caster, hostPos, a.Radius, allyFilter, RangeMetric.Chebyshev, buf)
                : ctx.Opponents(caster, hostPos, a.Radius,
                                CandidateFilter.ExcludeDead
                                | CandidateFilter.ExcludeInUltimateLeap
                                | CandidateFilter.MatchTraversalLayers,
                                RangeMetric.Chebyshev, buf);
            if (n == 0) return;

            var stat = FixedStat ?? a.Stat;

            // 저작은 퍼센트다(20 = +20%). 배율 변환은 여기서 한 번 — 어댑터가 모디파이어를
            // 만들 때 또 하면 두 번 곱해진다.
            float mul = 1f + a.PercentDelta / 100f;

            for (int i = 0; i < n; i++)
            {
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.ApplyStatModifier,
                    Target = buf[i],
                    Source = caster.Unit,
                    Selector = (int)stat,
                    Op = CombineOp,
                    Origin = ModifierOrigin,
                    Amount = mul,
                    Duration = a.Ttl,
                    StackId = 0,
                });
            }

            // **효과 없는 연출 금지.** 한 명도 못 걸었으면 안 튼다(위 early return).
            // `DataIndex < 0` 은 무연출 저작이다.
            if (p.HasData)
            {
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.PlayVisual,
                    Position = hostPos,
                    DataIndex = p.DataIndex,
                    Source = caster.Unit,
                });
            }
        }
    }

    // 보스 채찍 — 아군 이동속도. **payload 이름이 이미 스탯을 말하므로 저작을 안 읽는다**
    // (그 슬롯들은 `buffStat` 을 안 채워 왔고, 읽기 시작하면 기본값 0 = 공격력이 되어
    //  채찍이 조용히 다른 오라가 된다).
    public sealed class AllySpeedAuraSkill : StatAuraSkill
    {
        public const int Id = 2;
        public override int SkillId => Id;
        protected override bool TargetsAllies => true;
        protected override SkillStatKind? FixedStat => SkillStatKind.MoveSpeedMul;
        protected override SkillModifierOrigin ModifierOrigin => SkillModifierOrigin.Boss;
        // 채찍의 레거시 arm 은 `op = CombineOp.Multiplicative` 를 **명시**했다.
        protected override SkillCombineOp CombineOp => SkillCombineOp.Multiplicative;
    }

    // 가디언 — 아군에게 저작한 스탯(오늘은 공격력).
    public sealed class AllyStatAuraSkill : StatAuraSkill
    {
        public const int Id = 9;
        public override int SkillId => Id;
        protected override bool TargetsAllies => true;
        // 레거시 `BoostNearbyDefenders` 가 자기를 포함했다 — 그 결정을 보존한다.
        protected override bool IncludesSelf => true;
    }

    // 궁수 — 상대에게 저작한 스탯(오늘은 이동속도 감쇠).
    public sealed class OpponentStatAuraSkill : StatAuraSkill
    {
        public const int Id = 10;
        public override int SkillId => Id;
        protected override bool TargetsAllies => false;
    }
}
