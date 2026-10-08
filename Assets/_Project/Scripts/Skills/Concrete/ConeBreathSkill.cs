using Unity.Mathematics;

namespace Somnia.Battle.Skills.Concrete
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
    // ⚠ **반각은 자기 축(`ConeSinHalf`·`ConeCosHalf`)을 갖는다.** `HitThreshold`(투사체 도달 반경)에
    // 겸직시키지 않았다 — 오늘은 한 payload 가 둘 중 하나만 쓰지만, 콘을 쏘는 투사체가
    // 생기는 순간 한 필드가 두 뜻으로 갈린다(DoT 슬롯이 그렇게 과피해를 냈다).
    // 저작은 도(degree)이고 런타임 변환은 bake 1회 — sim 은 삼각함수를 부르지 않는다.
    //
    // ★ **도달은 정본 자 하나다**(제약 13 · unified-effect-layer unit 7 · 사용자 결정 2026-09-28 「다른 모든 것들과
    // 규칙을 맞춰」). 형 = **몸에서 나오는 것**(브레스는 드래곤 몸에서 뻗는다):
    //   · 길이 = 후보 질의의 원(`RangeMetric.SelfArea` = 사거리 + 드래곤 몸 + 대상 몸). **두 번째 길이 컷을 두지 않는다** —
    //     옛 판정은 몸 없는 중심 거리로 다시 잘라 몸 큰 적이 사거리 끝에 걸쳐도 안 맞았다.
    //   · 방향 = `SkillMath.SectorGate`(다른 방향 도형 — 브루저·말파이트 부채꼴 `AttackReach.InReachShaped` 와 같은 게이트).
    //     대상 **몸이 쐐기에 걸치면** 맞는다. 중심점만 보던 옛 판정은 가장자리에 걸친 큰 적을 놓쳤다.
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

            var hostPos = target.Origin.LaunchSite;   // 발사 자리 — 브레스는 여기서 편다

            // 레거시 필터 넷이 전부 여기로 접힌다:
            //   ① 진영 마스크        → `Opponents`(호출자 상대)
            //   ② 통행 층            → `MatchTraversalLayers`
            //   ③ 자기 제외          → `ExcludeSelf`
            //   ④ 사거리(길이)       → `RangeMetric.SelfArea` 의 원 — 방향만 아래 게이트가 곱한다
            var buf = new SkillEntityId[MaxTargets];
            int n = ctx.Opponents(
                caster, hostPos, p.TileRange,
                CandidateFilter.ExcludeSelf | CandidateFilter.ExcludeDead
                    | CandidateFilter.MatchTraversalLayers,
                RangeMetric.SelfArea, buf);

            // 게이트 프레임은 타일 단위 — `along` = 조준 방향 성분, `across` = 그 수직 성분(부호는 게이트가 접는다).
            float inv = ctx.TileSize > 1e-6f ? 1f / ctx.TileSize : 1f;
            for (int i = 0; i < n; i++)
            {
                var pos = ctx.Position(buf[i]);
                float dx = (pos.x - hostPos.x) * inv, dz = (pos.z - hostPos.z) * inv;
                float along = dir.x * dx + dir.y * dz;
                float across = dir.x * dz - dir.y * dx;
                // 같은 자리(겹침)는 게이트가 «쐐기 안» 으로 읽는다(along = across = 0).
                if (!SkillMath.SectorGate(along, across, p.ConeSinHalf, p.ConeCosHalf,
                                          ctx.Stat(buf[i], UnitStat.BodyRadius))) continue;

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
