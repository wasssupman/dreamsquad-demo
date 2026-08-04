using System;
using System.Collections.Generic;

namespace Wassup.Core
{
    // battle-sim-extraction unit 2 — sim tick 스케줄 입력 주입기.
    //
    // 입력을 벽시계가 아니라 tick 인덱스에 결박한다 — 같은 스케줄 2회 실행이 같은
    // tick 에 같은 행동을 낳는 것이 하네스 결정론의 전제다. 행동은 델리게이트로
    // 담는다(배치/카드/스킬/강제웨이브 — Bridge/GameManager 공개면 호출). 입력의
    // 데이터 커맨드화(직렬화 가능 스키마)는 M1 IMatchSession 계약의 몫이고, 이
    // 클래스는 그 전 단계의 주입 seam 이다.
    public sealed class HarnessInputSchedule
    {
        private readonly List<(int tick, Action action)> _entries = new();

        public void Add(int tick, Action action)
        {
            if (action == null) return;
            _entries.Add((tick, action));
        }

        // 이번 tick 도달분을 등록 순서대로 실행 — 같은 tick 다건의 순서 = 등록 순서(계약).
        public void RunDue(int tick)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].tick != tick) continue;
                _entries[i].action();
            }
        }
    }
}
