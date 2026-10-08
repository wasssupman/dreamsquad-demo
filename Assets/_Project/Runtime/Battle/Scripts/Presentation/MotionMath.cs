using UnityEngine;

namespace Somnia.Battle.Presentation
{
    // keyring-unify 0 → demo-diet unit 0 — 전투 연출이 쓰는 공통 운동 수학(스프링 추종 · 하마 궤적 · 비행 시간 재매핑).
    // 옛 `Somnia.Battle.UI.KeyringSim` 의 후계다. 아웃게임 키링과 공유하던 파일이라 이름이 「키링」이었고, 로비 전용 몫
    // (`FallStep` 낙하·`LeanAngle` 기울임·`ThrowArcControls` 던지기)은 아웃게임과 함께 지웠다. 소비처 = `CameraDirector`
    // (포인터 추종·헤드룸·오버뷰 가중치), `CoreHandView`(손패 간격), `CoreDeployFlightPresenter`·`CoreLeapPresenter`(하마).
    // 순수 static, 좌표계 비의존(Vector3 본체 + Vector2/스칼라 포워딩 오버로드 — z=0, bit-exact).
    // dt clamp(Mathf.Max(dt, 1e-4f))·초기화·재잡기·좌표 산출은 호출측 책임.
    public static class MotionMath
    {
        // 무게추 스프링+감쇠+속도상한 적분. maxSpeed <= 0 = 무제한.
        public static void SpringStep(ref Vector3 pos, ref Vector3 vel, Vector3 target,
            float spring, float damping, float maxSpeed, float dt)
        {
            Vector3 accel = (target - pos) * spring - vel * damping;
            vel += accel * dt;
            if (maxSpeed > 0f)
            {
                float sp = vel.magnitude;
                if (sp > maxSpeed) vel *= maxSpeed / sp;
            }
            pos += vel * dt;
        }

        // Vector2 포워딩 오버로드 — Vector3 본체에 위임. z=0 왕복이라 bit-exact.
        // 호출측이 마샬링을 직접 하면 copy-back 누락이 무증상 풋건이 되므로 여기서 흡수한다.
        public static void SpringStep(ref Vector2 pos, ref Vector2 vel, Vector2 target,
            float spring, float damping, float maxSpeed, float dt)
        {
            Vector3 p = pos, v = vel;
            SpringStep(ref p, ref v, target, spring, damping, maxSpeed, dt);
            pos = p;
            vel = v;
        }

        // 스칼라 포워딩 오버로드(가중치·각도 등 1차원 추종) — Vector3 본체에 위임.
        // camera-direction 손패 헤드룸이 첫 소비처: 0↔1 가중치를 스프링으로 밀어
        // 진입·복귀 모두 살짝 오버슈트 후 안착시킨다(MoveTowards 는 그 맛이 안 난다).
        public static void SpringStep(ref float pos, ref float vel, float target,
            float spring, float damping, float maxSpeed, float dt)
        {
            Vector3 p = new Vector3(pos, 0f, 0f), v = new Vector3(vel, 0f, 0f);
            SpringStep(ref p, ref v, new Vector3(target, 0f, 0f), spring, damping, maxSpeed, dt);
            pos = p.x;
            vel = v.x;
        }

        // defender-tap-to-place unit 6 — 3차 베지어(제어점 2개) 점 평가.
        // 시작 상승/도착 하강 접선을 독립 튜닝한다. 좌표계 비의존, endpoints 정확 → 착지 오차 0.
        public static Vector3 CubicBezier(Vector3 a, Vector3 controlA, Vector3 controlB, Vector3 b, float t)
        {
            float u = 1f - t;
            return u * u * u * a
                   + 3f * u * u * t * controlA
                   + 3f * u * t * t * controlB
                   + t * t * t * b;
        }

        // defender-drop-dismount unit 0 — 드롭 하마(下馬) 궤적 평가: 반동(Hermite)→솟음·착지(수직 끝접선 아치).
        // t01 ∈ [0,1] 전체 정규화 시간. recoilFrac = 반동 구간 비율. startVel 은 **반동 구간 정규화 시간 기준
        // 접선**(호출측이 월드속도 × 반동초 로 스케일해 전달) — 릴리스 잔여 스윙 속도를 반동에 흡수한다.
        // 구간 경계는 C0(위치)만 연속 — C1 불연속은 의도(분리 순간의 스냅). 시간 이징은 호출측 책임(기하만).
        //   dip = start − camUp·dipDistance
        //   arcHeight = max(|dip−end|·arcHeightFactor, minArcHeight)   ← 절대 하한 = "솟음" 보장(계약)
        //   c1 = Lerp(dip, end, launch.x) + camUp·(arcHeight·launch.y)
        //   c2 = end + camUp·(arcHeight·landingHeight)                 ← end 직상방 = 끝접선 순수 -camUp(스틱 착지)
        public static Vector3 DismountPoint(
            Vector3 start, Vector3 startVel, Vector3 end, Vector3 camUp,
            float recoilFrac, float dipDistance,
            float arcHeightFactor, float minArcHeight, Vector2 launch, float landingHeight,
            float t01)
        {
            float t = Mathf.Clamp01(t01);
            recoilFrac = Mathf.Clamp(recoilFrac, 1e-4f, 0.9f);
            Vector3 dip = start - camUp * dipDistance;

            if (t <= recoilFrac)
            {
                // 반동: Hermite — p0=start(접선 startVel), p1=dip(접선 0). startVel=0 이면 smoothstep 단조 하강.
                float s = t / recoilFrac;
                float s2 = s * s, s3 = s2 * s;
                return (2f * s3 - 3f * s2 + 1f) * start
                     + (s3 - 2f * s2 + s) * startVel
                     + (-2f * s3 + 3f * s2) * dip;
            }

            // 솟음·착지: dip→end 베지어. c2 가 end 직상방이라 끝접선 = 3(end−c2) = 순수 -camUp.
            float u = (t - recoilFrac) / (1f - recoilFrac);
            float arcHeight = Mathf.Max(Vector3.Distance(dip, end) * arcHeightFactor, minArcHeight);
            Vector3 c1 = Vector3.Lerp(dip, end, launch.x) + camUp * (arcHeight * launch.y);
            Vector3 c2 = end + camUp * (arcHeight * landingHeight);
            return CubicBezier(dip, c1, c2, end, u);
        }

        // flight-lift-feel unit 0 — 비행 구간 시간 재매핑(ease-out-in). 양 끝이 빠르고 중간이 느리다:
        // 초반 급상승 → 정점 체공 → 후반 급하강. **기하는 불변, 시간만 재분배한다.**
        //   power = 1  → 항등 (현행 선형과 byte-identical)
        //   power < 1  → 리듬 강화 (0.7 근처가 기본 대역)
        //
        // ⚠ **비행 구간에만** 적용한다. 호출측 규약:
        //     u    = (t01 − recoilFrac) / (1 − recoilFrac)
        //     t01' = recoilFrac + (1 − recoilFrac) × FlightTimeRemap(u, power)
        //   반동(웅크림) 구간까지 왜곡하면 힘 모으는 타이밍이 흔들린다.
        //
        // 왜 Out* 이 아닌가: DismountPoint 의 "시간 이징 없음(선형)" 계약은 Out* 이징이 끝속도를 0 으로
        // 죽여 스틱 착지를 물러지게 하기 때문에 세운 것이다. ease-out-in 은 끝속도를 **키우므로**
        // 그 계약과 충돌하지 않는다 — 오히려 착지 임팩트를 강화한다.
        // 총 시간은 바뀌지 않는다 — 드롭의 "비행 창 ⊆ pending 창" 계약이 그대로 산다.
        public static float FlightTimeRemap(float u, float power)
        {
            u = Mathf.Clamp01(u);               // 정의역 보장은 분기 앞 — 조기 반환도 같은 계약을 지킨다
            if (power >= 0.999f) return u;      // 항등 조기 반환 — 기본값 경로에 pow 비용·오차 0
            float p = Mathf.Max(0.05f, power);  // 0 은 계단 함수라 금지
            return u < 0.5f
                ? 0.5f * Mathf.Pow(2f * u, p)
                : 1f - 0.5f * Mathf.Pow(2f - 2f * u, p);
        }
    }
}
