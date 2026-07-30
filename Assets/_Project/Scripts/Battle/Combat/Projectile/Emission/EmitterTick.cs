using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile.Emission
{
    // projectile-emission-pattern unit 0 — 발사 스케줄 전진. plain 값 in/out 순수
    // static (제약 10) — EditMode 로 고정되고 아키텍처를 모른다.
    //
    // projectile-shot-sequence unit 0 — 각 step 의 interval 을 소비한다.
    // 음수 잔여 이월로 드리프트 0, interval 0 연속 step 은 같은 프레임에
    // 전부 발사, 느린 프레임은 여러 발을 한 번에 반환한다.
    public static class EmitterTick
    {
        // 인스턴스 시작. baseFireCount = durable 소유자가 들고 있는 영속 발사
        // 카운터(계약: 이걸 0 으로 시드하면 선택 규칙이 고정된다 — spec-review C2).
        public static void Begin(ref EmitterRuntime rt, in PatternSpec spec, int baseFireCount)
        {
            rt.burstRemaining = spec.shots.Length;
            rt.timer = 0f;
            rt.fireCount = baseFireCount;
            rt.shotIndex = 0;
        }

        // 이번 프레임에 나갈 발수. timer 가 0 으로 시작하므로 시작 프레임에 첫 발이
        // 나간다(VolleyMath 의 기존 버스트 semantics 와 동일).
        public static int Advance(ref EmitterRuntime rt, float dt, in PatternSpec spec)
        {
            if (rt.burstRemaining <= 0) return 0;

            rt.timer -= Unity.Mathematics.math.max(0f, dt);
            int fired = 0;
            // 스케줄 진행도는 스케줄러가 소유한다. ShotOrder 소비자가 아직
            // shotIndex를 전진시키지 않았어도 다음 interval을 정확히 읽어야 한다.
            int nextShotIndex = spec.shots.Length - rt.burstRemaining;
            while (rt.timer <= 0f && rt.burstRemaining > 0)
            {
                fired++;
                rt.burstRemaining--;
                nextShotIndex++;

                if (rt.burstRemaining > 0)
                {
                    rt.timer += Unity.Mathematics.math.max(
                        0f, spec.shots[nextShotIndex].intervalAfterPreviousSec);
                }
            }

            if (rt.burstRemaining == 0) rt.timer = 0f;
            return fired;
        }

        public static bool IsComplete(in EmitterRuntime rt) => rt.burstRemaining <= 0;

        // 한 trigger의 첫 탄부터 마지막 탄까지 걸리는 시간. 첫 step의 interval은
        // 계약상 무시되고, 이후 step의 "직전 탄 이후" 값만 합산한다. defender
        // AttackSystem이 다음 cooldown을 마지막 탄 뒤로 미루는 데 쓴다.
        public static float TotalDuration(in PatternSpec spec)
        {
            float duration = 0f;
            for (int i = 1; i < spec.shots.Length; i++)
                duration += Unity.Mathematics.math.max(0f, spec.shots[i].intervalAfterPreviousSec);
            return duration;
        }
    }
}
