using Unity.Entities;

namespace Wassup.Battle
{
    // time-manager Unit 2 — BattleSimGroup 의 시간 스케일 입력(singleton).
    //
    // BattleBridge(MonoBehaviour↔ECS 유일 게이트웨이)가 매 프레임 TimeManager.ScaleOf(Battle)
    // 로 write 하고, BattleScaledRateManager 가 read 한다. 그룹 시간 제어 인프라이므로 특정
    // 전투 맥락(Units/Movement/Combat/Effects)에 속하지 않는다.
    //
    // 값 없음/미생성 시 RateManager 는 1(정상 속도)로 취급한다.
    public struct BattleTimeScale : IComponentData
    {
        public float Value;
    }
}
