using System.Collections.Generic;
using Unity.Mathematics;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.BattleCore.Move
{
    // **대상 지향 추격판** — 「그 유닛까지, 내 통행 층으로」 구운 거리/방향장.
    //
    // 옛 전투에서 이것이 존재하는 이유는 규칙의 2단계다: 「그 적을 향해 **갈 수 있는** 경로가
    // 있는가」. 공용 사냥판에 위임했던 시절엔 **다른 질문**에 답하고 있었다 —
    //   · 「**아무** 방어유닛의 사격 칸까지」 — 대상이 특정되지 않는다(실측 5.0% 갈림)
    //   · 「**지상** 통행으로」 — 공용 필드가 지상 마스크로만 구워져 비행이 벽 위에서 조용히 죽었다
    //     (그게 「비행은 감지 대상 밖」으로 오독됐다)
    // 이 필드가 붙은 뒤로 **비행은 특별 취급이 없다** — 층은 그 유닛의 통행 층에서 오고
    // 규칙은 층을 언급하지 않는다.
    //
    // ⚠ 이것은 **캐시 키를 가진 필드**다. 「어느 대상까지 / 어느 장애물 상태로 구웠나」를 함께
    // 들지 않으면 사냥 중인 적 전원이 매 틱 격자 전체 BFS 를 돌린다(Android 실기기 비용).
    //
    // ⚠ 전제도 함께 옮긴다 — **「대상은 움직이지 않는다」**(M6 보류). 이동하는 방어유닛 저작이
    // 생기면 이 캐시와 어그로 추격판을 같이 고쳐야 한다. 오늘은 저작 0종이라 도달 불가다.
    public sealed class ChaseFieldCache
    {
        public readonly float2[] Flow;
        public readonly int[] Dist;

        /// <summary>어느 대상까지 구웠나. `None` = 아직 안 구웠다.</summary>
        public SimEntityId BuiltFor = SimEntityId.None;

        /// <summary>어느 장애물 상태로 구웠나. 다르면 낡은 것이다.</summary>
        public uint Signature;

        public ChaseFieldCache(int cellCount)
        {
            int n = math.max(1, cellCount);
            Flow = new float2[n];
            Dist = new int[n];
            Invalidate();
        }

        public bool Matches(SimEntityId target, uint signature)
            => BuiltFor == target && !BuiltFor.IsNone && Signature == signature;

        public void Invalidate()
        {
            BuiltFor = SimEntityId.None;
            for (int i = 0; i < Dist.Length; i++) Dist[i] = int.MaxValue;
            for (int i = 0; i < Flow.Length; i++) Flow[i] = float2.zero;
        }

        public void MarkBuilt(SimEntityId target, uint signature)
        {
            BuiltFor = target;
            Signature = signature;
        }
    }

    // 추격판 대여소. 판당 한 개를 두고 유닛이 사냥을 시작할 때 빌려 주고 끝나면 돌려받는다.
    // 격자 크기 배열 두 개가 유닛당 하나라 **틱 중 새로 만들지 않는 것**이 요점이다.
    public sealed class ChaseFieldPool
    {
        private readonly int _cellCount;
        private readonly Stack<ChaseFieldCache> _free = new Stack<ChaseFieldCache>(8);

        public ChaseFieldPool(int cellCount) => _cellCount = cellCount;

        public ChaseFieldCache Rent()
        {
            if (_free.Count > 0)
            {
                var c = _free.Pop();
                c.Invalidate();
                return c;
            }
            return new ChaseFieldCache(_cellCount);
        }

        public void Return(ChaseFieldCache cache)
        {
            if (cache == null) return;
            _free.Push(cache);
        }
    }
}
