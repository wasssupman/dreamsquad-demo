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

        // 후보 버퍼. 무상태 계약을 지키려면 필드로 들 수 없어 호출마다 받는다 —
        // 디스패처가 프레임당 하나를 재사용한다(할당은 스킬의 일이 아니다).
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
            int attackTiles = SkillMath.RangeToTiles(
                ctx.Stat(caster.Unit, UnitStat.AttackRange), TileSizeOf(ctx));

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
        }

        // 타일 크기는 격자 파라미터라 포트가 안다. 셀 중심 두 개의 x 차이가 곧 한 칸이다 —
        // 이걸 위해 동사를 하나 더 여는 것보다 이 파생이 싸다(제약 8).
        private static float TileSizeOf(ISkillContext ctx)
        {
            var a = ctx.CellCenter(new int2(0, 0));
            var b = ctx.CellCenter(new int2(1, 0));
            return math.abs(b.x - a.x);
        }
    }
}
