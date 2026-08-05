namespace Wassup.Sim.Match
{
    /// <summary>
    /// battle-sim-extraction unit 16-G — 각성 게이지의 **상태와 산식**을 소유한다.
    ///
    /// 적출 전에는 `DreamcatcherHandController`(MonoBehaviour)의 `Gauge` 프로퍼티였고, 획득 클램프
    /// ·넘침 계산·소비 바닥이 뷰 이벤트 발화와 한 메서드에 섞여 있었다. 그래서 "상한에서 얼마가
    /// 소멸하는가" 같은 계약을 확인하려면 컨트롤러와 씬을 세워야 했다.
    ///
    /// **엔진 무참조 · 부작용 없음** — `GaugeChanged`·`AwakeningOverflowed`·`AwakeningGainedAt` 은
    /// 프레젠테이션 신호라 컨트롤러가 계속 소유한다. 이 타입은 값만 결정하고, 그 값을 무엇에 쓸지는
    /// 호출자가 정한다(`MatchOutcomeRules` 와 같은 형태).
    ///
    /// 골든에는 실리지 않는다 — 정규 상태 라인에 게이지가 없다. 증인은 EditMode 다.
    /// </summary>
    public sealed class MatchGaugeRules
    {
        private int _current;
        private int _max;

        public int Current => _current;
        public int Max => _max;

        /// <summary>
        /// 매치 경계. `start`·`max` 는 저작값(`AwakeningConfig`)이고 호출자가 풀어서 넘긴다.
        /// 시작값은 상한을 넘을 수 없다 — 시트 오기(start &gt; max)를 여기서 접는다.
        /// </summary>
        public void Reset(int start, int max)
        {
            _max = max < 0 ? 0 : max;
            _current = start < 0 ? 0 : (start > _max ? _max : start);
        }

        public bool CanAfford(int cost) => _current >= cost;

        /// <summary>
        /// 각성 획득. **넘침은 소멸한다**(이월 없음) — 그 사실을 뷰가 알려야 해서
        /// `overflowed` 를 따로 낸다(적출 전 `AwakeningOverflowed` 발화 조건 그대로:
        /// 일부라도 상한에 막히면 그 양만큼).
        /// </summary>
        /// <param name="applied">실제로 게이지에 들어간 양. 뷰의 흡수 연출 수량이 이것이다.</param>
        /// <param name="overflowed">상한에 막혀 소멸한 양.</param>
        /// <returns>게이지가 실제로 움직였는가(0 이면 뷰 갱신 불필요).</returns>
        public bool TryGain(int reward, out int applied, out int overflowed)
        {
            applied = 0;
            overflowed = 0;
            if (reward <= 0) return false;

            int next = _current + reward;
            if (next > _max) next = _max;
            applied = next - _current;
            overflowed = reward - applied;
            if (applied == 0) return false;
            _current = next;
            return true;
        }

        /// 소비. 바닥은 0 이다(음수 게이지 없음). 지불 가능성 판정은 `MatchCardRules` 가 한다 —
        /// 여기서 다시 막으면 두 곳이 같은 규칙을 갖는다.
        public void Spend(int cost)
        {
            if (cost <= 0) return;
            _current = _current - cost < 0 ? 0 : _current - cost;
        }
    }
}
