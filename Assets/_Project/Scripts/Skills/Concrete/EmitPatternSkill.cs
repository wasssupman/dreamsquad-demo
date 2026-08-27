using Unity.Mathematics;

namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 1 — 발사 명세(패턴)를 쏜다.
    //
    // 이 스킬이 하는 판단은 **하나뿐이다: 어디를 쏘나.** 몇 발을 어떤 간격으로
    // 어떤 궤적으로 뿌리는지는 발사 명세(`EmitterSpec`)가 이미 저작으로 갖고 있고,
    // 그것을 굴리는 것은 emitter 시스템이다(계약 5 — 스킬은 「시작한다 + 수치」까지).
    //
    // ⚠ **성사와 카운터 전진은 원자다**(unit 0 미결 4의 결론). 발사 명세는 발사할
    // 때마다 전진하는 카운터를 갖는다 — 그게 「이번엔 어느 총구」를 정하기 때문에,
    // 쏘지 않았는데 전진하면 위상이 밀리고 쐈는데 안 전진하면 선택 규칙이 고정된다.
    // 그래서 이 스킬은 **쏠 수 없다고 판단하면 의도를 아예 방출하지 않는다.** 어댑터는
    // 의도 하나당 「전진 + 인스턴스 추가」를 붙여서 한다. 그 둘이 갈릴 자리가 없다.
    //
    // ⚠ **조준은 여기서 확정한다.** 방향 바인딩 탄의 원점·방향·최대거리는 «발사 시점의
    // 값»이라 저작이 미리 채울 수 없다. 안 채우면 방향 (0,0) 인 탄이 나가고, 그건
    // 조용하다 — 발사 연출은 그대로 나오기 때문이다.
    //
    // 이미 조준된 명세는 **건드리지 않는다**(포트가 `Preaimed` 로 답한다). 방향
    // 스냅샷을 미리 실어 보내는 소비자가 있고, 그쪽은 후보가 0이어도 발사한다.
    public sealed class EmitPatternSkill : ISkill
    {
        public const int Id = 7;
        public int SkillId => Id;

        // ⚠ **버퍼는 로컬이다.** concrete 는 필드를 갖지 않는다(토대 계약 5) —
        // 레지스트리가 static 이라 인스턴스가 월드 사이에도 공유되고, 필드를 들면
        // 그 공유가 곧 상태 공유가 된다. 형제 다섯도 전부 로컬로 잡는다.
        //
        // 상한을 넘는 후보는 버린다 — 이 규칙은 **최근접 하나**만 고르므로, 잘림이
        // 답을 바꾸려면 「상한 밖에 더 가까운 후보」가 있어야 하는데 풀 순서가
        // 거리순이 아니라 그럴 수 있다. 오늘 라이브 저작(사거리 2~4)에선 반경 안
        // 후보가 64를 넘지 못해 도달 불가다 — 장거리 조준 저작이 생기면 이 상한을
        // 계약으로 다시 정해야 한다(README 잔여 리스크 등재).
        private const int MaxCandidates = 64;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new EmitPatternParams(p);

            var need = ctx.AimNeedOfPattern(caster.Unit, a.PatternIndex);
            if (need == PatternAimNeed.Missing) return;   // 그런 명세가 없다 — 불발

            if (need == PatternAimNeed.Preaimed)
            {
                // 조준이 이미 실려 있다. 템플릿을 그대로 두고 쏜다.
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.EmitPattern,
                    Source = caster.Unit,
                    PatternIndex = a.PatternIndex,
                });
                return;
            }

            // ── 조준해야 한다 ──────────────────────────────────────
            // ⚠ **위치를 모르면 쏘지 않는다.** 이 가드를 조준 단계 «안» 이 아니라
            // 앞에 두면, 위치 없는 host 가 조준을 통째로 건너뛰고 방향 (0,0) 탄을
            // 내보낸다 — 이 축이 없애려던 바로 그 증상이다.
            if (!ctx.Has(caster.Unit, UnitPredicate.HasPosition)) return;

            var origin = ctx.Position(caster.Unit);
            var hostXZ = new float2(origin.x, origin.z);

            bool hasAim = ctx.TryFacing(caster.Unit, out float2 aim)
                          && math.lengthsq(aim) > SkillAim.AimEpsilonSq;

            // 조준이 없을 때만 후보를 본다 — 조준이 방향을 이미 정했으면 풀을 만들 이유가 없다.
            // ⚠ 조준이 있으면 후보를 안 보므로 **버퍼도 안 잡는다**(ECS 리뷰 N-2).
            // 주기 × 조준 저작이 생기면 이 할당이 매 주기 돈다.
            int n = 0;
            float2[] candXZ = null;
            if (!hasAim)
            {
                candXZ = new float2[MaxCandidates];
                var cand = new SkillEntityId[MaxCandidates];
                // 진영은 caster 에서 파생된다 — 「적」을 이름으로 부르지 않는 이유다.
                // 진영 미상이면 포트가 0을 돌려주고, 아래 `TryResolve` 가 false 를 내
                // 자연히 불발된다.
                //
                // ⚠ 자는 **유클리드**다. 셀 체비셰프로 고르면 대각선 끝 칸의 후보가
                // 「후보」이면서 사거리 밖이라(3칸 → 실거리 4.24 > 3.0), 그 적이 유일
                // 후보일 때 조준은 성립하고 탄은 도중에 소멸한다.
                int found = ctx.Opponents(
                    caster, origin, a.Range,
                    CandidateFilter.ExcludeDead
                    | CandidateFilter.ExcludeInUltimateLeap
                    | CandidateFilter.MatchTraversalLayers,
                    RangeMetric.Euclidean, cand);

                for (int i = 0; i < found && n < MaxCandidates; i++)
                {
                    var q = ctx.Position(cand[i]);
                    candXZ[n++] = new float2(q.x, q.z);
                }
            }

            if (!SkillAim.TryResolve(hostXZ, hasAim, aim, candXZ, n, out float2 dir, out _))
                return;   // 조준도 합법 후보도 없다 — 사건을 없던 것으로 한다

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.EmitPattern,
                Source = caster.Unit,
                PatternIndex = a.PatternIndex,
                Position = origin,
                DirectionXZ = dir,
                // 최대 비행 거리는 조준 후보를 본 자와 **같은 자**여야 한다.
                TileRange = a.Range,
            });
        }
    }
}
