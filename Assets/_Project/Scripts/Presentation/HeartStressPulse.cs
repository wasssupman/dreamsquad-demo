using UnityEngine;

namespace Wassup.Presentation
{
    /// <summary>
    /// heart-stress-axis unit 1 rev 2 — **스트레스 = 심박수.**
    ///
    /// rev 1 은 머리 위 «차오르는 바» 였고, rev 2 는 마음 주변 3×3 «잠식» 이었다. 둘 다
    /// 반려됐다(바 = 다른 바들과 문법이 같아 임팩트 0 · 잠식 = 마음 주변 타일 하이라이트가
    /// 쓸모없다). 남은 결론: **마음이니까 심장이 뛴다.**
    ///
    /// 이 어휘의 값어치:
    ///   · 학습이 필요 없다 — 빨라지면 위험하다는 걸 모두가 이미 안다.
    ///   · **지속**이다. 일시적 플래시가 아니라 판 내내 도는 상태라 「지금 얼마나 위험한가」를
    ///     항상 말한다(사용자 지시: 「일시적인게 아니라 스트레스 정도에 따라」).
    ///   · 화면과 마음 프랍이 **같은 박자**로 뛴다 — 두 채널이 하나로 읽힌다.
    ///
    /// 아키텍처 무참조 순수 함수(UnityEngine.Mathf 만 쓴다).
    /// </summary>
    public static class HeartStressPulse
    {
        /// <summary>스트레스(0~1) → 분당 심박. 쉬는 심박에서 시작해 한계까지 올라간다.</summary>
        public static float Bpm(float stress01, float restBpm, float maxBpm)
            => Mathf.Lerp(restBpm, maxBpm, Mathf.Clamp01(stress01));

        /// <summary>시각 + 심박 → 박동 위상(0~1). 되감기지 않게 누적 위상을 호출자가 넘긴다.</summary>
        public static float AdvancePhase(float phase, float deltaSec, float bpm)
        {
            phase += deltaSec * (bpm / 60f);
            return phase - Mathf.Floor(phase);   // 항상 0~1 (음수 delta 도 안전)
        }

        /// <summary>
        /// 박동 파형(0~1). **lub-dub** — 강한 첫 박 + 조금 늦은 약한 둘째 박 + 쉼.
        ///
        /// 사인파를 쓰지 않는 이유: 사인은 «숨쉬기» 로 읽힌다(위아래가 대칭이고 쉼이 없다).
        /// 심장은 «툭-툭 … 쉼» 이고, 그 비대칭이 곧 «심장» 이라는 신호다.
        /// </summary>
        public static float Beat(float phase)
        {
            phase -= Mathf.Floor(phase);
            float first = Thump(phase, 0.00f, 0.11f);
            float second = Thump(phase, 0.17f, 0.13f) * 0.62f;
            return Mathf.Clamp01(Mathf.Max(first, second));
        }

        // 한 박. center 에서 1, width 밖에서 0. 앞이 가파르고 뒤가 늘어진다(타격 → 여운).
        private static float Thump(float phase, float center, float width)
        {
            float d = phase - center;
            if (d < 0f) d *= 2.2f;              // 상승은 급하게
            float u = Mathf.Abs(d) / Mathf.Max(1e-4f, width);
            if (u >= 1f) return 0f;
            float v = 1f - u;
            return v * v;                        // 부드러운 감쇠
        }

        /// <summary>박동을 «밝기 배율»(0~1)로 접는다. depth 0 = 안 뛴다.
        /// 반환 하한이 1−depth 라 바닥에서도 완전히 꺼지지 않는다 — 꺼지면 깜빡임이 된다.</summary>
        public static float BeatScale(float beat, float depth)
            => 1f - Mathf.Clamp01(depth) * (1f - Mathf.Clamp01(beat));

        /// <summary>스트레스 → 화면/프랍 연출의 **기저 세기**(0~1).
        /// 선형이 아니라 후반 가중이다 — 낮은 스트레스에서 화면이 벌써 붉으면 판이 항상
        /// 위급해 보이고, 그러면 진짜 위급한 구간이 안 읽힌다.</summary>
        public static float Intensity(float stress01, float curvePower)
            => Mathf.Pow(Mathf.Clamp01(stress01), Mathf.Max(0.1f, curvePower));
    }
}
