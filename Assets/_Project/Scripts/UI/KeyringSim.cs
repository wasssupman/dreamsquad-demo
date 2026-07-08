using UnityEngine;

namespace Wassup.UI
{
    // keyring-unify 0 — 인게임/아웃게임 키링 공통 수학(스프링 추종·기울임각 공용, 낙하는 아웃게임 전용).
    // 순수 static, 좌표계 비의존(Vector3 본체 + Vector2 포워딩 오버로드 — z=0, bit-exact).
    // dt clamp(Mathf.Max(dt, 1e-4f))·초기화·재잡기·좌표 산출은 호출측 책임.
    public static class KeyringSim
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

        // Vector2 포워딩 오버로드(아웃게임 캔버스 px) — Vector3 본체에 위임. z=0 왕복이라 bit-exact.
        // 호출측이 마샬링을 직접 하면 copy-back 누락이 무증상 풋건이 되므로 여기서 흡수한다.
        public static void SpringStep(ref Vector2 pos, ref Vector2 vel, Vector2 target,
            float spring, float damping, float maxSpeed, float dt)
        {
            Vector3 p = pos, v = vel;
            SpringStep(ref p, ref v, target, spring, damping, maxSpeed, dt);
            pos = p;
            vel = v;
        }

        // 줄(→고리) 방향 → 기울임각(deg, ±maxAngle 클램프). 내부 정규화 금지 —
        // 입력은 호출측 그대로(인게임: 단위벡터의 camRight/camUp 투영 = 비단위 2D, 아웃게임: 단위 2D).
        // y 의 1e-3 floor 는 수평/역방향 퇴화 방지 — 스케일 불변 아님, 현행 동작 보존이 우선.
        public static float LeanAngle(float x, float y, float maxAngle)
        {
            return Mathf.Clamp(-Mathf.Atan2(x, Mathf.Max(y, 1e-3f)) * Mathf.Rad2Deg, -maxAngle, maxAngle);
        }

        // 중력 적분 + 착지/반동 판정. 반환 true = 반동 없이 바닥에 정착(착지 속도 < bounceMinSpeed).
        // lobby-keyring-drag 2 의 LobbyKeyringDrag.FallStep 에서 이동.
        public static bool FallStep(ref float y, ref float velY, float floorY, float dt,
            float gravity, float bounceDamping, float bounceMinSpeed)
        {
            velY -= gravity * dt;
            y += velY * dt;
            if (y > floorY) return false;
            y = floorY;
            float impact = -velY; // 착지 속도(양수)
            if (impact >= bounceMinSpeed && bounceDamping > 0f)
            {
                velY = impact * bounceDamping;
                return false;
            }
            velY = 0f;
            return true;
        }
    }
}
