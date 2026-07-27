namespace Wassup.Battle.Combat.Projectile.Emission
{
    // projectile-emission-pattern unit 0 — 발사 인스턴스의 순수 스케줄 상태.
    // 아키텍처 타입 무참조: 이 struct 는 ECS 컴포넌트 안에 값으로 박히기만 하고
    // (EmitterInstance), Mono 이식 시 그대로 살아남는다. 스케줄 상태가 발사
    // 템플릿(Entity 포함)을 품는 형태(VolleyFireState)는 순수 코어를 이식 불가로
    // 만들기 때문에 쓰지 않는다 — README 계약 1.
    public struct EmitterRuntime
    {
        public int burstRemaining;   // 버스트가 아직 빚진 발수
        public float timer;          // 다음 발까지 남은 초 (잔여 이월로 드리프트 0)
        // 선택 규칙의 결정론 소스. 인스턴스는 트리거 발화마다 생성·완주 후 제거되는
        // transient 라, 0 에서 시작하면 RoundRobin 이 영원히 rank 0(같은 대상만)이
        // 된다 — 영속 카운터는 durable 소유자(PatternSlot)가 들고 Begin 이 시드받는다.
        public int fireCount;
        public int shotIndex;        // 현재 버스트 내 순번 (베지어 스윙 소스)
    }
}
