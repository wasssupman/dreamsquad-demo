using Unity.Entities;

namespace Wassup.Battle
{
    // season-gimmick-clockout unit 2 — 전투 진행(running) 여부 신호(singleton).
    //
    // BattleBridge(MonoBehaviour↔ECS 유일 게이트웨이)가 매 프레임 _running 을 write 한다
    // (BattleTimeScale 동형). 그룹-wide phase 인프라라 특정 전투 맥락(Units/Movement/Combat/
    // Effects)에 속하지 않는다. 배치 페이즈와 전투를 구분해야 하는 running-only 시스템
    // (퇴근 타이머 등)이 read. 값 없음/미생성 시 false(미진행)로 취급한다.
    public struct BattleRunning : IComponentData
    {
        public bool Value;
    }
}
