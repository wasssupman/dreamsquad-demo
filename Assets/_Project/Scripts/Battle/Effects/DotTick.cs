namespace Wassup.Battle.Effects
{
    // dot-tick-cadence unit 1 — DoT 이산 tick 누적 산식(순수·Burst 호환).
    // plain 값 입출력, 아키텍처-blind. ECS/Mono 를 모른다. EditMode 로 검증(제약 10 모범).
    public static class DotTick
    {
        // 안전 상한: 극단적 dt/미세 interval 에서의 무한루프 방지. 결정론 유지, 실사용 도달 불가.
        public const int MaxTicksPerFrame = 1024;

        // tickTimer 를 dt 만큼 진행하고, 이번 프레임 지급할 청크(=tick) 수를 반환한다.
        // tickInterval<=0 은 연속 DoT 전제(호출측이 별도 처리) — 방어적으로 0 반환하고 timer 불변.
        public static int Advance(ref float tickTimer, float tickInterval, float dt)
        {
            if (tickInterval <= 0f)
                return 0;

            tickTimer += dt;
            int ticks = 0;
            while (tickTimer >= tickInterval && ticks < MaxTicksPerFrame)
            {
                tickTimer -= tickInterval;
                ticks++;
            }
            return ticks;
        }
    }
}
