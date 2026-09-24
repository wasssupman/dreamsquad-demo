using System.Collections.Generic;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 7a — **seam 의 틱 안 실행 순서.**
    //
    // ⚠ `Seam` 의 enum 값은 **뚫린 순서**(append-only)이지 실행 순서가 아니다 — `Periodic`(4)은
    // `FieldPrepPhase` 끝이라 `Attack`(0) 보다 **앞**에서 돈다. 번호로 「후속이냐 지난 seam 이냐」를
    // 판정하면 주기 사건이 「이미 지난 seam」으로 오판돼 **한 틱 밀린다**. 그래서 판정은 이 표로만 하고,
    // 표는 `TickPipeline` 을 읽어 **한 곳에서** 만든다 — 단계가 자기가 여는 seam 을 순서대로 말한다.
    // enum 값을 비교하는 코드가 0 이라는 것은 `TriggerDispatchTests` 가 소스로 못박는다.
    public interface ISeamHost
    {
        /// <summary>이 단계가 여는 seam 을 **실행 순서대로** 붙인다.</summary>
        void AppendSeams(List<Seam> into);
    }

    public sealed class SeamTickOrder
    {
        private readonly int[] _index;

        private SeamTickOrder(int[] index) => _index = index;

        public static SeamTickOrder From(TickPipeline pipeline)
        {
            var order = new List<Seam>((int)Seam._Count);
            for (int i = 0; i < pipeline.PhaseCount; i++)
                if (pipeline.PhaseAt(i) is ISeamHost host) host.AppendSeams(order);

            var index = new int[(int)Seam._Count];
            for (int i = 0; i < index.Length; i++) index[i] = -1;
            for (int i = 0; i < order.Count; i++) index[(int)order[i]] = i;
            return new SeamTickOrder(index);
        }

        /// <summary>틱 안 순번(0 부터). 파이프라인에 없는 seam 은 -1.</summary>
        public int IndexOf(Seam seam) => _index[(int)seam];

        /// <summary>`seam` 이 틱 안에서 `position`(이미 돈 마지막 seam 의 순번) **뒤**에 오나.</summary>
        public bool IsLater(Seam seam, int position) => _index[(int)seam] > position;
    }
}
