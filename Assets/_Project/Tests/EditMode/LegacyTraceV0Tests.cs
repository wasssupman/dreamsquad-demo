using System.IO;
using NUnit.Framework;
using Wassup.Core.Trace;

namespace Wassup.Tests.EditMode
{
    // battle-sim-extraction M0 unit 4 — 골든 포맷의 계약.
    //
    // 골든이 하는 일은 「구 sim 이 무엇을 했는지」를 **다시 만들 수 없는 형태로** 박제하는
    // 것이다. 그래서 지켜야 할 성질도 그 형태에 관한 것뿐이다: 왕복이 무손실이고, 비교
    // 해상도가 저장 해상도와 같고, 깨진 파일을 조용히 삼키지 않는다.
    public class LegacyTraceV0Tests
    {
        private const string GoldenDir = "Assets/_Project/Tests/Golden";

        private static LegacyTraceV0 Sample()
        {
            var t = new LegacyTraceV0
            {
                scenario = "unit-test", configHash = "0123456789abcdef",
                matchSeed = 42, stepDt = 1f / 60f, tickCount = 3,
                finalKills = 7, finalScore = 7, finalLeaks = 1, finalStateHash = 0xDEADBEEFCAFEF00DUL,
            };
            t.events.Add(new TraceEvent { tick = 0, channel = TraceChannel.UnitAttack, a = 3, b = 9, i = 0, f = 0.75f });
            t.events.Add(new TraceEvent { tick = 1, channel = TraceChannel.DamageNumber, a = 9, b = -1, i = 0, f = 12.5f });
            t.events.Add(new TraceEvent { tick = 2, channel = TraceChannel.EnemyKilled, a = 9, b = 3, i = 5, f = 0f });
            return t;
        }

        [Test]
        public void RoundTrip_IsByteIdentical()
        {
            string once = Sample().Serialize();
            string twice = LegacyTraceV0.Deserialize(once).Serialize();
            Assert.AreEqual(once, twice, "쓰기→읽기→다시 쓰기가 바이트로 같아야 골든이 될 자격이 있다");
        }

        [Test]
        public void RoundTrip_PreservesEveryField()
        {
            var a = Sample();
            var b = LegacyTraceV0.Deserialize(a.Serialize());
            Assert.AreEqual(a.scenario, b.scenario);
            Assert.AreEqual(a.configHash, b.configHash);
            Assert.AreEqual(a.matchSeed, b.matchSeed);
            Assert.AreEqual(a.tickCount, b.tickCount);
            Assert.AreEqual(a.events.Count, b.events.Count);
            Assert.AreEqual(a.finalKills, b.finalKills);
            Assert.AreEqual(a.finalScore, b.finalScore);
            Assert.AreEqual(a.finalLeaks, b.finalLeaks);
            Assert.AreEqual(a.finalStateHash, b.finalStateHash);
            Assert.IsNull(a.DiffAgainst(b), "왕복본은 자기 자신과 parity 여야 한다");
        }

        [Test]
        public void ContinuousValue_UsesEpsilon_NotExact()
        {
            // parity 기준: 연속 물리값은 epsilon(저장·비교 해상도 1e-3).
            var near = new TraceEvent { tick = 0, channel = TraceChannel.DamageNumber, a = 1, f = 12.5001f };
            var same = new TraceEvent { tick = 0, channel = TraceChannel.DamageNumber, a = 1, f = 12.5f };
            var far = new TraceEvent { tick = 0, channel = TraceChannel.DamageNumber, a = 1, f = 12.52f };
            Assert.IsTrue(near.SameAs(same), "1e-3 격자 안의 차이는 분기가 아니다");
            Assert.IsFalse(far.SameAs(same), "격자를 넘는 차이는 분기다");
        }

        [Test]
        public void IntegerFields_AreExact()
        {
            var a = new TraceEvent { tick = 0, channel = TraceChannel.EnemyKilled, a = 1, b = 2, i = 5 };
            var b = a; b.i = 6;
            Assert.IsFalse(a.SameAs(b), "정수(점수·kind·index)는 exact 다");
            var c = a; c.b = 3;
            Assert.IsFalse(a.SameAs(c), "SimEntityId 축도 exact 다");
        }

        [Test]
        public void CorruptFile_Throws_RatherThanSilentlyParsing()
        {
            Assert.Throws<System.FormatException>(() => LegacyTraceV0.Deserialize("NOPE\n"));
            // 선언 수와 실제 수가 다르면 «앞부분만 맞는 골든» 이 되어 조용히 통과한다.
            string t = Sample().Serialize().Replace("events=3", "events=4");
            Assert.Throws<System.FormatException>(() => LegacyTraceV0.Deserialize(t));
        }

        [Test]
        public void Diff_NamesConfigDriftFirst()
        {
            var a = Sample();
            var b = LegacyTraceV0.Deserialize(a.Serialize());
            b.configHash = "ffffffffffffffff";
            b.finalKills = 999;   // 값 회귀도 같이 있지만
            StringAssert.Contains("configHash", a.DiffAgainst(b),
                "조건이 갈렸으면 그 얘기를 먼저 해야 한다 — 아니면 드리프트를 회귀로 오진한다");
        }

        [Test]
        public void StoredCorpus_ParsesAndRoundTrips()
        {
            if (!Directory.Exists(GoldenDir))
            {
                Assert.Ignore($"골든 코퍼스가 아직 없다({GoldenDir}) — Play 에서 재생성 메뉴를 돌려라.");
                return;
            }
            var files = Directory.GetFiles(GoldenDir, "*.trace.txt");
            Assert.Greater(files.Length, 0, "코퍼스 디렉터리가 비어 있다");
            foreach (var f in files)
            {
                string text = File.ReadAllText(f);
                LegacyTraceV0 t = null;
                Assert.DoesNotThrow(() => t = LegacyTraceV0.Deserialize(text), $"{f} 파싱 실패");
                Assert.AreEqual(text, t.Serialize(), $"{f} 왕복 불일치");
                Assert.IsNotEmpty(t.configHash, $"{f} 에 configHash 가 없다 — 드리프트 판독이 불가능해진다");
                Assert.Greater(t.tickCount, 0, $"{f} tickCount");
            }
        }
    }
}
