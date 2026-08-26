using Unity.Mathematics;

namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 8 — **화염 브레스.** 정면 부채꼴에 즉발 피해.
    //
    // ⚠ **투사체를 만들지 않는다.** 즉발이고 대상이 「지금 부채꼴 안에 있는 것」이라,
    // 캐리어를 띄우면 비행 한 프레임 사이에 답이 달라진다. 레거시도 같은 이유로
    // 그 프레임의 후보 배열을 그 자리에서 훑었다.
    //
    // ⚠ **방향은 감지자가 정한다.** 「내가 지금 겨눈 곳」은 타겟팅 규칙의 결과이고
    // 그 규칙은 `AttackSystem` 이 소유한다 — 여기서 다시 고르면 사본이 된다.
    // 그래서 축은 `DirectionXZ` 로 실려 온다(그 필드가 «계산된 방향» 이라 불리는 이유).
    //
    // ⚠ **cos²θ 는 자기 축(`ConeCosSq`)을 갖는다.** `HitThreshold`(투사체 도달 반경)에
    // 겸직시키지 않았다 — 오늘은 한 payload 가 둘 중 하나만 쓰지만, 콘을 쏘는 투사체가
    // 생기는 순간 한 필드가 두 뜻으로 갈린다(DoT 슬롯이 그렇게 과피해를 냈다).
    // 저작은 도(degree)이고 런타임 변환은 bake 1회 — sim 은 삼각함수를 부르지 않는다.
    public sealed class ConeBreathSkill : ISkill
    {
        public const int Id = 34;
        public int SkillId => Id;

        private const int MaxTargets = 64;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            float damage = p.Magnitude;
            if (damage <= 0f || p.TileRange <= 0) return;

            float2 dir = target.DirectionXZ;
            // 축이 없으면 안 쏜다. 지어내면 저작·감지 실수가 «엉뚱한 방향으로 뿜는»
            // 형태로 조용히 살아남는다.
            if (math.lengthsq(dir) < 1e-6f) return;
            dir = math.normalize(dir);

            var hostPos = ctx.Position(caster.Unit);
            float rangeWorld = p.TileRange * ctx.TileSize;

            // 레거시 필터 넷 중 셋이 여기로 접힌다:
            //   ① 진영 마스크        → `Opponents`(호출자 상대)
            //   ② 통행 층            → `MatchTraversalLayers`
            //   ③ 자기 제외          → `ExcludeSelf`
            // 넷째(부채꼴)만 아래에서 직접 본다 — 사거리는 «반경» 이 아니라 «콘» 이라
            // 후보 질의에 못 맡긴다. 그래서 질의 반경은 콘의 **외접 반경**으로 넉넉히
            // 잡고 진짜 판정을 콘이 한다.
            var buf = new SkillEntityId[MaxTargets];
            int n = ctx.Opponents(
                caster, hostPos, p.TileRange,
                CandidateFilter.ExcludeSelf | CandidateFilter.ExcludeDead
                    | CandidateFilter.MatchTraversalLayers,
                RangeMetric.Chebyshev, buf);

            var fromXZ = new float2(hostPos.x, hostPos.z);
            for (int i = 0; i < n; i++)
            {
                var pos = ctx.Position(buf[i]);
                if (!SkillCone.IsInCone(fromXZ, new float2(pos.x, pos.z),
                                        dir, p.ConeCosSq, rangeWorld)) continue;

                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.DealDamage,
                    Source = caster.Unit,
                    Target = buf[i],
                    Amount = damage,
                });
            }
        }
    }
}
