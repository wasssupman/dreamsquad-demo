using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // continuous-agent-movement unit 8 — 에이전트 간 겹침 해소.
    //
    // 점 충돌 + 셀 clamp 시절에는 셀 경계가 겹침을 우연히 억제했다. 연속 이동으로 바꾸면
    // 그 억제가 사라져 적들이 서로를 관통한 채 한 점에 뭉친다.
    //
    // ⚠ **누적이 먼저, 적용은 나중에.** A→B 를 먼저 *적용*하면 B→A 가 갱신된 위치를 보게 되어
    // 순회 순서에 따라 결과가 갈린다. 그래서 모든 밀어냄을 **먼저 누적하고** 그 뒤에 한 번
    // 적용한다 — 이 클래스는 누적분만 계산하고 적용은 호출자가 한다.
    //
    // 이 2단계로 **순차 위치 갱신 의존은 제거됐다**. 다만 "순서 무관"까지 간 것은 아니다 —
    // float 덧셈은 결합법칙이 없어서 3항 이상 누적은 더한 *순서*에 마지막 비트(1 ULP)가
    // 의존한다. 그 순서는 호출자의 순회 순서에서 오고, 순회 순서는 청크 배치 = 구조 변경
    // (스폰·사망) 이력에서 온다.
    //   - 전체 리플레이(같은 커맨드 → 같은 구조 변경 순서 → 같은 청크 배치): 안전
    //   - 스냅샷 부분 재시뮬: 누적 순서가 갈릴 수 있어 위험
    // 해소는 stable id 정렬이며 그 축(SimEntityId)은 docs/spec/battle-sim-extraction/ unit 1
    // 소관이다 — 여기서 구현하지 않는다. 실패 사례는 SeparationTests 의 [Ignore] 테스트 참조.
    //
    // 순수 함수. plain 값만 받는다.
    public static class Separation
    {
        // 소프트 분리 — 밀어내되 관통을 하드 블록하지 않는다.
        // 1타일 복도에서 하드 블록은 교착을 만든다(정체는 게임플레이지만 교착은 버그다).
        public const float DefaultStrength = 0.5f;

        // self 가 other 로부터 받는 밀어냄(XZ). 겹치지 않으면 zero.
        //
        // 겹침 깊이에 비례하되 strength 로 감쇠한다 — 즉시 완전 분리하면 튕겨 나가고,
        // 여러 프레임에 걸쳐 풀면 밀집 대열이 자연스럽게 벌어진다.
        //
        // strength 의 단위는 **프레임당**이지 초당이 아니다(dt 를 곱하지 않는다). 계약 전문은
        // AgentSeparationSystem 헤더 참조.
        public static float2 PairPush(float3 self, float3 other, float radiusSum, float strength)
        {
            float dx = self.x - other.x;
            float dz = self.z - other.z;
            float d2 = dx * dx + dz * dz;
            if (d2 >= radiusSum * radiusSum) return float2.zero;

            // 정확히 겹친 경우(같은 좌표 스폰) 방향이 없다. 이때 임의 방향을 주면 난수가
            // 필요하고 결정론이 깨진다 — zero 를 돌려 다음 프레임에 다른 요인이 벌리게 둔다.
            if (d2 < 1e-8f) return float2.zero;

            float d = math.sqrt(d2);
            float overlap = radiusSum - d;
            return new float2(dx / d, dz / d) * (overlap * strength);
        }

        // unit 13 — 정지한(자기주도 이동을 하지 않은) 유닛이 받는 밀어냄에서 **전진 성분만**
        // 걷어낸다. 뒤로·옆으로 밀리는 건 그대로 받는다.
        //
        // 왜 필요한가: 시뮬이 "여기서 멈춘다"고 정한 유닛(가디언 교전 등)을 뒤 무리가 경로를
        // 따라 4~9타일 밀어 나른다(실측, Zig 9.15타일). 사거리 판정과 배치 의도가 함께 무너진다.
        //
        // 왜 프레임당 상한(maxPush)이 아닌가: 밀림은 큰 한 방이 아니라 **수백 프레임 누적
        // 압력**이다. 실제 밀어냄은 `겹침 × strength` 라 상한선에 애초에 닿지 않아서, 상한을
        // 0.05·0.02 로 낮춰도 밀림이 그대로였다(6.47 → 6.47 → 6.42타일). 상한은 폭주 방지용이지
        // 누적 방지용이 아니다.
        //
        // 왜 전면 차단(앵커)이 아닌가: 밀림은 사라지지만 폭1 복도에서 마개가 **진짜 벽**이 되어
        // 뒤가 통과하지 못한다(통과 8~13/15 로 붕괴). 옆·뒤 성분을 남겨야 뭉침이 계속 풀린다.
        //
        // forward 는 호출자가 그 유닛이 선 셀의 flow 에서 매 프레임 파생한다 — 앵커 좌표를
        // 저장하지 않는다(저장하면 무효화 시점이 생긴다). zero 면 원본 그대로.
        // forward 는 안에서 정규화한다 — 호출자가 flow 값을 그대로 넘겨도 투영이 왜곡되지
        // 않게(비단위 벡터를 그대로 쓰면 제거량이 |forward|² 배로 어긋난다).
        public static float2 RejectForwardPush(float2 accumulated, float2 forward)
        {
            float2 dir = math.normalizesafe(forward);
            if (math.lengthsq(dir) < 1e-8f) return accumulated;
            float along = math.dot(accumulated, dir);
            return along > 0f ? accumulated - dir * along : accumulated;
        }

        // 누적분을 실제 변위로 바꾼다. 한 프레임에 밀려나는 양을 상한해 폭주를 막는다
        // (밀집 대열에서 누적이 커지면 튕겨 나간다).
        public static float3 ApplyAccumulated(float3 position, float2 accumulated, float maxPush)
        {
            float len2 = math.lengthsq(accumulated);
            if (len2 < 1e-8f) return position;

            float len = math.sqrt(len2);
            float scale = len > maxPush ? maxPush / len : 1f;
            return new float3(
                position.x + accumulated.x * scale,
                position.y,
                position.z + accumulated.y * scale);
        }
    }
}
