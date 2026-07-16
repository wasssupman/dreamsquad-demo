using UnityEngine;

namespace Wassup.UI
{
    // placement-cell-snap unit 0 — 포커스 셀 선택에 히스테리시스(2D 슈미트)를 주는 순수 정책 함수.
    // 좌표 변환(origin/tileSize)은 호출부(bridge read 헬퍼)가 담당하고, 이 함수는
    // "이전 셀 + 소수 셀 좌표 + 여유 → 새 정수 셀" 만 결정한다. 아키텍처 타입 미참조 → EditMode 테스트.
    //
    // frac = (sim - boardOrigin) / tileSize 의 (x, z). GridMath.WorldToCell 과 같은 공간:
    //   셀 중심 = 정수, 셀 i 의 range = [i-0.5, i+0.5)  (round = floor(f + 0.5)).
    // 경계 근처 지터를 흡수하기 위해, 현재 셀을 [i-(0.5+margin), i+(0.5+margin)) 밴드 안에서 유지한다.
    public static class PlacementCellSnap
    {
        // margin 0.5 이상이면 이웃 셀 진입이 불가해지므로 상한을 둔다.
        private const float MaxMargin = 0.49f;

        public static Vector2Int Resolve(Vector2Int? current, Vector2 frac, float stickMargin, Vector2Int gridSize)
        {
            float margin = Mathf.Clamp(stickMargin, 0f, MaxMargin);

            int cx, cy;
            if (current.HasValue)
            {
                cx = ResolveAxis(current.Value.x, frac.x, margin);
                cy = ResolveAxis(current.Value.y, frac.y, margin);
            }
            else
            {
                cx = RoundAxis(frac.x);
                cy = RoundAxis(frac.y);
            }

            return new Vector2Int(
                Mathf.Clamp(cx, 0, Mathf.Max(0, gridSize.x - 1)),
                Mathf.Clamp(cy, 0, Mathf.Max(0, gridSize.y - 1)));
        }

        // half-away-from-zero (floor(f + 0.5)) — GridMath.WorldToCell 과 동일. banker's rounding 회피.
        private static int RoundAxis(float f) => Mathf.FloorToInt(f + 0.5f);

        // 현재 축을 [current-(0.5+margin), current+(0.5+margin)) 밴드 안에서 유지, 벗어나면 재반올림.
        // 상한은 exclusive 라 margin=0 이면 셀 range [i-0.5, i+0.5) 와 정확히 일치 → 순수 round 와 동등.
        private static int ResolveAxis(int current, float f, float margin)
        {
            float half = 0.5f + margin;
            if (f >= current - half && f < current + half) return current;
            return RoundAxis(f);
        }
    }
}
