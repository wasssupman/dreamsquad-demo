using System;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-A — sim 이 소유하는 수학 함수. `Unity.Mathematics` 무참조.
    ///
    /// **모든 본문은 `Unity.Mathematics` 의 구현을 그대로 옮긴 것이다.** "같은 값을 주는 다른 식"
    /// 이 아니라 **같은 식**이어야 한다 — 부동소수 결과가 sim 의 **이산 판정**(사거리 경계·타겟팅
    /// 동률)에 먹히므로 마지막 비트 차이가 분기를 뒤집고 거기서 발산한다.
    ///
    /// 순진한 재구현이 실제로 갈리는 지점 3개 — 전부 실측해서 옮겼다:
    /// - <see cref="Max"/>/<see cref="Min"/> 은 `Math.Max`/`Math.Min` 이 **아니다**. NaN 처리가
    ///   비대칭이다(`float.IsNaN(y) || x &gt; y ? x : y` — 두 번째 인자만 검사).
    /// - <see cref="Abs"/> 는 비교가 아니라 **부호 비트 마스크**다(`-0f` → `+0f`).
    /// - <see cref="Normalize"/> 는 나눗셈이 아니라 **역제곱근 곱**이다(`rsqrt(dot) * x`).
    ///
    /// 게이트: `SimMathParityTests` 가 임의 입력 + 경계값으로 두 구현을 **비트 단위** 대조한다.
    /// 그 테스트는 두 라이브러리가 공존하는 units 18~20 동안에만 쓸 수 있다.
    /// </summary>
    public static class SimMath
    {
        /// `Unity.Mathematics.math.FLT_MIN_NORMAL` — `NormalizeSafe` 의 임계.
        public const float FltMinNormal = 1.175494351e-38f;

        // ── 스칼라 ────────────────────────────────────────────────────────────
        // ⚠ NaN 비대칭 보존: 두 번째 인자만 NaN 검사한다.
        public static float Max(float x, float y) => float.IsNaN(y) || x > y ? x : y;
        public static float Min(float x, float y) => float.IsNaN(y) || x < y ? x : y;
        public static int Max(int x, int y) => x > y ? x : y;
        public static int Min(int x, int y) => x < y ? x : y;

        /// 부호 비트 마스크 — 비교 기반 abs 와 `-0f` 에서 갈린다.
        public static float Abs(float x)
            => BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(x) & 0x7FFFFFFF);
        public static int Abs(int x) => x < 0 ? -x : x;

        /// `max(lo, min(hi, v))` — **순서가 계약**이다(NaN 전파 방향이 달라진다).
        public static float Clamp(float v, float lo, float hi) => Max(lo, Min(hi, v));
        public static int Clamp(int v, int lo, int hi) => Max(lo, Min(hi, v));
        public static float Saturate(float x) => Clamp(x, 0f, 1f);

        public static float Sqrt(float x) => (float)Math.Sqrt(x);
        public static float Rsqrt(float x) => 1.0f / Sqrt(x);
        public static float Floor(float x) => (float)Math.Floor(x);
        /// ⚠ `Math.Round` 기본값 = **짝수 반올림**. `Mathf.RoundToInt` 와 같은 규칙이다.
        public static float Round(float x) => (float)Math.Round(x);
        /// NaN → 0, `-0f` → 0. 비교 두 번의 뺄셈이라 `Math.Sign` 과 다르다(그쪽은 NaN 에서 던진다).
        public static float Sign(float x) => (x > 0f ? 1f : 0f) - (x < 0f ? 1f : 0f);
        public static float Lerp(float start, float end, float t) => start + t * (end - start);
        public static float Sin(float x) => (float)Math.Sin(x);
        public static float Cos(float x) => (float)Math.Cos(x);

        // ── SimVec3 ───────────────────────────────────────────────────────────
        public static float Dot(SimVec3 a, SimVec3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static float LengthSq(SimVec3 v) => Dot(v, v);
        public static float Length(SimVec3 v) => Sqrt(Dot(v, v));
        public static float DistanceSq(SimVec3 a, SimVec3 b) => LengthSq(b - a);
        public static float Distance(SimVec3 a, SimVec3 b) => Length(b - a);

        /// ⚠ 나눗셈이 아니라 **역제곱근 곱**이다 — 영벡터에서 NaN 을 낸다(그게 원본 동작).
        public static SimVec3 Normalize(SimVec3 v) => Rsqrt(Dot(v, v)) * v;

        /// 영벡터/비정규수에서 `defaultValue`. 임계는 `dot` 자체와 비교한다(제곱근 없음).
        public static SimVec3 NormalizeSafe(SimVec3 v, SimVec3 defaultValue = default)
        {
            float len = Dot(v, v);
            return len > FltMinNormal ? v * Rsqrt(len) : defaultValue;
        }

        // ── SimVec2 ───────────────────────────────────────────────────────────
        public static float Dot(SimVec2 a, SimVec2 b) => a.x * b.x + a.y * b.y;
        public static float LengthSq(SimVec2 v) => Dot(v, v);
        public static float Length(SimVec2 v) => Sqrt(Dot(v, v));
        public static float DistanceSq(SimVec2 a, SimVec2 b) => LengthSq(b - a);
        public static float Distance(SimVec2 a, SimVec2 b) => Length(b - a);
        public static SimVec2 Normalize(SimVec2 v) => Rsqrt(Dot(v, v)) * v;

        public static SimVec2 NormalizeSafe(SimVec2 v, SimVec2 defaultValue = default)
        {
            float len = Dot(v, v);
            return len > FltMinNormal ? v * Rsqrt(len) : defaultValue;
        }
    }
}
