using System;

namespace Wassup.Sim.Match
{
    /// <summary>
    /// battle-sim-extraction unit 15-C-2 — 인접 시너지의 **판정**만 소유한다.
    ///
    /// 적출 전에는 `BattleBridge.RecomputeSynergyFor` 안에서 판정과 적용이 한 덩어리였다:
    /// 이웃을 세는 이중 루프가 `EntityManager.Exists`/`HasComponent&lt;PendingDeployment&gt;` 조회와
    /// 뒤섞여 있어서, "이 배치가 누구에게 몇 배를 주는가" 라는 규칙을 World 없이는 확인할 수 없었다.
    /// 시너지는 **피해 배율**이라 sim-critical 이고(제약 10-c), 여기 나오면 EditMode 로 직접 단정된다.
    ///
    /// **엔진 무참조** — `System` 만 쓴다(`ScoreMath`·`MatchOutcomeRules` 와 같은 부류.
    /// 같은 폴더의 `MatchPlacementRules` 는 아직 `UnityEngine` 을 갖는다 — 그쪽은 unit 17-F 대상).
    ///
    /// 입력은 **타입 키의 5×5 창**이다. 호출자가 "이 칸에 활성 디펜더가 있는가, 그 종류는 무엇인가"
    /// 를 풀어서 `int` 로 넘긴다(0 = 없음). 그래서 이 규칙은 `Entity`·`DefenderUnitData`·
    /// `PendingDeployment` 를 모른다 — 활성 판정(존재·배치 완료)은 호출자 몫이고, **같은 키 = 같은
    /// 종류** 라는 것만 계약이다.
    ///
    /// 창이 5×5 인 이유: 재계산 블록이 3×3(놓인 칸 + 8 이웃)이고 그 각각이 다시 8 이웃을 세므로
    /// 실제로 읽히는 범위가 중심에서 ±2 다.
    /// </summary>
    public static class MatchSynergyRules
    {
        /// 재계산 블록 = 놓인 칸 + 8 이웃.
        public const int BlockSize = 9;

        public const int WindowSpan = 5;
        public const int WindowSize = WindowSpan * WindowSpan;

        /// <summary>
        /// <see cref="CountBlock"/> 이 **미점유** 칸에 쓰는 값. 실제 이웃 수는 0 이상이라 겹치지 않는다.
        /// (배율을 내보내지 않고 이웃 수를 내보내는 이유가 이것이다 — 이웃 0 과 미점유는 다른 사건이고,
        /// 배율로는 둘 다 1.0 이라 구분이 사라진다. 호출자는 전자만 항등값 refresh 를 보낸다.)
        /// </summary>
        public const int Unoccupied = -1;

        /// 창 좌표 → 평면 인덱스. `dx`·`dy` 는 중심 기준 -2..2.
        public static int WindowIndex(int dx, int dy) => (dy + 2) * WindowSpan + (dx + 2);

        /// <summary>
        /// 블록 순회 순서. **계약이다** — 이 순서가 곧 모디파이어 채널 enqueue 순서이고,
        /// 그것이 골든의 `StatModifierSlot` 라인에 실린다. 적출 전 배열 리터럴의 순서를 그대로 옮겼다.
        /// </summary>
        public static (int dx, int dy) BlockOffset(int index) => index switch
        {
            0 => (0, 0),
            1 => (1, 0),
            2 => (-1, 0),
            3 => (0, 1),
            4 => (0, -1),
            5 => (1, 1),
            6 => (-1, 1),
            7 => (1, -1),
            8 => (-1, -1),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        /// <summary>
        /// 블록 9칸 각각에 대해 **같은 종류의 활성 이웃 수**를 센다.
        /// 점유되지 않은 칸에는 <see cref="Unoccupied"/> 를 쓴다.
        /// </summary>
        /// <param name="window">타입 키 5×5 (<see cref="WindowIndex"/> 로 인덱싱, 0 = 활성 디펜더 없음)</param>
        /// <param name="into">길이 <see cref="BlockSize"/>. <see cref="BlockOffset"/> 순서로 채워진다.</param>
        public static void CountBlock(ReadOnlySpan<int> window, Span<int> into)
        {
            if (window.Length < WindowSize) throw new ArgumentException("window must be 5x5", nameof(window));
            if (into.Length < BlockSize) throw new ArgumentException("into must hold 9", nameof(into));

            for (int i = 0; i < BlockSize; i++)
            {
                (int ox, int oy) = BlockOffset(i);
                int self = window[WindowIndex(ox, oy)];
                if (self == 0) { into[i] = Unoccupied; continue; }

                int neighbors = 0;
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (window[WindowIndex(ox + dx, oy + dz)] == self) neighbors++;
                }
                into[i] = neighbors;
            }
        }

        /// <summary>
        /// 이웃 수 → 피해 배율. 이웃 0 은 **곱셈 항등(1)** 이다 — 시너지 없음을 슬롯 제거가 아니라
        /// 항등값 refresh 로 표현하는 것이 모디파이어 채널의 계약이다(끄고 나서 이전 슬롯이 남지 않는다).
        /// </summary>
        public static float Multiplier(int sameTypeNeighbors, float perNeighbor)
            => sameTypeNeighbors <= 0 ? 1f : 1f + perNeighbor * sameTypeNeighbors;
    }
}
