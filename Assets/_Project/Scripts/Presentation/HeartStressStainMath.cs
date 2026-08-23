using UnityEngine;

namespace Wassup.Presentation
{
    /// <summary>
    /// heart-stress-axis unit 1 rev — 마음 스트레스가 보드에 **번지는** 규칙. 순수 함수.
    ///
    /// 왜 바가 아니라 잠식인가: 마음은 판을 끝내는 축인데 머리 위 가로 바로 그리면
    /// 본능·적 마음·유닛과 **같은 문법**이라 「색만 다른 4번째 바」로 읽힌다. 임팩트는
    /// 면적에서 오므로 보드를 먹게 한다 — 바는 화면의 15px 지만 잠식은 보드의 9칸이다.
    ///
    /// 링 구조: 0 = 마음 셀 · 1 = 직교 인접 4칸 · 2 = 대각 4칸.
    /// 구간이 **겹치도록** 잡아 «한 칸씩 툭툭 켜지는» 계단이 아니라 번지는 그림이 되게 한다.
    /// </summary>
    public static class HeartStressStainMath
    {
        public const int RingCount = 3;

        // 링별 (시작, 완성) 스트레스 비율. 겹침이 번짐을 만든다.
        private static readonly Vector2[] Bands =
        {
            new Vector2(0.00f, 0.30f),   // 중심 — 첫 피격부터 바로 보인다
            new Vector2(0.25f, 0.65f),   // 직교 4
            new Vector2(0.55f, 1.00f),   // 대각 4
        };

        /// <summary>스트레스(0~1) → 이 링의 채움(0~1).</summary>
        public static float RingFill(float stress01, int ring)
        {
            if (ring < 0 || ring >= RingCount) return 0f;
            var band = Bands[ring];
            if (stress01 <= band.x) return 0f;
            if (stress01 >= band.y) return 1f;
            return (stress01 - band.x) / (band.y - band.x);
        }

        /// <summary>셀 오프셋 → 링 인덱스. (0,0)=0 · 직교=1 · 대각=2 · 그 밖=-1.</summary>
        public static int RingOf(int dx, int dy)
        {
            int ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
            if (ax > 1 || ay > 1) return -1;
            if (ax == 0 && ay == 0) return 0;
            return (ax == 1 && ay == 1) ? 2 : 1;
        }

        /// <summary>맥동 배율(0~1 곱). 스트레스가 높을수록 빠르고 깊게 뛴다.
        /// 반환은 «밝기 배율» 이라 1 = 최대, 1−depth = 최소.
        /// 시간은 호출자가 넘긴다(unscaled 를 쓰는 것이 이 프로젝트 관용구 — 슬로우모 무관).</summary>
        public static float Pulse(float stress01, float time, float slowSpeed, float fastSpeed, float depth)
        {
            float w = Mathf.Lerp(slowSpeed, fastSpeed, Mathf.Clamp01(stress01));
            float s = 0.5f + 0.5f * Mathf.Sin(time * w);   // 0~1
            return 1f - Mathf.Clamp01(depth) * (1f - s);
        }
    }
}
