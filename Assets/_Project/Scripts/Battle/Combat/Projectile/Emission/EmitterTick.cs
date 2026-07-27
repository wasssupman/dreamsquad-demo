using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile.Emission
{
    // projectile-emission-pattern unit 0 — 발사 스케줄 전진. plain 값 in/out 순수
    // static (제약 10) — EditMode 로 고정되고 아키텍처를 모른다.
    //
    // 시간 산식은 VolleyMath.TickBurst 를 그대로 호출한다(중복 구현 금지):
    // 잔여 이월로 드리프트 0, interval <= 0 = 남은 전부 즉시, 느린 프레임은
    // 여러 발을 한 번에. 이 클래스가 얹는 것은 fireCount/shotIndex 전진과
    // 완주 판정뿐이다.
    public static class EmitterTick
    {
        // 인스턴스 시작. baseFireCount = durable 소유자가 들고 있는 영속 발사
        // 카운터(계약: 이걸 0 으로 시드하면 선택 규칙이 고정된다 — spec-review C2).
        public static void Begin(ref EmitterRuntime rt, in PatternSpec spec, int baseFireCount)
        {
            rt.burstRemaining = math.max(1, spec.shotCount);
            rt.timer = 0f;
            rt.fireCount = baseFireCount;
            rt.shotIndex = 0;
        }

        // 이번 프레임에 나갈 발수. timer 가 0 으로 시작하므로 시작 프레임에 첫 발이
        // 나간다(VolleyMath 의 기존 버스트 semantics 와 동일).
        public static int Advance(ref EmitterRuntime rt, float dt, float intervalSec)
            => VolleyMath.TickBurst(dt, ref rt.burstRemaining, ref rt.timer, intervalSec);

        public static bool IsComplete(in EmitterRuntime rt) => rt.burstRemaining <= 0;
    }
}
