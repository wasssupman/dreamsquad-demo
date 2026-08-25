using Unity.Mathematics;

namespace Wassup.Skills.Concrete
{
    // skill-layer-foundation unit 5 — 첫 concrete. 자장가.
    //
    // 이 파일 하나가 「자장가가 무슨 일을 하나」의 답이다. 예전엔 그 답이
    // `BossPeriodicTriggerSystem` 733줄 한복판의 40줄이었고, 그 40줄을 읽으려면
    // 후보 풀 지연 생성·진영 삼항·슬롯 스칼라 겸직을 먼저 통과해야 했다.
    //
    // **호출자가 곧 소유자다.** 이 클래스는 진영도 host 종류도 모른다 — 마메모가
    // 쓰던 것을 방어유닛이 부르면 상대 진영을 재운다. 코드 0줄로.
    //
    // ⚠ **무상태**(계약 5). 필드가 없다. 재우는 것까지가 스킬이고, 잠이 언제 깨는지는
    // `CcApplySystem`·wake-on-hit 이 소유한다.
    public sealed class AreaSleepSkill : ISkill
    {
        public const int Id = 1;
        public int SkillId => Id;

        // 후보 버퍼 상한. 무상태 계약(필드 금지)이라 호출마다 스택에 잡는다.
        // 발동이 초당 수 회라 GC 압력은 무시할 만하다(리뷰에서 확인).
        // ⚠ 상한을 넘으면 **가까운 순이 아니라 풀 순서 선착**으로 잘린다 — legacy `AuraPulse`
        // 는 무상한이었다. 반경 안 후보가 64를 넘는 판이 실제로 생기면 그때 계약을 정한다.
        private const int MaxCandidates = 64;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new AreaSleepParams(p);
            if (a.SleepCount < 1 || a.Radius < 1 || a.Duration <= 0f) return;

            var hostPos = ctx.Position(caster.Unit);
            var hostCell = ctx.CellOf(caster.Unit);

            // **전 범위**가 후보다. 제외는 「내가 지금 때릴 대상」뿐이고 그건 아래
            // rank 로 뺀다.
            //
            // ⚠ 여기를 도넛(안쪽 반지름 = 사거리)으로 만들면 **능력이 죽는다.** 보스는
            // 사냥해서 붙기 때문에 조우의 대부분을 사거리 안에서 보내고, 도넛은 접근
            // 중에만 점유된다 — 실측에서 3.5초 주기인데 조우당 1회밖에 안 터졌다.
            // 사용자 보고 "재우는 효과가 발생하지 않는다" 의 실체가 그것이었다.
            var candidates = new SkillEntityId[MaxCandidates];
            int n = ctx.Opponents(
                caster, hostPos, a.Radius,
                CandidateFilter.ExcludeSelf | CandidateFilter.ExcludeDead
                    | CandidateFilter.ExcludePendingDeployment,
                RangeMetric.Chebyshev, candidates);
            if (n == 0) return;

            // 거리² 로 좁힌 뒤 cap. 배제는 여기서 끝나야 cap 자리를 죽은/배치중 유닛이
            // 차지하지 않는다(위 필터가 그 역할).
            var distSq = new float[n];
            for (int i = 0; i < n; i++)
            {
                var d = ctx.Position(candidates[i]) - hostPos;
                distSq[i] = d.x * d.x + d.z * d.z;
            }

            // **「내가 때릴 대상」만 rank 로 뺀다.**
            // host 가 이번 공격에 때릴 수 있는 수 = attackTargetCount 이고, 공격은 사거리 안
            // **가까운 순**으로 고른다. 그래서 거리 오름차순의 **앞에서부터 그 수만큼**,
            // 그리고 **사거리 안일 때만** 건너뛰면 «재우자마자 자기가 깨우는» 자리만
            // 정확히 빠진다. 링 전체를 빼면 붙은 보스의 후보가 통째로 마른다.
            int skipCount = (int)math.max(0f, ctx.Stat(caster.Unit, UnitStat.AttackTargetCount));
            int attackTiles = SkillMath.RangeToTiles(ctx.Stat(caster.Unit, UnitStat.AttackRange));

            // 뺄 만큼 더 뽑아야 실제 재우는 수가 cap 을 유지한다.
            var picked = new int[MaxCandidates];
            int m = SkillMath.SelectNearest(distSq, n, a.SleepCount + skipCount, picked);

            int slept = 0, skipped = 0;
            for (int i = 0; i < m && slept < a.SleepCount; i++)
            {
                var id = candidates[picked[i]];
                if (skipped < skipCount)
                {
                    var c = ctx.CellOf(id);
                    if (SkillMath.ChebyshevDistance(c.x, c.y, hostCell.x, hostCell.y) <= attackTiles)
                    {
                        skipped++;
                        continue;
                    }
                }

                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.ApplyCc,
                    Target = id,
                    Duration = a.Duration,
                    Selector = (int)SkillCcKind.Sleep,
                });
                slept++;
            }

            // legacy 와 같다 — **실제로 잰 펄스만** 연출한다(효과 없는 연출 금지).
            // 리뷰 M1: 이전하면서 빠뜨렸다. 오늘 마메모는 무연출 저작이라 회귀가 없었지만,
            // 저작하는 순간 조용히 무시됐을 자리다.
            if (slept > 0 && p.HasData)
            {
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.PlayVisual,
                    Selector = (int)SkillVisualKind.HitPulse,
                    Position = hostPos,
                    DataIndex = p.DataIndex,
                    Source = caster.Unit,
                });
            }
        }

    }
}
