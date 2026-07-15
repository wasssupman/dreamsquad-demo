using UnityEngine;

namespace Wassup.DepthParallax
{
    // depth-parallax unit 1 — 틸트/뎁스 → UV 오프셋 결정 + 틸트 스프링 스텝의 아키텍처-blind 순수 함수.
    // UnityEngine(Vector2) 외 의존 없음. 무의존 모듈 경계라 Wassup.Runtime.KeyringSim 을 참조할 수 없어
    // SpringStep 은 검증된 임계감쇠 스프링의 로컬 포트다(README 공통원칙 "스프링 수학 중복").
    public static class DepthParallaxMath
    {
        // 뎁스 힌지 UV 오프셋. 중심 피벗 = depthCenter 기준 near/far 가 반대로 힌지.
        // 부호(depthSign)는 힌지 뺄셈 *후* 전체 항에 곱한다 — raw 뎁스에 먼저 곱하면 힌지가
        // [0,1] 밖으로 밀려 극성 플립이 깨진다. 셰이더 Cue A 와 같은 곱셈 순서로 계약 일치.
        // tilt==(0,0) 또는 depth==depthCenter 면 결과 0(rest no-op / 힌지 평면 정지).
        public static Vector2 UvOffset(Vector2 tilt, float depth, float depthCenter, float amplitude, float depthSign)
        {
            return tilt * (depth - depthCenter) * amplitude * depthSign;
        }

        // 무게추 스프링+감쇠+속도상한 적분(KeyringSim.SpringStep(Vector2) 포트). maxSpeed <= 0 = 무제한.
        // dt clamp·초기화·재잡기는 호출측 책임. target==pos & vel==0 이면 no-op(rest 유지).
        public static void SpringStep(ref Vector2 pos, ref Vector2 vel, Vector2 target,
            float spring, float damping, float maxSpeed, float dt)
        {
            Vector2 accel = (target - pos) * spring - vel * damping;
            vel += accel * dt;
            if (maxSpeed > 0f)
            {
                float sp = vel.magnitude;
                if (sp > maxSpeed) vel *= maxSpeed / sp;
            }
            pos += vel * dt;
        }
    }
}
