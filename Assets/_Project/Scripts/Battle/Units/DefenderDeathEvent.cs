using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // Emitted by UnitLifecycleSystem immediately before destroying a defender
    // entity. Carries the tile the defender occupied so BattleBridge can free the
    // placement slot and recompute adjacency synergy for surrounding cells.
    //
    // skill-layer-migration unit 3g — 작별 선물 payload 스탬프는 **은퇴했다.** 그 폭발이
    // concrete 로 갔고 자기 죽음 seam 이 값 스냅샷을 자기 채널로 나른다. 이 이벤트가
    // 답하는 질문은 이제 하나다 — 「어느 칸이 비었나」.
    public struct DefenderDeathEvent
    {
        public int2 cell;

    }
}
