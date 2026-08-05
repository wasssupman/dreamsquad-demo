using System;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Sim;

// battle-sim-extraction unit 18-A — **sim 자체 수학의 비트 동일성 게이트.**
//
// sim 은 `Unity.Mathematics` 를 참조하지 않지만(엔진-프리 목적지), **테스트 어셈블리는 양쪽을 다
// 참조할 수 있다.** 그래서 여기서만 두 구현을 직접 대조할 수 있고, 그 기회는 구 sim 이 살아 있는
// units 18~20 동안뿐이다 — 스왑 이후엔 비교 대상이 사라진다.
//
// **왜 epsilon 이 아니라 비트인가**: 이 값들이 sim 의 *이산* 판정(사거리 경계·타겟팅 동률)에
// 먹힌다. 마지막 비트 차이가 분기를 뒤집고 거기서부터 두 시뮬이 발산한다. epsilon 비교는
// 그 발산을 통과시킨다.
namespace Wassup.Tests.EditMode
{
    public class SimMathParityTests
    {
        const int Samples = 2000;

        static int Bits(float f) => BitConverter.SingleToInt32Bits(f);

        static void SameBits(float expected, float actual, string what)
        {
            // NaN 은 비교로 같지 않으므로 비트로 본다(부호·payload 까지 일치해야 한다).
            Assert.AreEqual(Bits(expected), Bits(actual),
                $"{what}: unity={expected} (0x{Bits(expected):X8}) sim={actual} (0x{Bits(actual):X8})");
        }

        static void SameBits(float3 expected, SimVec3 actual, string what)
        {
            SameBits(expected.x, actual.x, what + ".x");
            SameBits(expected.y, actual.y, what + ".y");
            SameBits(expected.z, actual.z, what + ".z");
        }

        /// 경계값 — 순진한 재구현이 갈리는 자리를 전부 포함한다.
        static readonly float[] Edge =
        {
            0f, -0f, 1f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity,
            float.Epsilon, -float.Epsilon, SimMath.FltMinNormal, -SimMath.FltMinNormal,
            float.MaxValue, float.MinValue, 1e-30f, -1e-30f, 0.5f, -0.5f, 3.14159265f,
        };

        static float[] Corpus()
        {
            var rng = new System.Random(20260805);   // 고정 시드 — 실패가 재현 가능해야 한다
            var a = new float[Edge.Length + Samples];
            Array.Copy(Edge, a, Edge.Length);
            for (int i = Edge.Length; i < a.Length; i++)
            {
                // 넓은 지수 범위를 훑는다 — 정규수만 보면 비정규수 경계를 놓친다.
                double mag = Math.Pow(10, rng.NextDouble() * 40 - 20);
                a[i] = (float)(mag * (rng.NextDouble() * 2 - 1));
            }
            return a;
        }

        [Test]
        public void 스칼라_함수가_비트까지_같다()
        {
            var c = Corpus();
            foreach (float x in c)
            {
                SameBits(math.abs(x), SimMath.Abs(x), $"Abs({x})");
                SameBits(math.sqrt(x), SimMath.Sqrt(x), $"Sqrt({x})");
                SameBits(math.rsqrt(x), SimMath.Rsqrt(x), $"Rsqrt({x})");
                SameBits(math.saturate(x), SimMath.Saturate(x), $"Saturate({x})");
                SameBits(math.sign(x), SimMath.Sign(x), $"Sign({x})");
                SameBits(math.floor(x), SimMath.Floor(x), $"Floor({x})");
                SameBits(math.round(x), SimMath.Round(x), $"Round({x})");
            }
        }

        [Test]
        public void 이항_스칼라가_NaN_비대칭까지_같다()
        {
            // `min`/`max` 는 **두 번째 인자만** NaN 검사한다 — `Math.Max` 로 바꾸면 여기서 깨진다.
            var c = Corpus();
            for (int i = 0; i < c.Length; i++)
            {
                float x = c[i], y = c[(i * 7 + 3) % c.Length];
                SameBits(math.max(x, y), SimMath.Max(x, y), $"Max({x},{y})");
                SameBits(math.min(x, y), SimMath.Min(x, y), $"Min({x},{y})");
                SameBits(math.lerp(x, y, 0.37f), SimMath.Lerp(x, y, 0.37f), $"Lerp({x},{y})");
                SameBits(math.clamp(x, -1f, 1f), SimMath.Clamp(x, -1f, 1f), $"Clamp({x})");
            }
        }

        [Test]
        public void 벡터_연산이_비트까지_같다()
        {
            var c = Corpus();
            for (int i = 0; i + 5 < c.Length; i += 3)
            {
                var u = new float3(c[i], c[i + 1], c[i + 2]);
                var v = new float3(c[i + 3], c[i + 4], c[i + 5]);
                var su = new SimVec3(c[i], c[i + 1], c[i + 2]);
                var sv = new SimVec3(c[i + 3], c[i + 4], c[i + 5]);

                SameBits(math.dot(u, v), SimMath.Dot(su, sv), "Dot");
                SameBits(math.lengthsq(u), SimMath.LengthSq(su), "LengthSq");
                SameBits(math.length(u), SimMath.Length(su), "Length");
                SameBits(math.distancesq(u, v), SimMath.DistanceSq(su, sv), "DistanceSq");
                SameBits(math.distance(u, v), SimMath.Distance(su, sv), "Distance");
                // `normalize` 는 나눗셈이 아니라 rsqrt 곱 — 나눗셈으로 쓰면 여기서 갈린다.
                SameBits(math.normalize(u), SimMath.Normalize(su), "Normalize");
                SameBits(math.normalizesafe(u), SimMath.NormalizeSafe(su), "NormalizeSafe");
                SameBits(u + v, su + sv, "add");
                SameBits(u - v, su - sv, "sub");
                SameBits(u * 0.37f, su * 0.37f, "mul");
            }
        }

        [Test]
        public void 영벡터에서_normalize_와_normalizesafe_가_갈리는_것까지_같다()
        {
            // 이 비대칭이 계약이다 — 둘 다 실제로 쓰이고(각 5회) 재구현이 흔히 통일해 버린다.
            SameBits(math.normalize(float3.zero), SimMath.Normalize(SimVec3.Zero), "Normalize(0)");
            SameBits(math.normalizesafe(float3.zero), SimMath.NormalizeSafe(SimVec3.Zero), "NormalizeSafe(0)");
            Assert.IsTrue(float.IsNaN(SimMath.Normalize(SimVec3.Zero).x), "Normalize(0) 은 NaN 이어야 한다");
            Assert.AreEqual(0f, SimMath.NormalizeSafe(SimVec3.Zero).x, "NormalizeSafe(0) 은 0 이어야 한다");
        }

        [Test]
        public void 난수가_같은_시드에서_비트까지_같은_수열을_낸다()
        {
            // 시드 파생 스트림이 골든 상태 해시에 실린다(`meteorRng`). 한 draw 라도 어긋나면
            // 그 뒤 모든 확률 판정이 갈린다.
            foreach (uint seed in new uint[] { 1u, 2u, 42u, 0x9E3779B9u, uint.MaxValue, 0x2545F491u })
            {
                var u = new Unity.Mathematics.Random(seed);
                var s = new SimRandom(seed);
                Assert.AreEqual(u.state, s.state, $"seed {seed}: 생성자 직후 상태(NextState 1회 버림)");

                for (int i = 0; i < 500; i++)
                {
                    Assert.AreEqual(u.NextUInt(), s.NextUInt(), $"seed {seed} draw {i}: NextUInt");
                    SameBits(u.NextFloat(), s.NextFloat(), $"seed {seed} draw {i}: NextFloat");
                    Assert.AreEqual(u.NextInt(0, 1000), s.NextInt(0, 1000), $"seed {seed} draw {i}: NextInt");
                    Assert.AreEqual(u.NextBool(), s.NextBool(), $"seed {seed} draw {i}: NextBool");
                    Assert.AreEqual(u.state, s.state, $"seed {seed} draw {i}: 상태 동기");
                }
            }
        }
    }
}
