using NUnit.Framework;
using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 1 — 골든 2종.
    //
    // ⚠ **정본 베이커는 Unity 메뉴**(`Wassup/BattleCore/Golden/Bake Missing`)다.
    // 이 테스트가 파일이 없을 때 구워 주는 것은 헤드리스 lane 이 첫 실행에서 스스로
    // 설 수 있게 하기 위한 **부트스트랩**일 뿐이다. 이미 있는 골든은 절대 덮지 않는다 —
    // 덮으면 그 시나리오가 지키던 회귀 감시가 그 자리에서 무효가 된다.
    [TestFixture]
    public class CoreGoldenTests
    {
        [Test]
        public void 저장소_루트를_찾는다()
        {
            Assert.IsNotNull(CoreGoldenStore.RepoRoot,
                "골든 폴더를 못 찾으면 아래 테스트들은 «통과하지만 아무것도 증언하지 않는다»");
        }

        [TestCase("empty_board")]
        [TestCase("spawn_destroy")]
        [TestCase("march_to_goal")]
        [TestCase("detour_obstacle")]
        [TestCase("detect_and_chase")]
        [TestCase("kill_race_basic")]
        [TestCase("kill_race_3min")]
        [TestCase("wave_clear_8")]
        [TestCase("time_attack_8")]
        [TestCase("heart_collapse")]
        public void 골든과_일치한다(string name)
        {
            var sc = CoreGoldenCorpus.ByName(name);
            Assert.IsNotNull(sc, $"코퍼스에 '{name}' 이 없다");

            var run = CoreGoldenCorpus.Run(sc);

            Assert.Greater(run.events.Count, 0, "빈 트레이스는 저장하지도 대조하지도 않는다");
            Assert.IsNotEmpty(run.configHash);

            if (!CoreGoldenStore.Exists(name))
            {
                CoreGoldenStore.Write(name, run);
                Assert.Pass($"골든 '{name}' 을 새로 구웠다(이벤트 {run.events.Count}). "
                            + "다음 실행부터 대조한다.");
            }

            var golden = CoreGoldenStore.Read(name);
            string diff = golden.DiffAgainst(run);
            Assert.IsNull(diff, $"'{name}' — {diff}");
        }

        [Test]
        public void empty_board_는_시작과_종료_두_사건뿐이다()
        {
            var run = CoreGoldenCorpus.Run(CoreGoldenCorpus.ByName("empty_board"));

            Assert.AreEqual(2, run.events.Count, "비어 있는 판은 시작과 종료만 낸다");
            Assert.AreEqual(CoreTraceChannel.MatchStarted, run.events[0].channel);
            Assert.AreEqual(0, run.events[0].tick);
            Assert.AreEqual(CoreTraceChannel.MatchEnded, run.events[1].channel);
            Assert.AreEqual(10800, run.events[1].tick);
            Assert.AreEqual((int)MatchEndReason.Complete, run.events[1].i);
        }

        [Test]
        public void spawn_destroy_는_스폰3_소멸1_을_증언한다()
        {
            var run = CoreGoldenCorpus.Run(CoreGoldenCorpus.ByName("spawn_destroy"));

            int spawns = 0, destroys = 0, ends = 0, goals = 0, other = 0;
            for (int i = 0; i < run.events.Count; i++)
            {
                switch (run.events[i].channel)
                {
                    case CoreTraceChannel.UnitSpawned: spawns++; break;
                    case CoreTraceChannel.UnitDestroyed: destroys++; break;
                    case CoreTraceChannel.MatchEnded: ends++; break;
                    case CoreTraceChannel.GoalReached: goals++; break;
                    default: other++; break;
                }
            }

            Assert.AreEqual(3, spawns);
            Assert.AreEqual(1, destroys);
            // 300틱짜리 시나리오라 판은 아직 안 끝난다 — 끝났다면 시계가 잘못 세고 있다.
            Assert.AreEqual(0, ends, "300틱에 판이 끝나면 안 된다");
            // unit 2 부터 적이 걷는다 — 3×3 픽스처의 골이 두 칸 앞이라 살아남은 둘이 닿는다.
            // 「시작 1 + 스폰 3 + 소멸 1」 이라는 unit 1 의 문장은 여기서 골 도달만큼 늘어난다.
            Assert.AreEqual(2, goals, "소멸한 하나를 뺀 둘이 골에 닿는다");
            Assert.AreEqual(1, other, "나머지는 시작 사건 하나뿐");
            Assert.AreEqual(7, run.events.Count);
        }
    }
}
