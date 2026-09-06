namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 1 — 범위 도발. host 반경 안 상대 진영 전원을
    // duration 초 동안 자기에게 붙인다.
    //
    // ⚠ **게이트를 복제하지 않는다.** 보스 면역 · 유닛 미조준 적 · 공격 수단 부재 ·
    // 도달 불가 판정은 전부 어그로 시스템 소유다. 여기서 미리 걸러도 같은 판정이 두 곳에
    // 생기고, 둘이 갈리는 순간 한쪽만 고쳐진다. 이 스킬이 소유하는 것은 **누구를 부르나**
    // (반경 · 상대 진영 · 이번 프레임 합법 후보)까지고, **불려온 뒤 어떻게 되나**는 아니다.
    //
    // ⚠ 통행 층 게이트만은 여기서 건다 — 빼면 **근접 가디언이 하늘의 적을 끌어온다**.
    // 이건 「불려온 뒤」가 아니라 「부를 수 있나」라서 후보 조건이 맞다.
    public sealed class AreaTauntSkill : ISkill
    {
        public const int Id = 8;
        public int SkillId => Id;

        // ⚠ **버퍼는 로컬이다**(토대 계약 5 — concrete 는 필드를 갖지 않는다).
        // 도발은 「최근접 하나」가 아니라 **집합**이라 잘림이 곧 결과 차이다 —
        // legacy arm 은 무상한이었다. 라이브 저작(반경 2 = 25셀)에선 도달 불가지만
        // 반경이 커지면 이 상한이 조용히 대상을 줄인다(README 잔여 리스크 등재).
        private const int MaxTargets = 64;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new TauntParams(p);

            // 퇴화 저작은 발동을 조용히 소모한다(계약 6). 저작 검증은 bake 가 loud 로 한다.
            if (a.Duration <= 0f || a.Radius <= 0) return;
            // 가디언 표식이 없으면 도발할 자격이 없다 — 어그로가 「누구에게」 붙는지가
            // 그 용량에 매여 있다.
            if (!ctx.Has(caster.Unit, UnitPredicate.HasAggroCapacity)) return;
            if (!ctx.Has(caster.Unit, UnitPredicate.HasPosition)) return;

            var center = ctx.Position(caster.Unit);
            var buf = new SkillEntityId[MaxTargets];
            int n = ctx.Opponents(
                caster, center, a.Radius,
                CandidateFilter.ExcludeDead
                | CandidateFilter.ExcludeInUltimateLeap
                | CandidateFilter.MatchTraversalLayers,
                RangeMetric.SelfArea, buf);

            for (int i = 0; i < n; i++)
            {
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.Taunt,
                    Source = caster.Unit,   // 가디언 — 어그로가 붙는 대상
                    Target = buf[i],
                    Duration = a.Duration,
                });
            }
        }
    }
}
