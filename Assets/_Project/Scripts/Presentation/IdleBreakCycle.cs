using UnityEngine;

namespace Wassup.Presentation
{
    // idle-break-shared unit 0 — 대기 컷 규칙의 단일 소유자. 백엔드 중립(아키텍처 타입 0).
    //
    // 틀(2026-09-17 사용자): 기본 대기 루프가 항상 돌고, N초마다 「그 외 idle 모션」 하나를 한 바퀴 끼운 뒤 루프로 돌아온다.
    // 풀에 기본 idle 은 없다. 이 구조체는 «언제 전이하는가»와 «다음 컷은 무엇인가»만 안다 — 무엇을 어떻게 보여줄지
    // (플립북 재생 / AnimationState 트랙)와 컷의 한 바퀴 길이(FlipbookMath.Duration / Animation.Duration)는 뷰가 답한다.
    //
    // 소비 형태(두 뷰 동일):
    //   if (!_idle.Tick(dt)) return;
    //   if (_idle.Looping) { int i = _idle.PickBreak(count, roll); float d = DurationOf(i);
    //                        if (d > 0f) { _idle.BeginBreak(d); PlayBreak(i); return; } }
    //   _idle.BeginLoop(interval); PlayBaseLoop();
    public struct IdleBreakCycle
    {
        public bool Active;      // 순환 중인가. false 면 Tick 은 아무것도 안 한다(원샷·walk·오버라이드가 끼어든 상태).
        public bool Looping;     // true = 기본 루프 중(다음 컷까지 대기) · false = 컷 재생 중
        public float Timer;      // 남은 초(호출측이 배틀 스케일로 환산한 dt 를 준다)
        public int LastIndex;    // 직전에 튼 컷 인덱스(-1 = 없음) — 연속 회피용. Stop 해도 유지한다(복귀 뒤에도 같은 컷이 이어지지 않게).

        public static IdleBreakCycle Idle => new IdleBreakCycle { LastIndex = -1 };

        public void Stop() => Active = false;

        // 기본 루프 + 다음 컷까지의 대기. 음수는 0 = 다음 틱에 바로 컷(연속 재생).
        public void BeginLoop(float interval)
        {
            Active = true;
            Looping = true;
            Timer = Mathf.Max(0f, interval);
        }

        // 컷 한 바퀴. duration 은 뷰가 잰 길이 — 시트/트랙의 loop 플래그와 무관하게 여기서 끊는다.
        public void BeginBreak(float duration)
        {
            Active = true;
            Looping = false;
            Timer = Mathf.Max(0f, duration);
        }

        // 반환 = 이번 틱에 전이해야 하는가. 전이 뒤 호출측이 BeginLoop/BeginBreak 로 다음 구간을 연다.
        public bool Tick(float dt)
        {
            if (!Active) return false;
            Timer -= dt;
            return Timer <= 0f;
        }

        // 직전과 다른 컷을 뽑는다(컷이 2개 이상일 때). 규칙은 UnitAnimationChoice 가 소유(EditMode 테스트 있음).
        public int PickBreak(int breakCount, float roll)
        {
            int next = UnitAnimationChoice.ChooseNext(breakCount, LastIndex, roll);
            if (next >= 0) LastIndex = next;
            return next;
        }
    }
}
