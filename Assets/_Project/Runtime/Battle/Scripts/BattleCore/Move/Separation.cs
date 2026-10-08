// salvaged from Assets/_Project/Scripts/Battle/Movement/Separation.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: 없음(순수 수학). 다만 **헤더의 1 ULP 경고는 이제 유효하지 않다** —
// 누적 순회가 `SimEntityId` 오름차순으로 닫혔다(M27 · 계약 5). 그 문장을 아래로 옮겨 적었다.
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Move
{
    // 에이전트 간 겹침 해소.
    //
    // ⚠ **누적이 먼저, 적용은 나중에.** A→B 를 먼저 *적용*하면 B→A 가 갱신된 위치를 보게 되어
    // 순회 순서에 따라 결과가 갈린다. 그래서 모든 밀어냄을 **먼저 누적하고** 그 뒤에 한 번
    // 적용한다 — 이 클래스는 누적분만 계산하고 적용은 호출자가 한다.
    //
    // ⚠ float 덧셈은 결합법칙이 없어 3항 이상 누적은 더한 **순서**에 마지막 비트가 의존한다.
    // 옛 전투는 그 순서가 청크 배치(스폰·사망 이력)에서 와서 스냅샷 부분 재시뮬이 위험했다.
    // 새 코어는 `SimEntityId` 오름차순으로 누적해 그 축을 **닫았다**(M27) — 순서가 값이 됐다.
    public static class Separation
    {
        // 소프트 분리 — 밀어내되 관통을 하드 블록하지 않는다.
        // 1칸 복도에서 하드 블록은 교착을 만든다(정체는 게임플레이지만 교착은 버그다).
        //
        // ⚠ 단위는 **틱당**이다(dt 를 곱하지 않는다). 옛 값은 「프레임당 0.5」였고 프레임률이
        // 가변이었다. 고정 틱 1/60 이면 「프레임당」과 「틱당」이 같은 뜻이므로 **의미를 그대로**
        // 옮겼다 — 값 자체의 재검토는 플레이 뒤다(M13 · 이식 제외 표).
        public const float DefaultStrength = 0.5f;

        // self 가 other 로부터 받는 밀어냄(XZ). 겹치지 않으면 zero.
        //
        // 겹침 깊이에 비례하되 strength 로 감쇠한다 — 즉시 완전 분리하면 튕겨 나가고,
        // 여러 틱에 걸쳐 풀면 밀집 대열이 자연스럽게 벌어진다.
        public static float2 PairPush(float3 self, float3 other, float radiusSum, float strength)
        {
            float dx = self.x - other.x;
            float dz = self.z - other.z;
            float d2 = dx * dx + dz * dz;
            if (d2 >= radiusSum * radiusSum) return float2.zero;

            // 정확히 겹친 경우(같은 좌표 스폰) 방향이 없다. 임의 방향을 주면 난수가 필요하고
            // 결정론이 깨진다 — zero 를 돌려 다음 틱에 다른 요인이 벌리게 둔다.
            if (d2 < 1e-8f) return float2.zero;

            float d = math.sqrt(d2);
            float overlap = radiusSum - d;
            return new float2(dx / d, dz / d) * (overlap * strength);
        }

        // 정지한(자기주도 이동을 하지 않은) 유닛이 받는 밀어냄에서 **전진 성분만** 걷어낸다.
        // 뒤로·옆으로 밀리는 건 그대로 받는다.
        //
        // 왜 필요한가: 코어가 「여기서 멈춘다」고 정한 유닛(교전 중)을 뒤 무리가 경로를 따라
        // 4~9칸 밀어 나른다(실측 9.15칸). 사거리 판정과 배치 의도가 함께 무너진다.
        //
        // 왜 틱당 상한이 아닌가: 밀림은 큰 한 방이 아니라 **수백 틱 누적 압력**이다. 실제
        // 밀어냄은 `겹침 × strength` 라 상한선에 애초에 닿지 않아, 상한을 낮춰도 밀림이
        // 그대로였다. 상한은 폭주 방지용이지 누적 방지용이 아니다.
        //
        // 왜 전면 차단(앵커)이 아닌가: 밀림은 사라지지만 폭1 복도에서 마개가 **진짜 벽**이 되어
        // 뒤가 통과하지 못한다. 옆·뒤 성분을 남겨야 뭉침이 계속 풀린다.
        //
        // forward 는 호출자가 그 유닛이 선 칸의 흐름에서 매 틱 파생한다 — 앵커 좌표를 저장하지
        // 않는다(저장하면 무효화 시점이 생긴다). 안에서 정규화한다.
        public static float2 RejectForwardPush(float2 accumulated, float2 forward)
        {
            float2 dir = math.normalizesafe(forward);
            if (math.lengthsq(dir) < 1e-8f) return accumulated;
            float along = math.dot(accumulated, dir);
            return along > 0f ? accumulated - dir * along : accumulated;
        }

        // 누적분을 실제 변위로 바꾼다. 한 틱에 밀려나는 양을 상한해 폭주를 막는다.
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
