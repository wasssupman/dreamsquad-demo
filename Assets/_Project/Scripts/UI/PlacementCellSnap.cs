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
        // 상한의 근거 = **이웃 도달 창**. 밴드를 벗어나면 손가락 칸으로 반올림하므로,
        // c 에서 이웃 c+1 에 착지하는 구간은 f ∈ [c+0.5+margin, c+1.5) → 폭 = (1 − margin) 타일.
        //   margin 0.3  → 창 0.7타일 (넉넉 — 연속 이동으로 확실히 도달)
        //   margin 0.7↑ → 창이 프레임당 이동량 수준으로 좁아져 이웃을 건너뛰기 시작(실용 한계)
        //   margin ≥ 1  → 창 0 → 이웃 도달 **불가**(항상 2칸 이상 점프) = 하드 고장 → clamp 근거
        // 1.0 직전까지 열어두되(튜닝 재량), 실사용 권장은 0.2~0.3 이다.
        // (초기 주석 "0.5 이상이면 진입 불가"는 오판 — 0.5 의 창은 0.5타일로 멀쩡하다.)
        private const float MaxMargin = 0.95f;

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

        // unit 7 — 끈적 액체 하이라이트용 신호. **Resolve 와 같은 파일·같은 밴드·같은 clamp** 를 쓰는 게 load-bearing:
        // 분모가 드리프트하면 "액체는 안 끊긴 척인데 이미 넘어감"(프리뷰가 거짓말)이 된다.
        //   dir = 당기는 방향(정규화), t = 0(중심) ~ 1(파열 직전)
        // ★ t 는 **체비쇼프**(축별 max)다 — Resolve 의 밴드가 축별 독립(박스)이라 파열은
        //   max(|dx|,|dy|) ≥ 0.5+margin 에서 일어난다. 유클리드로 재면 대각선에서 t 가 1 에 못 미친 채
        //   Resolve 가 먼저 넘어가 버린다(시각과 판정 불일치).
        public static void EvaluateStretch(Vector2Int committed, Vector2 frac, float stickMargin,
                                           out Vector2 dir, out float t)
        {
            float margin = Mathf.Clamp(stickMargin, 0f, MaxMargin);
            Vector2 d = new Vector2(frac.x - committed.x, frac.y - committed.y);
            float len = d.magnitude;
            dir = len > 1e-5f ? d / len : Vector2.zero;
            float half = 0.5f + margin;
            t = Mathf.Clamp01(Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) / half);
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
