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

        static void SameBits(float2 expected, SimVec2 actual, string what)
        {
            SameBits(expected.x, actual.x, what + ".x");
            SameBits(expected.y, actual.y, what + ".y");
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

        // ── 18-I/N1 (3렌즈 리뷰 F3·D2) — 문서가 게이트라 주장하는데 안 덮던 표면 ──────

        /// <summary>
        /// **`SimVec2` 는 이 파일에 한 번도 없었다.** `SimVec3` 만 대조하고 있었는데,
        /// `MovementSystem` 의 `NormalizeSafe(SimVec2)` 가 **모든 이동 유닛의 스텝 방향**이다
        /// (#17 "위치 갱신 단일 권한"). 1 ULP 가 갈리면 다음 틱 `WorldToCell` 이 셀 경계에서
        /// 다른 셀을 반환하고, 거기서 사거리·타겟팅·존 진입이 전부 갈린다.
        ///
        /// 외 소비처: `GridMath` · `MovementCellTrim` · `ProjectileMoveSystem`(직선 전진) ·
        /// `SweepHitMath`(경로 명중 판정).
        /// </summary>
        [Test]
        public void SimVec2_연산이_비트까지_같다()
        {
            var c = Corpus();
            for (int i = 0; i + 3 < c.Length; i += 2)
            {
                var u = new float2(c[i], c[i + 1]);
                var v = new float2(c[i + 2], c[i + 3]);
                var su = new SimVec2(c[i], c[i + 1]);
                var sv = new SimVec2(c[i + 2], c[i + 3]);

                SameBits(math.dot(u, v), SimMath.Dot(su, sv), "Dot2");
                SameBits(math.lengthsq(u), SimMath.LengthSq(su), "LengthSq2");
                SameBits(math.length(u), SimMath.Length(su), "Length2");
                SameBits(math.distancesq(u, v), SimMath.DistanceSq(su, sv), "DistanceSq2");
                SameBits(math.distance(u, v), SimMath.Distance(su, sv), "Distance2");
                SameBits(math.normalize(u), SimMath.Normalize(su), "Normalize2");
                SameBits(math.normalizesafe(u), SimMath.NormalizeSafe(su), "NormalizeSafe2");
                SameBits(u + v, su + sv, "add2");
                SameBits(u - v, su - sv, "sub2");
                SameBits(u * 0.37f, su * 0.37f, "mul2");
            }

            // SimVec3 쪽과 같은 비대칭이 2축에도 성립해야 한다.
            SameBits(math.normalize(float2.zero), SimMath.Normalize(SimVec2.Zero), "Normalize2(0)");
            SameBits(math.normalizesafe(float2.zero), SimMath.NormalizeSafe(SimVec2.Zero), "NormalizeSafe2(0)");
        }

        /// <summary>
        /// ⚠ **이 테스트는 지금 자명하게 통과한다** — EditMode 에서 `math.sin` 은 관리 경로라
        /// 결국 `Math.Sin` 이고 `SimMath.Sin` 도 그렇다(libm 대 libm).
        ///
        /// 그런데도 넣는 이유는 **드리프트 감지**다. 초월함수는 IEEE-754 가 정확 반올림을 요구하지
        /// 않아 "sim 이 자기 `Sin` 을 소유해야 하나" 가 실제로 논의됐고(N1), **만들지 않기로**
        /// 결정했다. 나중에 누가 그 결정을 뒤집어 다항식을 넣으면 **그 순간 여기서 잡힌다** —
        /// 구 sim(Burst-sin)과의 A/B parity 가 조용히 깨지는 것보다 훨씬 낫다.
        ///
        /// `PI`·`ToRadians` 상수도 함께 본다. 상수 한 자리가 다르면 각도 전체가 밀린다.
        /// </summary>
        [Test]
        public void 초월함수와_상수가_비트까지_같다()
        {
            Assert.AreEqual(Bits(math.PI), Bits(SimMath.PI), "PI 상수");
            SameBits(math.radians(1f), SimMath.Radians(1f), "ToRadians 상수");

            // 각도 코퍼스 — 경계(0·±π/2·±π·±2π)와 큰 인수(축소 오차가 드러나는 자리).
            var angles = new float[]
            {
                0f, -0f, math.PI * 0.5f, -math.PI * 0.5f, math.PI, -math.PI,
                math.PI * 2f, 1e-8f, 1e8f, 1000f, -1000f, 0.5f, -0.5f,
                float.NaN, float.PositiveInfinity, float.NegativeInfinity,
            };
            foreach (float x in angles)
            {
                SameBits(math.sin(x), SimMath.Sin(x), $"Sin({x})");
                SameBits(math.cos(x), SimMath.Cos(x), $"Cos({x})");

                math.sincos(x, out float us, out float uc);
                SimMath.SinCos(x, out float ss, out float sc);
                SameBits(us, ss, $"SinCos({x}).sin");
                SameBits(uc, sc, $"SinCos({x}).cos");
            }

            // 저작 각도 범위(패턴 min/max) 를 도 단위로 훑는다 — 실사용 입력이 여기다.
            for (int deg = -360; deg <= 360; deg++)
            {
                float r = math.radians(deg);
                SameBits(r, SimMath.Radians(deg), $"Radians({deg})");
                SameBits(math.sin(r), SimMath.Sin(SimMath.Radians(deg)), $"Sin({deg}deg)");
                SameBits(math.cos(r), SimMath.Cos(SimMath.Radians(deg)), $"Cos({deg}deg)");
            }
        }

        /// <summary>
        /// `CreateFromIndex` 는 모든 무작위 발사 패턴의 시드다(`PatternShotRandomizer`).
        /// ⚠ 인덱스를 시드로 그냥 쓰지 않고 `WangHash(index + 62)` 로 흩뿌린다 — `+62` 와 해시
        /// 상수 넷 중 하나만 달라도 그 뒤 **모든** draw 가 갈린다.
        /// </summary>
        [Test]
        public void CreateFromIndex_가_비트까지_같은_수열을_낸다()
        {
            foreach (uint index in new uint[] { 0u, 1u, 2u, 7u, 62u, 63u, 1000u, uint.MaxValue - 1u })
            {
                var u = Unity.Mathematics.Random.CreateFromIndex(index);
                var s = SimRandom.CreateFromIndex(index);
                Assert.AreEqual(u.state, s.state, $"index {index}: 생성 직후 상태");

                for (int i = 0; i < 100; i++)
                {
                    SameBits(u.NextFloat(), s.NextFloat(), $"index {index} draw {i}");
                    Assert.AreEqual(u.state, s.state, $"index {index} draw {i}: 상태 동기");
                }
            }

            // 퇴화 인덱스는 **양쪽 다 거절**한다(해시가 0 → 난수열 사망).
            Assert.Throws<ArgumentException>(() => SimRandom.CreateFromIndex(uint.MaxValue));
        }

        /// <summary>
        /// 3렌즈 리뷰 발견 — `ModifierAuthoring` 과 `SimModifierAuthoring` 이 **바이트 동일 중복**이다
        /// (같은 네임스페이스·같은 본문·둘 다 "구 `ModifierAuthoring` 이식"). 호출처가 갈려 있어서
        /// (`FieldBuilderSystems` 는 `Sim` 접두사 쪽, `DamageApplicationSystem` 은 무접두사 쪽)
        /// **정책이 바뀌면 하나만 고쳐지고 나머지 절반의 호출처가 옛 규칙을 유지한다.**
        ///
        /// 통합은 `Sim` 접두사 정리(unit 20)와 같은 축이라 그때 한다 — 그 전까지 **갈리는 것만** 막는다.
        /// </summary>
        [Test]
        public void 중복된_ModifierAuthoring_두_벌이_같은_정책을_낸다()
        {
            foreach (float mul in new[] { 0f, 0.5f, 0.999f, 1f, 1.0001f, 1.3f, 5f, -1f })
            {
                Wassup.Sim.Effects.ModifierAuthoring.FromMultiplier(mul, out var opA, out var magA);
                Wassup.Sim.Effects.SimModifierAuthoring.FromMultiplier(mul, out var opB, out var magB);
                Assert.AreEqual(opA, opB, $"×{mul}: op 가 갈렸다");
                SameBits(magA, magB, $"×{mul}: magnitude");
            }
        }

        /// <summary>
        /// 18-I/2 arm E — 발사 패턴의 셔플 시드가 `math.hash(int2)` 다. 상수 하나만 달라도 랜덤
        /// 패턴의 각도·간격이 통째로 갈리고 **그것이 골든에 실린다**. 음수·오버플로 경계를 함께 본다
        /// (`asuint` 캐스트와 wrap 곱셈이 원본 동작이라 여기서 어긋나기 쉽다).
        /// </summary>
        [Test]
        public void Hash_int2_가_비트까지_같다()
        {
            var rng = new System.Random(20260806);
            for (int i = 0; i < Samples; i++)
            {
                int a = rng.Next(int.MinValue, int.MaxValue);
                int b = rng.Next(int.MinValue, int.MaxValue);
                Assert.AreEqual(math.hash(new int2(a, b)), SimMath.Hash(new SimInt2(a, b)),
                    $"hash({a}, {b})");
            }

            foreach (var v in new[]
            {
                new int2(0, 0), new int2(1, 0), new int2(0, 1), new int2(-1, -1),
                new int2(int.MinValue, int.MaxValue), new int2(int.MaxValue, int.MinValue),
            })
            {
                Assert.AreEqual(math.hash(v), SimMath.Hash(new SimInt2(v.x, v.y)), $"hash{v}");
            }
        }
    }
}
