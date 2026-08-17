using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile
{
    // dreamcatcher-content-5 unit 1 — MovementKind.BoomerangReturn 의 궤적 수학.
    // 순수 static, Burst 호환, EditMode 고정 (Orbit/BallisticArc/Bezier3 와 같은
    // 형태·같은 거주지).
    //
    // sim 은 XZ 평면만 간다 — 화면 높이는 탄 SO 의 visualHeightOffset 이 준다
    // (BoardSpace.ToView 가 sim-Y 를 drop 하므로 sim Y 에 높이를 구워도 안 보인다).
    public static class Boomerang
    {
        // 발사 축을 따라 maxDistance 까지 나갔다가 같은 축을 되짚어 발사점으로 돌아온다.
        //
        // ⚠ `axis` 는 **불변 입력**이다. 호출하는 arm 이 「돌아오는 중이니 축을 뒤집자」로
        // 상태를 갱신하면 다음 프레임이 origin 반대편을 계산해 **발사점 뒤로 날아간다**.
        // 궤도(Orbit)에서 direction 을 매 프레임 쓰는 것과 정반대다 — 거긴 접선이 순수한
        // 파생값이라 아무것도 되먹이지 않는다.
        //
        // 「지금 어느 다리인가」는 `returning` 으로 **알려주기만** 하고 저장하지 않는다
        // (content-5 계약 5). 진행 방향이 필요한 곳(넉백)은 그 프레임 스윕에서 뽑고,
        // 화면 facing 은 뷰가 직전 위치와의 차이로 이미 만든다.
        //
        // 퇴화 저작(speed<=0 / maxDistance<=0)은 여기서 클램프하지 않는다 — 그러면
        // 왕복 완료 조건이 영원히 거짓인 **불멸 투사체**가 조용히 살아남는다. 드레인
        // (SpawnProjectile)이 스폰 단계에서 loud 거절한다(DirectionalLinear 선례).
        public static float3 Position(float3 origin, float2 axis, float maxDistance,
                                      float speed, float elapsed, out bool returning)
        {
            float traveled = speed * elapsed;
            returning = traveled > maxDistance;
            // 나가는 다리: traveled · 돌아오는 다리: 2*maxDistance − traveled.
            // 왕복을 마치면 이 값이 음수로 내려가지만 그 프레임의 위치가 필요한 것은
            // 마지막 스윕 하나뿐이고, 소멸 판정은 IsComplete 가 따로 한다.
            // ⚠ 완료 프레임에는 이 값이 음수로 내려간다(오버슛 최대 speed*dt). 클램프하지
            // 않으면 **마지막 스윕 선분이 발사점 뒤로 뻗어** 뒤에 서 있던 적을 때린다 —
            // 계약에 없는 피해 사건이다. 소멸 판정은 IsComplete 가 따로 하므로 여기서
            // 0 으로 접어도 수명은 그대로다.
            float along = math.max(returning ? 2f * maxDistance - traveled : traveled, 0f);
            return origin + new float3(axis.x, 0f, axis.y) * along;
        }

        // 왕복 1회에 걸리는 시간. 도착 판정과 EditMode 고정이 같은 식을 공유한다.
        public static float TotalTime(float maxDistance, float speed)
            => speed > 0f ? 2f * maxDistance / speed : float.PositiveInfinity;

        // 왕복 완료 = 되짚어 온 거리가 편도의 2배. 거리 비교가 아니라 **누적 시간**으로
        // 판정하는 이유: 발사점 복귀를 위치로 보면 부동소수 경계에서 한 프레임 튄다.
        // elapsed 는 궤도가 이미 쓰는 투사체 로컬 시계라 결정론이 투사체 안에서 닫힌다.
        public static bool IsComplete(float maxDistance, float speed, float elapsed)
            => speed * elapsed >= 2f * maxDistance;
    }
}
