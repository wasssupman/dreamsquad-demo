namespace Wassup.Battle.Combat
{
    // aggro-targeting Unit 9 — 정의 계층(아키텍처 무참조). 어그로 획득/해제 정책의
    // 순수 판정. ECS/MonoBehaviour 실행모델 무참조 — held/capacity/bool 만 받는다.
    // 해석 계층(AttackSystem·AggroStateSystem)이 상태를 마샬링해 호출만 한다.
    // DcTrigger.Tick / BounceRetarget.FindNext 와 동종 pure-function+EditMode 패턴.
    public static class AggroPolicy
    {
        // capacity 게이트 + 선점: 아직 어그로 안 걸렸고(선점) 상한 여유가 있을 때만 획득.
        public static bool CanAcquire(int held, int capacity, bool alreadyAggroed)
            => !alreadyAggroed && held < capacity;

        // 해제 = 링크 가디언이 살아있지 않음. 단순하지만 해제 조건 확장 지점으로 유지.
        public static bool ShouldRelease(bool guardianAlive) => !guardianAlive;
    }
}
