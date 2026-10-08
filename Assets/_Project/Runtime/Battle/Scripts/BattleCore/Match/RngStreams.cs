using Unity.Mathematics;
using Somnia.Battle.Core;

namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 1 — 난수. 계열 6개, 전부 `MatchSeed.Derive*` 에서 나온다.
    //
    // 계열을 나누는 이유(옛 `match-seed-unification` 계승): 한 스트림을 공유하면 한쪽의
    // 호출 횟수가 바뀔 때 다른 쪽 결과가 통째로 밀린다 — 「웨이브를 안 건드렸는데 맵이
    // 바뀌었다」가 그 증상이다. salt 상수는 옛것을 그대로 물려받아 **같은 시드가 같은
    // 계열값**을 내게 한다.
    //
    // `System.Random` 을 쓰지 않는다 — 구현이 플랫폼·런타임 버전에 매여 결정론 보장이
    // 없다. `Unity.Mathematics.Random`(xorshift)은 순수 값 타입이고 엔진에 의존하지 않는다.
    public sealed class RngStreams
    {
        public Random Map;
        public Random Wave;
        public Random Visual;
        public Random Pickup;
        public Random Gimmick;
        public Random Meteor;

        public RngStreams(int matchSeed)
        {
            Reset(matchSeed);
        }

        public void Reset(int matchSeed)
        {
            Map = Make(MatchSeed.DeriveMapSeed(matchSeed));
            Wave = Make(MatchSeed.DeriveWaveSeed(matchSeed));
            Visual = Make(MatchSeed.DeriveVisualSeed(matchSeed));
            Pickup = Make(MatchSeed.DerivePickupSeed(matchSeed));
            Gimmick = Make(MatchSeed.DeriveGimmickSeed(matchSeed));
            Meteor = Make(MatchSeed.DeriveMeteorSeed(matchSeed));
        }

        // `Random` 은 state 0 을 금지한다. `MatchSeed.Mix` 가 0 아닌 int 를 보장하지만,
        // 그 보장이 언젠가 바뀌어도 여기서 죽지 않게 한 겹 더 막는다.
        private static Random Make(int derived)
        {
            uint s = unchecked((uint)derived);
            return new Random(s != 0u ? s : 1u);
        }
    }
}
