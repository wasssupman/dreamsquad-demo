namespace Somnia.Battle.UnitAi
{
    // defender-autobattle-ai unit 0 — 방어유닛 행동 상태(진영 의도층). 로직 레이어 · 엔진 참조 불가.
    // 큰 값이 우선한다. Deploying·Locked 는 «판/행동 불가», Engaging 은 «스윙 중», Sustaining 은 «소환물 유지», Ready 는 «행동 가능».
    // 오늘의 규칙을 그대로 옮긴 것(동작 무변) — 진리표(DefenderAiTests)가 규칙서다.
    public enum DefenderAiState : byte
    {
        Ready = 0,
        Sustaining = 1,   // Summon 정책 + 소환물 생존. 뷰는 능력 루프(activeAnimation)를 튼다
        Engaging = 2,     // 공격 시작 뒤 타격 판정 대기(hitDelayRemaining > 0). 즉발 공격은 Ready 에서 사건만 낸다
        Locked = 3,       // CC(Sleep/Stun) ‖ 도약 비행 — 공통 술어층(UnitActionPhase)
        Deploying = 4,    // 배치 페이즈(PendingDeployment) — 판에 없다
    }

    // 공격 정책 — 아키타입 판별은 이 값으로(타입 체크 금지, README 계약 7). 스폰 시 능력 SO 존재에서 한 번 bake 된다.
    public enum DefenderAttackPolicy : byte { Target = 0, Bomb = 1, Summon = 2 }

    // plain 값 스냅샷. 적용 레이어(DefenderAiStateSystem)가 컴포넌트를 읽어 채운다. Entity·컴포넌트가 여기 들어오지 않는다(계약 2).
    public struct DefenderAiInput
    {
        public bool deploying;
        public bool actionLocked;
        public bool swinging;
        public DefenderAttackPolicy policy;
        public bool summonAlive;   // Summon 정책에서만 의미
    }

    public static class DefenderAi
    {
        public static DefenderAiState Resolve(in DefenderAiInput i)
        {
            if (i.deploying) return DefenderAiState.Deploying;
            var phase = UnitActionPhase.Resolve(i.actionLocked, i.swinging);   // 공통 술어층 — 적과 같은 함수
            if (phase == ActionPhase.Locked) return DefenderAiState.Locked;
            if (phase == ActionPhase.Swinging) return DefenderAiState.Engaging;
            if (i.policy == DefenderAttackPolicy.Summon && i.summonAlive) return DefenderAiState.Sustaining;
            return DefenderAiState.Ready;
        }

        // 오늘의 START 게이트: Ready 에서 쿨 준비. Sustaining 도 «시도»한다 — AttackSystem 이 소환물 생존이면 스폰을 건너뛰고
        // 쿨만 리셋하는데(재소환 대기 = 남은 쿨), 그 리셋이 없으면 소환물 사망 즉시 재소환이 되어 동작이 바뀐다.
        // «타겟이 있나»는 적용 레이어가 안다(후속 unit-ai-targeting 에서 편입).
        public static bool CanStartAttack(DefenderAiState state, bool cooldownReady)
            => cooldownReady && (state == DefenderAiState.Ready || state == DefenderAiState.Sustaining);
    }
}
