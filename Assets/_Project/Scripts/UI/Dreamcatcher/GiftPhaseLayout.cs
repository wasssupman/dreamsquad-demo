using UnityEngine;

namespace Wassup.UI
{
    // gift-phase-presentation unit 0 — 선물 페이즈 안무가 소비하는 순수 레이아웃 수학.
    // plain 값 입력 → plain 값 출력(제약 10). RectTransform/Time 등 아키텍처 타입 금지.
    // 좌표계: cardsRoot 로컬(anchoredPosition), +y 위. 회전은 UI z-rot(도, CCW 양수).
    public static class GiftPhaseLayout
    {
        // k번째 카드의 그리드 로컬 좌표(행 우선, 양축 중앙 정렬). 부분 마지막 행은
        // 재중앙화하지 않고 열 위치를 유지한다(현 소비처는 10장/5열 = 꽉 찬 2행).
        public static Vector2 GridSlot(int k, int count, int cols, Vector2 cell)
        {
            int rows = (count + cols - 1) / cols;
            int c = k % cols;
            int r = k / cols;
            float x = (c - (cols - 1) * 0.5f) * cell.x;
            float y = ((rows - 1) * 0.5f - r) * cell.y; // 첫 행이 위
            return new Vector2(x, y);
        }

        // 부채꼴 f번째(좌→우 0..n-1) 위치+회전. 원호 중심은 (0, baseY - radius),
        // 중앙 카드가 baseY 정점. 카드는 바깥으로 기움(왼쪽 = +z CCW).
        public static (Vector2 pos, float rotDeg) FanSlot(
            int f, int n, float radius, float arcDeg, float baseY)
        {
            float t = n <= 1 ? 0.5f : (float)f / (n - 1);
            float angDeg = (t - 0.5f) * arcDeg;
            float rad = angDeg * Mathf.Deg2Rad;
            var pos = new Vector2(radius * Mathf.Sin(rad), baseY + radius * (Mathf.Cos(rad) - 1f));
            return (pos, -angDeg);
        }

        // 리플 셔플 연출용 지퍼 인터리브 — 좌뭉치(0..h-1)/우뭉치(h..n-1)가 교차로
        // 재적층되는 시각적 순서. 결과 덱 순서와 무관한 가짜 연출이므로 계약은
        // "n개 전부 정확히 1회 등장" + 가능한 한 좌/우 교차.
        public static int[] RiffleOrder(int n)
        {
            var order = new int[n];
            int h = (n + 1) / 2;
            int li = 0, ri = h, w = 0;
            while (li < h || ri < n)
            {
                if (li < h) order[w++] = li++;
                if (ri < n) order[w++] = ri++;
            }
            return order;
        }

        // i번째 흡수 시작 지연(가속 케이던스). 간격은 first 에서 decay 배로 줄되
        // min 아래로는 내려가지 않는다. delay(0) = 0.
        public static float AbsorbDelay(int i, float first, float min, float decay)
        {
            float sum = 0f, gap = first;
            for (int j = 0; j < i; j++)
            {
                sum += Mathf.Max(min, gap);
                gap *= decay;
            }
            return sum;
        }

        // 스택 적층 시 index 기반 결정론 미세 지터(seeded RNG 금지 — 구조적 결정론).
        // 황금비 저불일치 수열로 [-max, max] 분산.
        public static (float rotDeg, Vector2 offset) StackJitter(int k, float maxRotDeg, float maxOffset)
        {
            float rot = Signed(k, 0.6180339887f) * maxRotDeg;
            var offset = new Vector2(
                Signed(k, 0.7548776662f) * maxOffset,
                Signed(k, 0.5698402909f) * maxOffset);
            return (rot, offset);
        }

        private static float Signed(int k, float mult)
        {
            float v = (k + 1) * mult;
            return (v - Mathf.Floor(v)) * 2f - 1f;
        }
    }
}
