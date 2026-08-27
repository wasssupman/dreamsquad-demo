using Unity.Mathematics;

namespace Wassup.Skills
{
    // skill-layer-migration unit 1 — 「어디를 쏘나」를 정하는 순수 규칙.
    //
    // ⚠ **새로 쓴 것이 아니라 옮긴 것이다.** 원본은
    // `Battle/Combat/Projectile/Emission/OnPlaceFireAim.cs` 였고, 규칙·상수·비교
    // 부등호까지 그대로다(`SkillAimTests` 가 그 무회귀를 잡고 있다 — 파일도 같이 개명됐다).
    // 옮긴 이유는 하나뿐이다: **concrete 가 이 규칙을 호출해야 하는데 `Wassup.Skills`
    // 는 Battle 을 참조하지 않는다**(계약 1). 도메인이 쓰는 규칙은 도메인에 산다.
    //
    // 후보 컨테이너가 `NativeArray` → `float2[]` 로 바뀐 것도 같은 이유다 —
    // 이 어셈블리는 `Unity.Mathematics` 하나만 참조한다. 호출처 둘 다 관리 코드라
    // (디스패처는 managed `SystemBase`, 브리지는 MonoBehaviour) Burst 손실은 없다.
    //
    // 규칙 자체는 사용자 결정(2026-08-15, `defender-on-place-skills` unit 4):
    // **조준이 있으면 그 방향, 없으면 가장 가까운 후보.** 조준이 최근접보다 세다 —
    // 조준은 방향만 정하고 「쏠 만한가」는 호출처가 이미 판정했으므로, 조준 방향에
    // 아무도 없어도 발사는 일어나고 명중이 0일 수 있다(어디를 쏠지는 플레이어 몫).
    public static class SkillAim
    {
        // 후보는 **호출처가 이미 거른 «이번 프레임 합법 후보»** 여야 한다(살아 있고 · 판 안이고 ·
        // host 가 때릴 수 있는 통행 층이고 · 사거리 안). 이 함수는 그 판정을 하지 않는다 —
        // 필터를 여기 두면 sim 과 브리지가 서로 다른 후보 개념을 갖게 된다.
        //
        // 반환 false = 「쏠 방향이 없다」. 호출처가 발사를 취소할지(규칙 경로) 자기 폴백을
        // 쓸지(브리지 레거시는 (0,1)) 정한다.
        //
        // `count` 는 `candidateXZ` 의 **유효 앞부분 길이**다. 호출처가 재사용 버퍼를
        // 쓰기 때문에 배열 길이와 후보 수가 다르다 — 배열 길이를 믿으면 지난 프레임의
        // 후보가 총구를 가져간다.
        public static bool TryResolve(float2 hostXZ, bool hasAim, float2 aim,
                                      float2[] candidateXZ, int count,
                                      out float2 dir, out int pickedIndex)
        {
            pickedIndex = -1;
            if (hasAim && math.lengthsq(aim) > AimEpsilonSq)
            {
                dir = math.normalize(aim);
                return true;
            }

            // 동률은 **낮은 index 가 이긴다**(엄격 `<`). 좌표가 연속이라 정확한 동률은 드물지만,
            // 이 프로젝트는 같은 자리에서 두 번 결정론을 명시했다(자장가가 셀 거리 대신 월드
            // 거리를 쓰는 이유 · fan-out 의 row-major 정렬). 선택 index 를 밖으로 돌려주는 것도
            // 그래서다 — 호출처가 「누구를 겨눴나」를 같은 규칙으로 재계산하지 않게.
            int n = candidateXZ == null ? 0 : math.min(count, candidateXZ.Length);
            float bestDistSq = float.MaxValue;
            float2 best = float2.zero;
            for (int i = 0; i < n; i++)
            {
                float2 to = candidateXZ[i] - hostXZ;
                float d2 = math.lengthsq(to);
                // 중심에 정확히 겹친 후보는 방향을 못 준다(정규화가 NaN). 배제하고 다음 후보로.
                if (d2 < DegenerateDistSq || d2 >= bestDistSq) continue;
                bestDistSq = d2;
                best = to;
                pickedIndex = i;
            }

            if (pickedIndex < 0)
            {
                dir = float2.zero;
                return false;
            }
            dir = math.normalize(best);
            return true;
        }

        // 조준 벡터가 «있다» 고 볼 최소 길이². `DeployedFacing` 은 격자 단위 벡터라 정상값은 1 이고,
        // 미배치/퇴화만 0 근처다.
        public const float AimEpsilonSq = 0.001f;

        // 후보가 host 와 같은 지점인지 판정하는 하한². **비교도 레거시와 같은 `<` 다** — 정확히
        // 이 값인 후보 하나가 갈리는 차이지만, 무회귀는 «거의 같다» 가 아니다(리뷰 L2).
        public const float DegenerateDistSq = 0.001f;
    }
}
