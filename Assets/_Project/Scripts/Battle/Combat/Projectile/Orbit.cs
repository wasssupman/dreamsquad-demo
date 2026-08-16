using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile
{
    // dreamcatcher-content-4 unit 1 — MovementKind.OrbitAroundPoint 의 궤적 수학.
    // 순수 static, Burst 호환, EditMode 고정 (BallisticArc/Bezier3/SkyFall 과 같은
    // 형태·같은 거주지).
    //
    // sim 은 XZ 평면만 돈다 — 화면 높이는 탄 SO 의 visualHeightOffset 이 준다
    // (BoardSpace.ToView 가 sim-Y 를 drop 하므로 sim Y 에 높이를 구워도 안 보인다.
    // BallisticArc 의 아치가 view 공간에 사는 것과 같은 이유).
    public static class Orbit
    {
        // 시작 각도는 0 고정이다: 발사마다 위상을 흔들면 같은 입력의 리플레이가 갈린다
        // (구조적 결정론 — seeded RNG 금지). 화염구를 여러 개 균등 위상으로 띄우는
        // 날에도 발사 arm 이 elapsed 오프셋을 나눠 주면 되고 이 함수는 그대로다.
        //
        // 각속도 음수 = 역회전. 반경 0 은 중심에 붙어 도는 퇴화 궤도로 안전하게 흐른다
        // (분모가 없어 NaN 이 생기는 지점이 하나도 없다).
        // content-4 unit 8 — `phase`(rad)로 **여러 구슬을 같은 궤도에 균등 배치**한다.
        // 위상을 elapsed 오프셋으로 흉내낼 수는 없다: elapsed 는 수명도 재는 값이라
        // 앞당기면 그 구슬만 일찍 죽는다. 그래서 각도에만 더하는 별도 축이다.
        // 기본 0 = 종전 동작(호출처 다수가 4인자 그대로).
        public static float3 Position(float3 center, float radius, float angularSpeed, float elapsed,
                                      float phase = 0f)
        {
            math.sincos(angularSpeed * elapsed + phase, out float s, out float c);
            return center + new float3(c, 0f, s) * radius;
        }

        // 구슬 i(0-based)의 위상 — n개를 원 둘레에 균등 배치한다. n<=1 이면 0.
        // 분할을 이 파일에 두는 이유는 Position/Tangent 와 **같은 각도 규약**을 쓰기
        // 때문이다. 발사 arm 이 2π/n 을 다시 유도하면 규약이 두 곳으로 갈린다.
        public static float PhaseOf(int index, int count)
            => count <= 1 ? 0f : 2f * math.PI * index / count;

        // 진행 방향(단위 벡터, sim 평면 (x,z)) — 위 원의 미분 r·ω·(-sinθ, cosθ) 을
        // 정규화한 것. PathHit 이 한 프레임에 여러 명을 스쳤을 때 front-most 정렬
        // (`dot(victim - prev, direction)`)이 이 벡터를 쓴다.
        //
        // 크기를 버리고 **부호만** 남기는 이유 둘: ① 정렬은 방향만 보므로 r·ω 를
        // 실어봐야 순서가 같고, ② ω=0(저작 실수로 멈춘 궤도)에서도 단위 벡터가
        // 나온다. 0 벡터를 남기면 정렬이 조용히 무의미해져서, 나중에 이 궤적을
        // 관통 예산과 함께 쓰는 사람이 원인을 못 찾는 함정이 된다.
        //
        // 이 파일에 사는 이유: Position 과 같은 원의 미분이라 축 규약((x,z)=float2)과
        // 회전 부호를 한 곳에서만 정한다 — arm 이 다시 유도하면 조용히 틀린다.
        public static float2 Tangent(float angularSpeed, float elapsed, float phase = 0f)
        {
            math.sincos(angularSpeed * elapsed + phase, out float s, out float c);
            float2 t = new float2(-s, c);
            return angularSpeed < 0f ? -t : t;
        }
    }
}
