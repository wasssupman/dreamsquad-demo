using Wassup.Data;

namespace Wassup.Core
{
    /// <summary>
    /// wave-pull-revival unit 3 — 「진출 예상선」. 지금 이 페이스면 진출권 안인가.
    ///
    /// <para><b>이 값은 가짜다.</b> 진짜 기준선은 같은 시드를 돈 10인의 누적 처치 곡선에서
    /// 나와야 하는데(PRD §7.1) 경기 중 서버 조회 경로가 아직 없다. 그때까지는 «기본 진행으로
    /// 지금까지 나왔을 적의 점수 × par 비율» 을 par 로 쓴다.</para>
    ///
    /// <para>왜 저작 곡선이 아니라 플랜에서 뽑나: 곡선을 덱마다 손으로 그리면 맵·적 구성이
    /// 바뀔 때마다 같이 고쳐야 하고, 안 고치면 조용히 거짓말이 된다. 플랜에서 뽑으면 덱을
    /// 바꿔도 par 가 따라오고 저작은 비율 하나로 끝난다.</para>
    ///
    /// <para><b>서버가 생기면 이 클래스 안만 바꾼다</b> — 표시하는 쪽(HUD)은 건드리지 않는다.
    /// 인터페이스를 만들지 않는 이유는 구현체가 하나이기 때문이다(제약 8).</para>
    ///
    /// <para><b>절대 새면 안 된다</b>: 이 값은 표시 전용이다. 로그·서버 제출값·결과 화면에
    /// 들어가는 순간 «가짜 경쟁 기록»이 진짜인 척 저장된다.</para>
    /// </summary>
    public static class PaceBaseline
    {
        /// <summary>
        /// 경과 시각의 par 점수. 저작 비율이 0 이하이거나 플랜이 비었으면 false —
        /// 이때 호출자는 <b>줄을 숨긴다</b>. 0 을 par 로 읽으면 «항상 앞섬»이라는 거짓말이 된다.
        /// </summary>
        public static bool TryExpectedScore(
            in GeneratedWavePlan plan, float elapsedSec, float parFraction, out int expected)
        {
            expected = 0;
            if (parFraction <= 0f) return false;
            if (plan.waves == null || plan.waves.Count == 0) return false;

            // 기본 진행(당김 없음)에서 웨이브 i 는 triggerTimeSec 에 나온다 — 생성기가 넣는
            // 명목 그리드다. 이벤트 구동 런타임은 이 값을 읽지 않지만, «가만히 뒀다면» 이라는
            // par 의 정의에는 정확히 이 그리드가 맞다.
            float cumulativeBefore = 0f;
            float cumulativeAfter = 0f;
            float windowStart = 0f;
            float windowEnd = 0f;
            bool found = false;

            for (int i = 0; i < plan.waves.Count; i++)
            {
                var wave = plan.waves[i];
                float waveScore = WaveKillScore(wave);
                float start = wave.triggerTimeSec;
                // 마지막 웨이브의 창은 플랜의 명목 간격을 쓴다(상수를 박으면 간격이 다른
                // 덱에서 마지막 구간만 이상하게 가팔라진다).
                float end = i + 1 < plan.waves.Count
                    ? plan.waves[i + 1].triggerTimeSec
                    : start + (plan.waveIntervalSec > 0f ? plan.waveIntervalSec : 1f);

                if (elapsedSec < end)
                {
                    windowStart = start;
                    windowEnd = end;
                    cumulativeBefore = cumulativeAfter;
                    cumulativeAfter += waveScore;
                    found = true;
                    // 창을 찾으면 즉시 끝낸다. 라이브 덱은 100웨이브이고 이 함수가 매 프레임
                    // 돌기 때문에, 남은 90여 웨이브를 계속 순회하면 초당 수천 번의 헛일이 된다.
                    break;
                }
                cumulativeAfter += waveScore;
            }

            float raw;
            if (!found)
            {
                // 마지막 웨이브 시각을 지났다 — 전량이 par 다.
                raw = cumulativeAfter;
            }
            else
            {
                // 웨이브 창 안에서는 계단이 아니라 경사로 오른다. 계단이면 20초마다 예상선이
                // 툭 뛰어 «갑자기 30점 부족» 이 되고, 그 점프는 플레이어가 한 일과 무관하다.
                float span = windowEnd - windowStart;
                float t = span > 0.0001f
                    ? UnityEngine.Mathf.Clamp01((elapsedSec - windowStart) / span)
                    : 1f;
                raw = UnityEngine.Mathf.Lerp(cumulativeBefore, cumulativeAfter, t);
            }

            expected = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.RoundToInt(raw * parFraction));
            return true;
        }

        /// <summary>한 웨이브를 전부 잡았을 때 벌리는 점수(가중치 포함).</summary>
        public static int WaveKillScore(in GeneratedWave wave)
        {
            if (wave.groups == null) return 0;
            int total = 0;
            for (int g = 0; g < wave.groups.Count; g++)
            {
                var group = wave.groups[g];
                if (group.unit == null || group.count <= 0) continue;
                // 분열체처럼 killScore 0 인 적은 par 에도 0 이다(보상 불변식과 같은 축).
                total += group.unit.killScore * group.count;
            }
            return total;
        }
    }
}
