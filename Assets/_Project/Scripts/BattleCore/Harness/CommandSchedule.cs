using System.Collections.Generic;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 「몇 번째 틱에 무슨 커맨드」. 하네스 시나리오의 입력이다.
    //
    // 플레이어 입력을 결정론으로 만드는 유일한 방법이 이것이다 — 실시간 입력은 프레임에
    // 매여 재현이 안 되지만, 「틱 t 에 이 커맨드」는 값이다. 옛 하네스의 `placementTicks`
    // 가 같은 일을 했고, 그게 골든이 성립하는 이유였다.
    public sealed class CommandSchedule
    {
        private readonly struct Entry
        {
            public readonly int Tick;
            public readonly int Seq;
            public readonly Command Command;

            public Entry(int tick, int seq, in Command command)
            {
                Tick = tick;
                Seq = seq;
                Command = command;
            }
        }

        private readonly List<Entry> _entries = new List<Entry>(16);
        private int _seq;
        private int _cursor;

        public int Count => _entries.Count;

        /// <summary>
        /// 커맨드를 건다. 같은 틱에 여럿이면 **건 순서**대로 적용된다 —
        /// 시간만으로는 전순서가 안 되므로 순번을 함께 든다.
        /// </summary>
        public CommandSchedule Add(int tick, in Command command)
        {
            var e = new Entry(tick, _seq++, command);
            int at = _entries.Count;
            while (at > 0 && _entries[at - 1].Tick > e.Tick) at--;
            _entries.Insert(at, e);
            return this;
        }

        public void Rewind() => _cursor = 0;

        /// <summary>
        /// 이 틱의 커맨드를 순서대로 적용한다. 커서가 앞으로만 가므로 전체가 O(n) 이다.
        /// 거절된 receipt 는 **버리지 않고** 호출자에게 돌려준다(시나리오가 거절을 의도할
        /// 수 있다 — 제출 잠금 테스트가 그렇다).
        /// </summary>
        public void ApplyDue(BattleMatch match, int tick, List<Receipt> receipts)
        {
            while (_cursor < _entries.Count && _entries[_cursor].Tick <= tick)
            {
                var e = _entries[_cursor++];
                var r = match.Apply(e.Command);
                receipts?.Add(r);
            }
        }
    }
}
