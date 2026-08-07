using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // continuous-agent-movement unit 8 — 에이전트 간 겹침 해소.
    //
    // 점 충돌 + 셀 clamp 시절에는 셀 경계가 겹침을 우연히 억제했다. 연속 이동으로 바꾸면
    // 그 억제가 사라져 적들이 서로를 관통한 채 한 점에 뭉친다.
    //
    // ⚠ **순서 무관이어야 한다.** A→B 를 먼저 처리하면 B→A 가 갱신된 위치를 보게 되어
    // 순회 순서에 따라 결과가 갈리고 결정론이 깨진다. 그래서 모든 밀어냄을 **먼저 누적하고**
    // 그 뒤에 한 번 적용한다 — 이 클래스는 누적분만 계산하고 적용은 호출자가 한다.
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
