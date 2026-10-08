// salvaged from Assets/_Project/Scripts/Battle/Effects/DotTick.cs (battle-core-rebuild unit 6a)
//   규칙(이산 틱 누산 · 안전 상한)은 그대로다.
namespace Somnia.Battle.BattleCore.Effects
{
    // 지속 피해의 이산 틱 누산. plain 값 입출력이라 아키텍처를 모른다(제약 10 모범).
    public static class DotTick
    {
        /// <summary>
        /// 안전 상한. 극단적 dt·미세 주기에서의 무한 루프를 막는다 — 결정론을 유지하고
        /// 실사용으로는 도달하지 않는다.
        /// </summary>
        public const int MaxTicksPerFrame = 1024;

        /// <summary>
        /// 타이머를 `dt` 만큼 진행하고 **이번 틱에 지급할 청크 수**를 돌려준다.
        /// 주기 ≤ 0 은 연속 지속 피해 전제(호출부가 따로 처리)라 0 을 돌려주고 타이머를 안 건드린다.
        /// </summary>
        public static int Advance(ref float tickTimer, float tickInterval, float dt)
        {
            if (tickInterval <= 0f) return 0;

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
