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
        /// <summary>
        /// ⚠ **이 둘만 다른 함수와 성질이 다르다.** IEEE-754 는 `+ - * / sqrt` 에만 정확 반올림을
        /// 요구하고 **초월함수에는 아무것도 요구하지 않는다** — 플랫폼 libm 이 각자 다항식을 쓰므로
        /// 런타임/아키텍처가 다르면 마지막 비트가 갈릴 수 있다.
        ///
        /// **그래도 자체 구현하지 않는다**(N1 결정, 2026-08-06). 구 sim 은 이 수학을 Burst 로 돌리고
        /// Burst 는 초월함수를 자기 구현으로 인트린식화한다 — 여기에 다항식을 넣으면 구 sim 과
        /// **확실히** 갈리고, unit 20 A/B parity 의 목적("신 sim == 구 sim")이 사라진다.
        /// `Math.Sin` 유지는 최소한 우연히 같을 가능성이 있다.
        ///
        /// ⚠ `SimMathParityTests` 가 이 둘을 덮지만 그 대조는 **관리 경로 대 관리 경로**다
        /// (EditMode 의 `math.sin` 도 결국 `Math.Sin`). 즉 **드리프트는 잡지만 Burst 와의 차이는
        /// 못 본다** — 그건 unit 20 의 교차 골든(Editor·IL2CPP)만 볼 수 있고, 그래서 골든 코퍼스가
        /// 이 경로(탄도 아치·`DirectionalLinear` 패턴)를 실제로 밟는지 확인이 선행 조건이다.
        /// 호출처는 둘뿐이다 — `BallisticArc.ArcHeight` · `PatternDirection.Resolve`.
        /// </summary>
        public static float Sin(float x) => (float)Math.Sin(x);
        /// <inheritdoc cref="Sin"/>
        public static float Cos(float x) => (float)Math.Cos(x);

        /// <summary>
        /// ⚠ **`(float)Math.PI` 로 계산하지 않는다.** 구 `Unity.Mathematics.math.PI` 는 double
        /// 리터럴을 float 로 굳힌 `const` 라 여기서도 같은 리터럴을 같은 방식으로 굳힌다 —
        /// 런타임 변환을 끼우면 같은 값이 나오리라는 보장이 표준에 없다.
        /// 18-H/1(포물선 아치)이 처음 요구했다.
        /// </summary>
        public const float PI = 3.14159265358979323846f;

        /// 구 `math.TODEGREES`/`math.radians` 와 같은 상수·같은 형태(곱셈 한 번).
        public const float ToRadians = 0.0174532925199432957692f;
        public static float Radians(float degrees) => degrees * ToRadians;

        /// <summary>
        /// 구 `math.sincos` 대응. **두 값을 한 번에** 내는 형태를 유지한다 —
        /// 호출부가 `Sin`/`Cos` 를 따로 부르는 것과 결과가 같아야 하고, 실제로 같다.
        /// </summary>
        public static void SinCos(float x, out float s, out float c)
        {
            s = Sin(x);
            c = Cos(x);
        }

        // ── SimVec3 ───────────────────────────────────────────────────────────
        public static float Dot(SimVec3 a, SimVec3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static float LengthSq(SimVec3 v) => Dot(v, v);
        public static float Length(SimVec3 v) => Sqrt(Dot(v, v));
        public static float DistanceSq(SimVec3 a, SimVec3 b) => LengthSq(b - a);
        public static float Distance(SimVec3 a, SimVec3 b) => Length(b - a);

        /// <summary>
        /// 성분별 보간. 구 `math.lerp(float3, float3, float)` 과 같은 형태(`a + t*(b-a)`)라
        /// 스칼라 <see cref="Lerp(float,float,float)"/> 와도 일관된다 — `(1-t)*a + t*b` 로 바꾸면
        /// 부동소수 결과가 달라진다. 18-H/1 이 처음 요구했다.
        /// </summary>
        public static SimVec3 Lerp(SimVec3 start, SimVec3 end, float t)
            => new SimVec3(Lerp(start.x, end.x, t), Lerp(start.y, end.y, t), Lerp(start.z, end.z, t));

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

        // ── 해시 ──────────────────────────────────────────────────────────────
        /// <summary>
        /// `Unity.Mathematics.math.hash(int2)` 를 그대로 옮겼다(`int2.gen.cs:947`):
        /// `csum(asuint(v) * uint2(0x83B58237, 0x833E3E29)) + 0xA9D919BF`.
        ///
        /// ⚠ **"같은 값을 주는 다른 해시" 로 대체할 수 없다.** 18-I/2 의 발사 패턴이 이 값을
        /// `SimRandom.CreateFromIndex` 의 시드로 쓰므로, 상수 하나만 달라도 랜덤 패턴의 각도·간격이
        /// 통째로 갈리고 그것이 골든에 실린다.
        ///
        /// ⚠ 상수는 **타입마다 다르다**(`int2`/`int3`/`float2`… 각자 고유). 기억으로 적으면 안 되고
        /// 패키지 소스에서 옮겨야 한다 — 초판이 다른 타입의 상수를 적었고 `SimMathParityTests` 가
        /// 그것을 잡았다.
        ///
        /// unchecked 곱셈/덧셈이 원본 동작이다(오버플로 wrap).
        /// </summary>
        public static uint Hash(SimInt2 v)
        {
            unchecked
            {
                uint x = (uint)v.x * 0x83B58237u;
                uint y = (uint)v.y * 0x833E3E29u;
                return x + y + 0xA9D919BFu;
            }
        }
    }
}
