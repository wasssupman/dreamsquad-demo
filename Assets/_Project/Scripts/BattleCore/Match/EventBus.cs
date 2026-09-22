using System;
using System.Collections.Generic;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 사건 배달. **구독 순서가 계약**이다(UML §1).
    //
    // 담당자 사이의 순서 의존(처치 → 전멸 판정, 골 도달 → 붕괴 → 보너스 제안)을
    // 「한 함수가 담당자 둘을 차례로 부른다」로 쓰지 않는 것이 계약 12 의 요지다.
    // 그래서 순서를 **구독에 숫자로 적는다** — 낮은 `order` 가 먼저 받는다.
    // 같은 `order` 면 구독한 차례. 둘 다 결정론이다.
    //
    // 배달 시점: `Publish` 는 **쌓기만** 하고, 틱 마지막 `FlushPhase` 가 발행 순서대로
    // 배달한다. 발행 도중 배달하면 한 담당자의 리스트를 다른 담당자가 수정하는 중에
    // 순회하게 되고, 그 버그는 웨이브 전멸 같은 「한 틱에 여럿 죽는」 순간에만 나온다.
    public sealed class EventBus
    {
        private readonly struct Subscription
        {
            public readonly int Order;
            public readonly int Seq;
            public readonly Action<CoreEvent> Handler;

            public Subscription(int order, int seq, Action<CoreEvent> handler)
            {
                Order = order;
                Seq = seq;
                Handler = handler;
            }
        }

        private const int KindCount = 8;   // CoreEventKind 의 여유 폭. 늘리면 여기도 늘린다.

        private readonly List<Subscription>[] _subs = new List<Subscription>[KindCount];

        // 발행 순서를 유지하는 두 버퍼. `_pending` 은 아직 배달 안 한 것,
        // `_outbox` 는 배달까지 끝나 Unity 층이 가져갈 것.
        private readonly List<CoreEvent> _pending;
        private readonly List<CoreEvent> _outbox;

        private int _seq;

        public EventBus(int capacity = 256)
        {
            _pending = new List<CoreEvent>(capacity);
            _outbox = new List<CoreEvent>(capacity);
            for (int i = 0; i < KindCount; i++) _subs[i] = new List<Subscription>(4);
        }

        /// <summary>배달까지 끝난 이벤트. Unity 층(`BattleDriver`)이 틱 뒤에 드레인한다.</summary>
        public IReadOnlyList<CoreEvent> Outbox => _outbox;

        /// <summary>
        /// 구독. `order` 가 낮을수록 먼저 받는다 — 이 숫자가 담당자 간 순서 계약이다.
        /// 틱 밖(조립 시점)에서만 부른다.
        /// </summary>
        public void Subscribe(CoreEventKind kind, int order, Action<CoreEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var list = _subs[(int)kind];
            var sub = new Subscription(order, _seq++, handler);

            int at = list.Count;
            while (at > 0 && (list[at - 1].Order > sub.Order)) at--;
            list.Insert(at, sub);
        }

        /// <summary>발행. 쌓기만 한다 — 배달은 `Flush`.</summary>
        public void Publish(in CoreEvent e) => _pending.Add(e);

        /// <summary>
        /// 발행 순서대로 배달하고 outbox 로 옮긴다. 배달 중 새로 발행된 것은 **같은
        /// 틱 안에서 이어서** 배달한다(리스너가 낸 사건을 다음 틱으로 미루면 한 틱짜리
        /// 지연이 규칙이 되어 버린다). `_pending` 이 자라는 동안 index 로 훑으므로
        /// 순회 중 추가가 안전하다.
        /// </summary>
        public void Flush()
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                var e = _pending[i];
                var list = _subs[(int)e.Kind];
                for (int s = 0; s < list.Count; s++) list[s].Handler(e);
                _outbox.Add(e);
            }
            _pending.Clear();
        }

        /// <summary>Unity 층이 가져간 뒤 비운다. 코어는 outbox 를 읽지 않는다.</summary>
        public void ClearOutbox() => _outbox.Clear();
    }
}
