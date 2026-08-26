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

        // elite-enemy-tier unit 1 — 방향성 광역(부채꼴) 멤버십. 드래곤 화염 브레스가 유일한
        // 소비자다. 위 반경 판정과 **같은 계층**에 두는 이유: 둘 다 "이 대상이 이 광역 안인가"
        // 라는 한 가지 순수 술어이고, 도형을 데이터로 추상화하는 계층(EffectArea)은 소비자가
        // 도형을 런타임에 고르지 않아 과설계로 판정돼 접혔다(docs/spec/elite-enemy-tier/
        // 1_cone_predicate.md 에 근거 전문).
        //
        // ★**셀이 아니라 월드 XZ 로 판정한다.** 위 반경 판정이 셀인 것은 착탄이 셀에 락돼
        // 있어서(격자가 정의 그 자체)지만, 브레스는 연속 이동하는 비행 유닛에서 나가고 조준
        // 방향도 월드에서 만들어진다. 멤버십만 이산으로 하면 반경 1~2타일에서 셀 중심 양자화가
        // 방향 판정을 최대 ~45° 흔든다. 사거리만 타일→월드로 환산해 넘긴다.
        // 선례: AttackReach.InReach 도 «사거리는 타일 · 미세 판정은 월드» 하이브리드다.
        //
        // 인자: from=시전자 XZ · to=대상 XZ · dir=**정규화된** 조준 방향 · cosSq=cos²(반각) ·
        // rangeWorld=사거리(월드). cosSq 는 저작 각도에서 bake 가 1회 변환한다(sim 은 삼각함수를
        // 부르지 않고, 저작값 하나가 두 표현으로 갈리지 않는다).
        //
        // ⚠ **정의역은 반각 (0°, 90°) 다.** 아래 `dp > 0` 부호 가드는 제곱이 부호를 잃어
        // **등 뒤에 대칭 콘이 생기는 것**을 막는 필수 조건인데, 동시에 정의역을 90° 로 자른다.
        // 게다가 cos²θ = cos²(180−θ) 라 저작 120° 는 조용히 60° 콘으로 동작하고 180° 는
        // «전방위» 가 아니라 «정면 한 줄» 이 된다. 그래서 **bake 가 반각 >= 90 을 거절**한다 —
        // 이 함수는 클램프하지 않고 정의역을 문서로만 방어한다(순수 술어의 책임 밖).
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
