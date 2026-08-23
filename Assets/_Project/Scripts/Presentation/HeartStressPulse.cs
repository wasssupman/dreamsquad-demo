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
        // ── 단계 (heart-stress-axis unit 8) ────────────────────────────────────
        //
        // **연속값을 그대로 연출에 흘리면 아무것도 안 읽힌다.** 두 판독성 리뷰가 독립적으로
        // 같은 진단을 냈다: 지속되는 색·알파는 몇 초 안에 **순응**돼 의식에서 사라지고,
        // 인간은 「수준」이 아니라 「변화」를 읽는다. 그래서 단계로 꺾어 **전이를 사건으로**
        // 만든다 — 「아까보다 한 단 나빠졌다」는 셈은 학습이 필요 없다.
        //
        // 이 단계가 **모든 채널의 공통 클록**이다(심박 BPM · 림 두께 · 숫자 노출 · 균열).
        // 채널마다 자기 임계를 두면 「뭐가 먼저 바뀌었지」가 되고 사건이 흐려진다.
        public const int StageCount = 4;   // 0 평온 · 1 불안 · 2 위기 · 3 임계

        // 진입/이탈 임계가 **비대칭**이다(히스테리시스). 이 게임은 처치로 스트레스가
        // **내려가는** 저울이라 경계에서 왕복이 잦은데, 대칭이면 단계가 깜빡여 «늑대소년» 이 된다.
        private static readonly float[] Enter = { 0f, 0.25f, 0.55f, 0.82f };
        private static readonly float[] Exit  = { 0f, 0.18f, 0.46f, 0.74f };

        /// <summary>현재 단계 + 스트레스 → 다음 단계. 히스테리시스라 **직전 단계가 인자**다.</summary>
        public static int StageOf(float stress01, int currentStage)
        {
            stress01 = Mathf.Clamp01(stress01);
            int stage = Mathf.Clamp(currentStage, 0, StageCount - 1);
            // 올라갈 때는 진입 임계, 내려갈 때는 이탈 임계를 본다.
            while (stage < StageCount - 1 && stress01 >= Enter[stage + 1]) stage++;
            while (stage > 0 && stress01 < Exit[stage]) stage--;
            return stage;
        }

        /// <summary>스트레스(0~1) → 분당 심박.
        ///
        /// ⚠ **연속 램프가 아니라 단계 계단이다.** 서서히 빨라지면 「지금 빠른가」를 판단할
        /// 비교 대상이 없어 순응된다. 단계 경계에서 BPM 이 점프해야 「방금 빨라졌다」가
        /// 사건으로 잡히고, 그 사건이 곧 임계 통과의 통지다.</summary>
        public static float Bpm(int stage, float restBpm, float maxBpm)
        {
            stage = Mathf.Clamp(stage, 0, StageCount - 1);
            // 균등 분할이 아니라 **후반 가중** — 위기·임계의 간격이 벌어져야 그 두 단계가
            // 서로 구분된다(52 / 84 / 122 / 168 꼴).
            float t = stage / (float)(StageCount - 1);
            return Mathf.Lerp(restBpm, maxBpm, t * t * 0.45f + t * 0.55f);
        }

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
