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

        public DreamcatcherCycleDeck(IReadOnlyList<DreamcatcherCard> cards, int seed)
        {
            if (cards != null)
                for (int i = 0; i < cards.Count; i++)
                    if (cards[i] != null)
                        _queue.Add(new Entry { entryId = _queue.Count, card = cards[i] });

            // Fisher-Yates with the injected seed: same (cards, seed) → same order.
            var rng = new System.Random(seed);
            for (int i = 0; i < _queue.Count - 1; i++)
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

        private int IndexInQueue(int entryId)
        {
            for (int i = 0; i < _queue.Count; i++)
                if (_queue[i].entryId == entryId) return i;
            return -1;
        }

        /// <summary>
        /// battle-sim-extraction unit 16 — **커밋 가능성의 정본**. `TryGetCard` 는 큐 **또는**
        /// 부착분을 보므로(아이콘 스냅샷이 부착분을 읽어야 한다) 사용 가능 판정에 쓰면 안 된다.
        /// 커밋(`UseUnit`/`UseAndRecycle`)이 요구하는 것은 **손패 안**이므로 검증도 이것을 봐야
        /// 둘이 어긋나지 않는다.
        /// </summary>
        public bool IsInHand(int entryId, int handSize) => IndexInHand(entryId, handSize) >= 0;

        // Membership guard: only entries currently visible in the hand are usable.
        private int IndexInHand(int entryId, int handSize)
        {
            int idx = IndexInQueue(entryId);
            return (idx >= 0 && idx < System.Math.Min(handSize, _queue.Count)) ? idx : -1;
        }
    }
}
