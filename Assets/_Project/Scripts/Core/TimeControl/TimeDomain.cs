namespace Wassup.Core.TimeControl
{
    // 시간 스케일이 독립적으로 적용되는 컨텍스트. 확장 = 멤버 추가(컴파일타임).
    // TimeManager 가 (int)domain 을 배열 인덱스로 쓰므로 값은 0부터 연속 유지(명시 값 부여 금지).
    //  - Battle:      ECS BattleSimGroup + BattleBridge 웨이브/타이머 클럭 + 전투 표현(Spine/VFX)
    //  - Interaction: 드래그/배치/카메라/HUD/정지메뉴 UI (기본 1, 보통 스케일 안 받음)
    public enum TimeDomain
    {
        Battle,
        Interaction,
    }
}
