using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // combat-action-lock unit 0 — "행동 불가(공격+이동 정지)" 판정 단일 소스.
    // Sleep/Stun 을 lock 으로 취급. 순수 함수(Burst 호환) — AttackSystem(Combat) 과
    // MovementSystem(Movement) 이 CcEffect(Effects 소유)를 읽기만 해서 게이트한다.
    public static class CcActionLock
    {
        // lock-set 단일 소스. 새 lock 종류는 여기만 추가.
        public static bool IsLock(CcKind kind) => kind == CcKind.Stun || kind == CcKind.Sleep;

        public static bool IsLocked(in DynamicBuffer<CcEffect> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
                if (IsLock(buffer[i].kind)) return true;
            return false;
        }

        // boss-jjangssen unit 3 — 보스 CC 면역 술어. 순수 함수라 EditMode 로 고정한다
        // (부여 지점 3곳이 같은 판정을 써야 하고, CcKind 에 값이 추가될 때의 회귀 가드).
        //
        // 규칙: **행동정지(Stun/Sleep)와 넉백(Impulse)을 막는다 — 출처 불문.**
        // lock-set 을 IsLock 단일 소스에서 조회하므로 새 lock 종류가 추가되면 면역이 자동 동행한다.
        //
        // unit 8 — 원래는 `직접 출처` 조건이 앞에 있어 스택 임계발 CC 는 통과했다. 그 예외의
        // 근거는 "DoT 가 CcEffect 버퍼를 공유하니 kind 로만 막으면 스택 DoT 까지 죽는다" 였는데,
        // dot-effect-extraction 이 DoT 를 전용 채널로 빼면서 근거가 사라졌다(같은 날 3시간 뒤).
        // 남은 유일한 통과자가 Ice 5중첩 스턴이라, 축은 면역에 구멍만 유지하고 있었다.
        // 스택 카드는 여전히 보스전에서 산다 — 감속은 StatModifier, DoT 는 DotApplyEvents 라
        // 둘 다 이 술어를 지나지 않는다.
        public static bool IsBossImmune(CcKind kind)
            => IsLock(kind) || kind == CcKind.Impulse;
    }
}
