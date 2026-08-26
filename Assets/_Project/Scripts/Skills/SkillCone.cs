using Unity.Mathematics;

namespace Wassup.Skills
{
    // skill-layer-migration unit 8 — 콘(부채꼴) 판정. `TileAoe.IsInCone` 에서 이사했다.
    //
    // 왜 이사했나: 이 판정의 유일한 프로덕션 호출처가 화염 브레스 arm 이었고, 그 arm 이
    // 도메인으로 오면서 판정도 따라와야 했다(도메인은 `Wassup.Battle` 을 못 본다).
    // `TileAoe` 쪽은 위임만 남겨 기존 테스트와 문서 참조를 살린다 — 복사본을 만들면
    // 「저작값 하나가 두 표현으로 갈린다」는 이 판정이 피하려던 바로 그 사고가 된다.
    public static class SkillCone
    {
        // «같은 자리» 임계(월드 거리²). 0.01 월드 유닛 = 타일 1개 기준 1% — 셀 판정을
        // 흔들지 않으면서 방향 계산이 의미를 잃는 구간만 잡는다.
        public const float SameSpotEpsSq = 1e-4f;

        public static bool IsInCone(float2 from, float2 to, float2 dir, float cosSq, float rangeWorld)
        {
            float2 d = to - from;
            float d2 = math.lengthsq(d);
            // 같은 자리 = 포함. 델타가 0 에 가까우면 방향이 무의미해져 아래 부호 가드가
            // 대상을 조용히 제외한다. 비행 적은 sim 좌표가 평면이라 바로 아래 유닛과
            // XZ 가 겹칠 수 있다.
            if (d2 <= SameSpotEpsSq) return true;
            if (d2 > rangeWorld * rangeWorld) return false;
            float dp = math.dot(d, dir);
            // normalize 를 쓰지 않는다 — 제곱 비교로 rsqrt 왕복을 없앤다.
            // ⚠ 그 대가로 **부호 가드가 필수**다. 없으면 등 뒤에 대칭 콘이 생긴다.
            // 그리고 cos²θ = cos²(180−θ) 라 정의역이 90° 에서 잘린다 — 저작 검증은
            // bake 가 한다(120° 를 조용히 60° 로 돌리지 않기 위해).
            return dp > 0f && dp * dp >= cosSq * d2;
        }
    }
}
