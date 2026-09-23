using Wassup.Battle.Units;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 4 — **점수.**
    //
    // 규칙이 한 줄이다: **1킬 = 1점, 예외 없음**(Y3). 보스도 분열체도 1 이고, 티어로
    // 가중하던 축은 은퇴했다. 그래서 「총점」과 「잡은 마리 수」가 한 축이다.
    //
    // 흘려보낸 적은 들어오지 않는다(Y4) — 그쪽은 처치 사건을 내지 않는다. 그것이
    // 「못 잡은 적 = 못 번 점수」라는 이 게임의 **유일한 페널티**의 실체다.
    //
    // 서버에 올리는 수도 **총점 그대로**다(Y5). 남은 안정도를 값에 실어 동점을 가르던
    // 인코딩은 폐기됐다 — 가공을 한 겹 두면 화면 숫자와 서버 숫자가 갈린다.
    //
    // 담당자 단계가 없다(`ITickPhase` 아님). 점수는 **구독만으로** 성립한다 —
    // 매 틱 할 일이 없는데 단계를 만들면 다음 사람이 「여기 뭘 넣어야 하나」를 묻는다.
    public sealed class ScoreLedger
    {
        private readonly EventBus _bus;

        private int _kills;

        public ScoreLedger(EventBus bus)
        {
            _bus = bus;
            _bus.Subscribe(CoreEventKind.UnitSlain, EventOrder.Score, OnSlain);
        }

        /// <summary>잡은 마리 수 = 점수. 음수가 될 길이 없다(더하기만 한다).</summary>
        public int Kills => _kills;

        /// <summary>총점. 점수원이 처치 하나뿐이라 <see cref="Kills"/> 와 같다 —
        /// 호출부가 「총점」을 읽는 자리를 남겨 둔다(점수 축이 늘면 여기만 바뀐다).</summary>
        public int Total => _kills;

        /// <summary>**서버에 보내는 수.** 총점 그대로이며 가공이 없다.</summary>
        public int SubmissionScore => _kills;

        public void Begin() => _kills = 0;

        private void OnSlain(CoreEvent e)
        {
            // ⚠ **진영으로 거른다.** 「1킬 = 1점, 예외 없음」은 *적을* 잡은 것에 대한 규칙이고,
            // 방어유닛이 죽은 것은 처치가 아니다. 사건 자체는 진영을 가리지 않고 나므로
            // (그래야 각성 게이지가 사망 보상을 받는다) 거르는 것은 읽는 쪽의 일이다.
            if (e.Faction != Faction.EnemyUnit) return;
            _kills++;
            _bus.Publish(CoreEvent.ScoreChanged(e.Tick, _kills));
        }
    }
}
