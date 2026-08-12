using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // 사거리 판정의 **단일 술어**. 아키텍처 중립이라 순수 함수로 둔다
    // (제약 10 — 타겟팅은 sim-critical 이라 단위 테스트를 유지한다).
    //
    // ⚠ **소비처가 다섯이고 다섯이 반드시 같은 답을 받아야 한다.**
    //   1) AttackSystem 타겟 선정 게이트        — «때릴 수 있나»
    //   2) AttackSystem focus 락 유지            — 락을 문 뒤에도 같은 조건이어야 한다
    //   3) AttackSystem committedTarget 재판정   — RESOLVE 시 사거리 이탈 판정
    //   4) EnemyAiStateSystem.HasFireTarget      — «멈춰도 되나»(Engaging → EnemyBehavior.Halt)
    //   5) PatrolAreaMath.StepDir/CloseInDir     — «더 다가가야 하나»(사격 칸 도착 판정)
    // 새 소비처가 생기면 이 함수를 쓰고 목록에 적을 것.
    //
    // 특히 **락 경로(2·3)를 빠뜨리기 쉽다.** 셀만 보면 락을 문 뒤로는 2차 게이트가 영영
    // 적용되지 않고, (4)의 미러와 갈려 «쏘면서 골로 걸어가는» 상태가 된다(코드 리뷰 지적).
    //
    // ⚠ **스냅샷은 한 이동 스텝 어긋난다.** (4)(5)는 `[UpdateBefore(MovementSystem)]`,
    // (1)(2)(3)은 `[UpdateAfter(MovementSystem)]` 이라 같은 프레임에도 서로 다른 위치를 본다.
    // 어긋남 상한은 `ClampDisplacement` 가 프레임당 변위를 tileSize 로 묶어 유한하고,
    // 아래 CellSlackTiles(0.5)가 그 흔들림을 흡수하는 여유다 — 이 상한을 0 으로 조이면
    // 판정이 프레임마다 뒤집혀 사격이 끊긴다. 순서를 바꾸는 것도 답이 아니다:
    // (1)이 post-move 를 보는 것은 **타격 정확도상 의도**다.
    //
    // **왜 두 단계인가**
    //
    // 판정의 1차는 셀 체비셰프다 — 격자 게임이고 «대각 인접도 사거리 1»이 기존 계약이다.
    // 그런데 GridMath.WorldToCell 이 셀 중심 ±0.5타일에서 칸을 가르므로, 공격자와 타겟이
    // **둘 다 연속 이동**이면 각자 반 칸씩 밀려 사거리 1이 실측 2칸 가까이 닿는다
    // (타일 고정 유닛은 한쪽만 밀리므로 최대 1.5칸). 그래서 연속↔연속에만 물리 거리를
    // **후순위로** 덧건다.
    //
    // ⚠ **한 곳에만 넣으면 교착이 난다** (2026-08-12 실측). 처음엔 (1)에만 넣었는데,
    // (3)이 «격자상 사격 칸에 도착했으니 멈춰»라고 하고 (1)이 «물리적으로 머니 못 쏴»라고
    // 해서 순찰병이 적 옆에 붙어 선 채 아무것도 안 했다(182프레임). 이동을 멈추는 근거가
    // 사격 가능 여부인 이상, 공격이 더 엄한 조건을 걸면 **이동도 그 조건까지 좁혀 들어가야**
    // 한다. 지금은 셋이 이 파일 하나를 본다.
    public static class AttackReach
    {
        // 셀 절반. 밸런스 knob 이 아니라 **격자 정의에서 나오는 구조 상수**다
        // (WorldToCell 이 floor(x/tile + 0.5) 로 ±0.5 에서 칸을 가른다). SO 로 빼지 않는다.
        // 값의 의미: «연속 유닛도 타일 고정 유닛이 이미 갖던 만큼만 오차를 갖는다» —
        // 그래서 이 상한은 누구의 체감 사거리도 좁히지 않고 두 배 슬랙만 깎는다.
        public const float CellSlackTiles = 0.5f;

        // 1차 게이트 — 셀 체비셰프. 격자 계층(BFS·필드)이 쓰는 그 메트릭이다.
        public static bool InCellRange(int2 atkCell, int2 tgtCell, int tileRange)
            => math.max(math.abs(tgtCell.x - atkCell.x), math.abs(tgtCell.y - atkCell.y)) <= tileRange;

        // 2차 게이트 — 물리 거리. 월드에서도 **체비셰프**로 잰다(셀 규칙과 같은 metric 이라야
        // «대각 인접도 사거리 1» 계약이 유지된다. 유클리드로 재면 대각이 1.41 이라 조용히 좁아진다).
        public static bool InWorldReach(float3 atkPos, float3 tgtPos, int tileRange, float tileSize)
            => math.max(math.abs(tgtPos.x - atkPos.x), math.abs(tgtPos.z - atkPos.z))
               <= (tileRange + CellSlackTiles) * tileSize;

        // 합본. bothContinuous = 공격자와 타겟이 **둘 다** 연속 이동인가. 한쪽이라도 타일
        // 고정이면 2차를 걸지 않는다(슬랙이 애초에 한쪽뿐이라 덧걸 이유가 없고, 출시된
        // 유닛 전원의 체감 사거리를 건드리지 않기 위함).
        public static bool InReach(int2 atkCell, int2 tgtCell, int tileRange,
                                   float3 atkPos, float3 tgtPos, float tileSize, bool bothContinuous)
        {
            if (!InCellRange(atkCell, tgtCell, tileRange)) return false;
            return !bothContinuous || InWorldReach(atkPos, tgtPos, tileRange, tileSize);
        }
    }
}
