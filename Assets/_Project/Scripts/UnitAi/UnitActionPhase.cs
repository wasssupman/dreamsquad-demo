namespace Wassup.UnitAi
{
    // defender-deploy-phase unit 3 — 「지금 이 유닛이 행동을 시작할 수 있나」의 자리. 로직 레이어(Wassup.UnitAi, 엔진 참조 불가) · Burst 호환.
    //
    // 우선순위 표(문서 계약, docs/spec/defender-deploy-phase README 6):
    //   Dead > Deploying > Locked > Swinging > Free
    // 위 두 랭크는 이 함수의 인자가 아니라 **쿼리 랭크**다 — `WithNone<PendingDeployment>`(존재 배제 · 피격·타겟 후보까지)
    // 와 `DeadTag`(파괴 대기). 여기는 그 아래 셋만 접는다. 오늘은 AttackSystem·MovementSystem 의 lock 식 추출이고(동작 무변),
    // 유닛이 스스로 전이하는 행동(재장전·후퇴)이 생기면 이 자리가 저장 상태(DefenderAiState)로 승격된다 — 적 FSM 선례.
    public enum ActionPhase : byte
    {
        Free = 0,
        Swinging = 1,   // 공격 시작 뒤 타격 판정 대기(hitDelayRemaining > 0) — 새 START 없음, RESOLVE 는 완료
        Locked = 2,     // CC(Sleep/Stun) ‖ 도약 비행 — START 없음, 진행 중 스윙은 완료(combat-action-lock 규약)
    }

    public static class UnitActionPhase
    {
        public static ActionPhase Resolve(bool actionLocked, bool swinging)
            => actionLocked ? ActionPhase.Locked : swinging ? ActionPhase.Swinging : ActionPhase.Free;

        // 공격 START · 캐스트 START · 자기주도 이동.
        public static bool CanStartAction(ActionPhase phase) => phase == ActionPhase.Free;

        // 이미 시작된 스윙의 RESOLVE — 오늘 정의된 랭크 어디서도 막지 않는다(CC 중에도 완료). 랭크가 늘 때 좁힌다.
        public static bool CanResolveSwing(ActionPhase phase) => true;
    }
}
