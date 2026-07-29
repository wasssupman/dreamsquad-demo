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
        // 규칙: **직접 걸리는 행동정지(Stun/Sleep)와 넉백(Impulse)만 막는다.**
        // - 스택 임계가 유발한 CC 는 통과 → 스택을 쌓으면 보스도 재울 수 있다(카드 설계 유지).
        // - DoT/Slow 는 통과 → Bleed 데미지·둔화가 보스전에서 살아남는다.
        // lock-set 을 IsLock 단일 소스에서 조회하므로 새 lock 종류가 추가되면 면역이 자동 동행한다.
        public static bool IsBossImmune(CcKind kind, CcSource source)
            => source == CcSource.Direct && (IsLock(kind) || kind == CcKind.Impulse);
    }
}
