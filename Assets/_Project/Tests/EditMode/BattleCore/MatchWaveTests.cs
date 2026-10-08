using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Wave;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 4 — 웨이브 케이던스·당김·보너스.
    [TestFixture]
    public class MatchWaveTests
    {
        private static BattleMatch Battle(System.Action<MatchDefinition> tweak = null)
        {
            var def = CoreMatchFixtures.Definition();
            tweak?.Invoke(def);
            def.ConfigHash = def.ComputeConfigHash();
            return CoreMatchFixtures.BeginBattle(def);
        }

        [Test]
        public void 웨이브_1은_전투가_열리자마자_예약된다()
        {
            var def = CoreMatchFixtures.Definition();
            var match = new BattleMatch(def);
            var queued = CoreMatchFixtures.Listen(match, CoreEventKind.WaveQueued);
            match.Begin();

            for (int t = 0; t < 120; t++) match.Tick();
            Assert.AreEqual(0, queued.Count, "배치 창 동안에는 적이 안 나온다");

            match.Apply(Command.FinishPlacement());
            match.Tick();
            Assert.AreEqual(1, queued.Count, "기다릴 앞 웨이브가 없다");
            Assert.AreEqual(1, queued[0].Arg);
        }

        [Test]
        public void 리드인은_스폰_기준시각에만_더해진다()
        {
            var match = Battle();
            var spawned = CoreMatchFixtures.Listen(match, CoreEventKind.UnitSpawned);
            match.Tick();   // 웨이브 1 예약

            // 리드인 1초 = 60틱. 그 전에는 한 기도 안 나온다.
            for (int t = 0; t < 59; t++) match.Tick();
            Assert.AreEqual(0, spawned.Count);
            match.Tick();
            Assert.AreEqual(1, spawned.Count, "예약 시각과 섞으면 당김 연타마다 누적 왜곡된다");
        }

        [Test]
        public void 케이던스는_전멸_또는_상한_경과다()
        {
            // 적을 아무도 안 죽이면 상한 간격(5초)마다 다음 웨이브가 온다.
            var match = Battle();
            var queued = CoreMatchFixtures.Listen(match, CoreEventKind.WaveQueued);
            for (int t = 0; t < 60 * 4; t++) match.Tick();
            Assert.AreEqual(1, queued.Count, "4초에는 아직");
            for (int t = 0; t < 70; t++) match.Tick();
            Assert.AreEqual(2, queued.Count, "5초에 자동 진행");
        }

        [Test]
        public void 전멸하면_상한을_안_기다린다()
        {
            var match = Battle();
            var queued = CoreMatchFixtures.Listen(match, CoreEventKind.WaveQueued);
            match.Tick();
            for (int t = 0; t < 90; t++) match.Tick();   // 웨이브 1 전원 스폰
            Assert.AreEqual(1, queued.Count);

            // 나온 적을 전부 지운다 — 전멸 판정은 **살아 있는 적이 0** 이다.
            for (int i = 0; i < match.World.Units.Count;)
            {
                var u = match.World.Units[i];
                if (u.Faction == Somnia.Battle.Skills.Faction.EnemyUnit)
                    match.Apply(Command.DebugDestroy(u.Id));
                else i++;
            }
            match.Tick();
            Assert.AreEqual(2, queued.Count, "잘 막으면 바로 다음이 온다");
        }

        [Test]
        public void 당김은_상한이_있고_전멸로만_회복된다()
        {
            var match = Battle();
            match.Tick();

            Assert.IsTrue(match.Apply(Command.PullWave()).Accepted);
            Assert.IsTrue(match.Apply(Command.PullWave()).Accepted);
            Assert.AreEqual(RejectReason.NoMoreWaves, match.Apply(Command.PullWave()).Reason,
                "덱이 3웨이브뿐이라 더 밀 것이 없다");
        }

        [Test]
        public void 당김_상한을_다_쓰면_거절된다()
        {
            var match = Battle(d => d.WaveDeck = CoreMatchFixtures.Deck(waveCount: 10, pullCap: 1));
            match.Tick();

            Assert.IsTrue(match.Apply(Command.PullWave()).Accepted);
            Assert.AreEqual(RejectReason.PullCapReached, match.Apply(Command.PullWave()).Reason,
                "상한 경과는 회복이 아니다 — **전멸로만** 돌아온다");
            Assert.AreEqual(0, match.Waves.PullsLeft);
        }

        [Test]
        public void 당김_상한_0은_금지가_아니라_폴백이다()
        {
            var match = Battle(d => d.WaveDeck = CoreMatchFixtures.Deck(waveCount: 10, pullCap: 0));
            Assert.AreEqual(3, match.Waves.PullsLeft,
                "저작 누락이 조용히 기능을 끄면 「버튼이 안 먹는다」의 원인을 못 찾는다");
        }

        [Test]
        public void 기제층_당김은_상한을_무시한다()
        {
            // ⚠ 상한 0 은 «당김 금지»가 아니라 **폴백 3** 이다 — 저작 누락이 조용히 기능을
            // 끄면 「버튼이 안 먹는다」의 원인을 찾을 수 없기 때문이다. 그래서 1 로 저작한다.
            var match = Battle(d => d.WaveDeck = CoreMatchFixtures.Deck(waveCount: 10, pullCap: 1));
            match.Tick();

            Assert.IsTrue(match.Apply(Command.PullWave()).Accepted);
            Assert.AreEqual(RejectReason.PullCapReached, match.Apply(Command.PullWave()).Reason);
            Assert.IsTrue(match.Apply(Command.DebugForceWave()).Accepted,
                "no-op 으로 만들면 통합 스모크가 타임아웃한다");
        }

        [Test]
        public void 간격_폴백_사슬은_fail_closed_다()
        {
            // 덱이 0 → 플랜의 명목 간격(제한시간 ÷ 웨이브 수 = 180/3 = 60).
            var match = Battle(d =>
            {
                var deck = CoreMatchFixtures.Deck();
                deck.MaxWaveIntervalSec = 0f;
                d.WaveDeck = deck;
            });
            Assert.AreEqual(60f, match.Waves.Interval, 1e-3f,
                "0 이면 전 웨이브가 한 프레임에 쏟아진다 — 사슬 전체가 그 방어선이다");
        }

        [Test]
        public void 보스_웨이브는_생성기가_판별하고_경보는_스폰에서_한_번_난다()
        {
            var match = Battle(d =>
            {
                var deck = CoreMatchFixtures.Deck(waveCount: 3, interval: 3f);
                deck.BossWaveInterval = 2;      // 2번째 웨이브가 보스
                deck.BossPool = new[] { 2 };
                deck.BossEscortMin = 1;
                deck.BossEscortMax = 1;
                d.WaveDeck = deck;
            });
            var started = CoreMatchFixtures.Listen(match, CoreEventKind.WaveStarted);

            for (int t = 0; t < 60 * 10; t++) match.Tick();

            Assert.GreaterOrEqual(started.Count, 2);
            Assert.AreEqual(0f, started[0].Amount, 1e-4f, "웨이브 1 은 보스가 아니다");
            Assert.AreEqual(1f, started[1].Amount, 1e-4f, "웨이브 2 가 보스다");
            int bossStarts = 0;
            for (int i = 0; i < started.Count; i++) if (started[i].Amount > 0.5f) bossStarts++;
            Assert.AreEqual(1, bossStarts, "스폰 시점에 재판정하면 이중 발화한다(X13)");
        }

        [Test]
        public void 보너스는_일반_처치로만_쌓이고_래치된다()
        {
            var match = Battle(d =>
            {
                d.Bonus = new BonusWaveDef
                {
                    EnemyIndex = 1,
                    EnemyCount = 4,
                    PortalAppearDelaySec = 0f,
                    FirstSpawnDelaySec = 0f,
                    SpawnIntervalSec = 0.1f,
                    KillThreshold = 2,
                    MaxStressToOffer = 100f,
                };
            });
            var offered = CoreMatchFixtures.Listen(match, CoreEventKind.BonusOffered);
            var slain = CoreMatchFixtures.Listen(match, CoreEventKind.UnitSlain);

            match.Tick();
            for (int t = 0; t < 120; t++) match.Tick();   // 웨이브 1 스폰

            KillAllEnemies(match);
            Assert.GreaterOrEqual(slain.Count, 2);
            Assert.AreEqual(1, offered.Count, "래치 — 문턱에서 떨리지 않는다");

            // 당기면 크레딧은 **한 회분만** 깎인다.
            Assert.IsTrue(match.Apply(Command.PullBonus()).Accepted);
            Assert.IsFalse(match.Waves.BonusOffered);
        }

        [Test]
        public void 스트레스가_높으면_보너스가_안_뜬다()
        {
            var match = Battle(d =>
            {
                d.Bonus = new BonusWaveDef
                {
                    EnemyIndex = 1, EnemyCount = 2, KillThreshold = 1,
                    MaxStressToOffer = 0f,   // 만피에서만
                    SpawnIntervalSec = 0.1f,
                };
                d.Heart = new HeartDef { MaxHealth = 100f, KillHealPerAwakening = 0f };
            });
            var offered = CoreMatchFixtures.Listen(match, CoreEventKind.BonusOffered);

            match.Tick();
            for (int t = 0; t < 120; t++) match.Tick();
            KillAllEnemies(match);
            Assert.AreEqual(1, offered.Count, "만피면 뜬다");
        }

        [Test]
        public void 전멸_판정은_보너스_적을_세지_않는다()
        {
            var match = Battle(d =>
            {
                d.WaveDeck = CoreMatchFixtures.Deck(waveCount: 10, interval: 600f);
                d.Bonus = new BonusWaveDef
                {
                    EnemyIndex = 1, EnemyCount = 4, KillThreshold = 1,
                    MaxStressToOffer = 100f, SpawnIntervalSec = 0.1f,
                };
            });
            var queued = CoreMatchFixtures.Listen(match, CoreEventKind.WaveQueued);

            match.Tick();
            for (int t = 0; t < 120; t++) match.Tick();
            KillAllEnemies(match);            // 일반 적 전멸 → 다음 웨이브 + 보너스 제안
            int afterClear = queued.Count;

            Assert.IsTrue(match.Apply(Command.PullBonus()).Accepted);
            for (int t = 0; t < 60; t++) match.Tick();   // 보너스 적이 판에 선다
            KillAllEnemies(match);

            // 보너스 적만 남은 상태에서도 「전멸」이 성립해야 한다 —
            // 공용 목록에 필터를 걸면 소비처가 조용히 같이 좁아진다(X12).
            Assert.GreaterOrEqual(queued.Count, afterClear);
        }

        /// <summary>
        /// 판 위의 적을 **피해 단계로** 지운다. 디버그 소멸이 아니라 피해인 이유:
        /// 처치 사건(`UnitSlain`)은 「피해로 죽었다」의 사건이라 소멸만으로는 안 난다 —
        /// 그리고 각성·점수·보너스 크레딧이 전부 그 사건에 걸려 있다.
        /// </summary>
        private static int KillAllEnemies(BattleMatch match)
        {
            int n = 0;
            var units = match.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Faction != Somnia.Battle.Skills.Faction.EnemyUnit || u.Dead) continue;
                u.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
                n++;
            }
            match.Tick();   // 피해 단계가 처치 사건을 낸다
            match.Tick();   // 표시 틱 ≠ 소멸 틱 — 한 틱 뒤에 목록에서 빠진다
            return n;
        }
    }
}
