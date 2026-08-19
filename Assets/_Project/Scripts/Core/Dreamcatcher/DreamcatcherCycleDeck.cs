using System.Collections.Generic;
using Wassup.Data;

namespace Wassup.Core
{
    // dreamcatcher-awakening-hand unit 3 — CR-style cycle queue for the in-match
    // dreamcatcher hand. Pure C# (no ECS/Bridge/UI): the controller (unit 4)
    // owns entity registries and cost checks; this class only knows entries.
    //
    // Rules (spec contract 4~5):
    // - Entries are shuffled ONCE at construction with an injected seed
    //   (System.Random — UnityEngine.Random is forbidden for determinism).
    // - Hand = front N of the queue (non-destructive view).
    // - Squad/Active use → entry moves to the BACK of the queue (rule A).
    // - Unit use → entry leaves the queue (attached, out-of-pool) until
    //   Recover() appends it to the back (host defender died).
    // - Same card SO twice in the deck = two independent entries (entryId).
    public class DreamcatcherCycleDeck
    {
        public struct Entry
        {
            public int entryId;
            public DreamcatcherCard card;
        }

        private readonly List<Entry> _queue = new List<Entry>();
        private readonly Dictionary<int, Entry> _attached = new Dictionary<int, Entry>();

        public int QueueCount => _queue.Count;
        public int AttachedCount => _attached.Count;
        public int TotalCount => _queue.Count + _attached.Count;

        // first-run-tutorial — pinnedFront 는 «앞 N장은 준 순서를 지키고 나머지만 섞는다».
        //
        // 온보딩이 첫 손패를 저작하기 위한 것이다: 첫 판 유저에게 무작위 4장을 주면 그 판의
        // 설명(«이 카드를 붙이면 이렇게 된다»)이 매번 달라진다. 0 = 기존 동작(전량 셔플).
        //
        // **뒤는 계속 섞는다** — 손패가 통째로 고정되면 그 판의 이후 드로우까지 대본이 되고,
        // 온보딩이 끝난 뒤의 플레이가 «튜토리얼 모드» 로 남는다. 고정은 첫 노출까지만이다.
        public DreamcatcherCycleDeck(IReadOnlyList<DreamcatcherCard> cards, int seed, int pinnedFront = 0)
        {
            if (cards != null)
                for (int i = 0; i < cards.Count; i++)
                    if (cards[i] != null)
                        _queue.Add(new Entry { entryId = _queue.Count, card = cards[i] });

            // Fisher-Yates with the injected seed: same (cards, seed) → same order.
            int start = System.Math.Clamp(pinnedFront, 0, _queue.Count);
            var rng = new System.Random(seed);
            for (int i = start; i < _queue.Count - 1; i++)
            {
                int j = i + rng.Next(_queue.Count - i);
                (_queue[i], _queue[j]) = (_queue[j], _queue[i]);
            }
        }

        // Front-N view. Fewer than handSize entries left → returns what remains
        // (empty-slot rendering is the view's job). Fully attached → empty list.
        public List<Entry> Hand(int handSize)
        {
            int take = System.Math.Min(handSize, _queue.Count);
            var result = new List<Entry>(take);
            for (int i = 0; i < take; i++) result.Add(_queue[i]);
            return result;
        }

        public bool TryGetCard(int entryId, out DreamcatcherCard card)
        {
            int idx = IndexInQueue(entryId);
            if (idx >= 0) { card = _queue[idx].card; return true; }
            if (_attached.TryGetValue(entryId, out var entry)) { card = entry.card; return true; }
            card = null;
            return false;
        }

        // Squad/Active commit: remove from the hand region, append to the back.
        public bool UseAndRecycle(int entryId, int handSize)
        {
            int idx = IndexInHand(entryId, handSize);
            if (idx < 0) return false;
            var entry = _queue[idx];
            _queue.RemoveAt(idx);
            _queue.Add(entry);
            return true;
        }

        // Unit commit: remove from the hand region, hold out-of-pool until Recover.
        public bool UseUnit(int entryId, int handSize)
        {
            int idx = IndexInHand(entryId, handSize);
            if (idx < 0) return false;
            var entry = _queue[idx];
            _queue.RemoveAt(idx);
            _attached[entryId] = entry;
            return true;
        }

        // Host defender died: the entry rejoins the queue at the back
        // (recovery order = call order = death order).
        public bool Recover(int entryId)
        {
            if (!_attached.TryGetValue(entryId, out var entry)) return false;
            _attached.Remove(entryId);
            _queue.Add(entry);
            return true;
        }

        // dreamcatcher-retire-recall unit 1 — 「인수인계」 회수: 준 순서 그대로 큐 **맨 앞**에
        // 꽂는다(0,1,2...). 미부착 id 는 건너뛰고, 건너뛴 만큼 뒤 항목이 앞으로 당겨진다.
        //
        // 인덱스 계산이 여기 있는 이유: "손패 = 큐 앞 N" 은 이 클래스만 아는 사실이다
        // (Hand()). 컨트롤러는 "이 id 들을 이 순서로 앞에" 만 말한다(spec 계약 13).
        //
        // ⚠ 이것은 큐에서 **남의 인덱스를 미는 첫 연산**이다. Recover(맨 뒤)는 기존 인덱스를
        // 건드리지 않아 손패 창(front-N) 멤버십이 불변이었지만, 앞 삽입은 창 밖으로 카드를
        // 밀어낸다. 그래서 이 연산은 **플레이어 조작 시점에만** 일어나야 한다(spec 계약 12) —
        // 조준 중 비동기 재정렬은 UseUnit 의 멤버십 가드를 나중에 실패시킨다.
        public int RecoverToFront(IReadOnlyList<int> entryIds)
        {
            if (entryIds == null) return 0;
            int inserted = 0;
            for (int i = 0; i < entryIds.Count; i++)
            {
                if (!_attached.TryGetValue(entryIds[i], out var entry)) continue;
                _attached.Remove(entryIds[i]);
                _queue.Insert(inserted++, entry);
            }
            return inserted;
        }

        private int IndexInQueue(int entryId)
        {
            for (int i = 0; i < _queue.Count; i++)
                if (_queue[i].entryId == entryId) return i;
            return -1;
        }

        // Membership guard: only entries currently visible in the hand are usable.
        private int IndexInHand(int entryId, int handSize)
        {
            int idx = IndexInQueue(entryId);
            return (idx >= 0 && idx < System.Math.Min(handSize, _queue.Count)) ? idx : -1;
        }
    }
}
