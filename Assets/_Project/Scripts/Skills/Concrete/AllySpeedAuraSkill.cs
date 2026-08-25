namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 0 — 채찍질. 나이트메어가 자기 편을 몰아세운다.
    //
    // 자장가와 **대칭**인 스킬이다: 같은 반경 선별인데 대상이 반대편이 아니라 **같은 편**이고,
    // 주는 것이 수면이 아니라 이속이다. 예전엔 그 대칭이 코드에서 안 보였다 — 둘 다
    // `hostIsEnemy ? A : B` 삼항이라 어느 쪽이 뒤집힌 건지 읽어서 알 수 없었다.
    // 이제 한쪽은 `Opponents`, 한쪽은 `Allies` 라고 적혀 있다.
    //
    // ⚠ **펄스 오라다.** 매 발동이 TTL 을 새로 주고, 저작 계약이 `duration > periodSeconds`
    // 라 갱신이 끊기지 않는다. host 가 죽거나 대상이 반경을 벗어나면 TTL 이 자연 만료된다 —
    // 회수(revoke) 경로가 없는 것이 의도다.
    public sealed class AllySpeedAuraSkill : ISkill
    {
        public const int Id = 2;
        public int SkillId => Id;

        private const int MaxTargets = 64;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new AuraParams(p);
            // degenerate 저작(배율 1.0 / TTL 없음)은 조용히 소모한다 — 발동은 일어났고
            // 아무 일도 안 한 것이 저작의 결과다.
            if (a.PercentDelta == 0f || a.Ttl <= 0f) return;

            var hostPos = ctx.Position(caster.Unit);
            var buf = new SkillEntityId[MaxTargets];
            int n = ctx.Allies(caster, hostPos, a.Radius,
                CandidateFilter.ExcludeSelf, RangeMetric.Chebyshev, buf);
            if (n == 0) return;

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
                    Selector = (int)SkillStatKind.MoveSpeedMul,
                    Op = SkillCombineOp.Multiplicative,
                    Origin = SkillModifierOrigin.Boss,
                    Amount = mul,
                    Duration = a.Ttl,
                    StackId = 0,
                });
            }

            // **효과 없는 연출 금지.** 한 명도 못 버프했으면 안 튼다(위 early return).
            // `DataIndex < 0` 은 무연출 저작이다 — blink 선례.
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
}
