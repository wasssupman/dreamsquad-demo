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
    }
}
