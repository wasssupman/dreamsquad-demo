using System.Collections.Generic;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-A — 내부 채널(구 sim 의 `NativeQueue` 싱글턴 9종 대응).
    ///
    /// **핵심 설계 결과: 같은틱/1틱-지연을 위한 장치가 필요 없다.**
    ///
    /// 청사진 ③ §2 는 26쌍을 "같은 틱 12 · 1틱 지연 14" 로 분류하지만, 그 분류는 **채널의 속성이
    /// 아니라 phase 순서의 결과**다 — 생산자가 소비자보다 **앞**이면 같은 틱, **뒤**면 소비자가
    /// 이미 드레인한 뒤라 다음 틱에 소비된다. 청사진 자신이 AggroHit 을 두고 *"구조적 영구 지연 —
    /// 소비자가 생산자보다 앞. 선언 없음 — **구조가 보장**"* 이라고 적었다.
    ///
    /// 그래서 이 타입은 **평범한 FIFO** 다. 지연을 플래그로 표현하지 않는다:
    /// - 플래그로 만들면 phase 순서와 플래그가 **두 개의 진실**이 되고, 어긋나면 조용히 갈린다.
    /// - `StatModifierApply` 한 채널에 같은틱 3 + 지연 7 생산자가 **공존**한다. 채널 단위 플래그로는
    ///   애초에 표현되지 않는다.
    ///
    /// ⇒ 26쌍은 **구현 장치가 아니라 테스트 행렬**이다. 검증은 "phase 순서가 맞는가" 로 환원된다.
    ///
    /// 소비 규약: 채널당 sim 내 소비자는 **정확히 1개**(fan-in 만 존재). 소비자는 자기 phase 에서
    /// <see cref="Drain"/> 으로 통째로 비운다 — 부분 소비는 계약 위반이다(남은 것이 다음 틱에
    /// 섞이면 순서가 무너진다).
    /// </summary>
    public sealed class SimChannel<T> where T : struct
    {
        private readonly List<T> _items = new List<T>();
        private readonly List<T> _drained = new List<T>();

        public int Count => _items.Count;

        public void Enqueue(in T item) => _items.Add(item);

        /// <summary>
        /// 현재 적재분을 통째로 넘기고 채널을 비운다. 반환 리스트는 **다음 Drain 까지만 유효**
        /// (재사용 버퍼 — 드레인마다 새로 할당하면 틱당 9개가 쓰레기가 된다).
        /// 드레인 중 같은 채널에 enqueue 하는 것은 안전하다 — 그 항목은 다음 드레인 몫이다.
        /// </summary>
        public List<T> Drain()
        {
            _drained.Clear();
            _drained.AddRange(_items);
            _items.Clear();
            return _drained;
        }

        /// 매치 경계. 틱 경계에서 부르면 지연 계약이 깨진다(1틱 지연분이 사라진다).
        public void Reset()
        {
            _items.Clear();
            _drained.Clear();
        }
    }
}
