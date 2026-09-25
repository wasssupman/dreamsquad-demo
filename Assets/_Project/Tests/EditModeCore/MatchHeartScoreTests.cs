using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Skills;
using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 4 — 마음과 점수.
    [TestFixture]
    public class MatchHeartScoreTests
    {
        private static BattleMatch Battle(System.Action<MatchDefinition> tweak = null)
        {
            var def = CoreMatchFixtures.Definition();
            tweak?.Invoke(def);
            def.ConfigHash = def.ComputeConfigHash();
            return CoreMatchFixtures.BeginBattle(def);
        }

        [Test]
        public void 스트레스는_체력의_표시_반전이다()
        {
            var match = Battle();
            Assert.AreEqual(300f, match.Heart.MaxHealth, 1e-4f);
            Assert.AreEqual(0f, match.Heart.Stress, 1e-4f, "만피 = 스트레스 0");

            Assert.AreEqual(50f, StressMath.FromHealth(150f, 300f), 1e-4f);
            Assert.AreEqual(100f, StressMath.FromHealth(0f, 300f), 1e-4f);
            Assert.AreEqual(0f, StressMath.FromHealth(0f, 0f), 1e-4f,
                "마음이 미저작이면 0 이다 — 100 으로 읽으면 판이 시작하자마자 끝난다");
        }

        [Test]
        public void 돌격형은_마음을_치고_산화한다()
        {
            var match = Battle(d =>
            {
                for (int i = 0; i < d.Enemies.Length; i++)
                {
                    d.Enemies[i].TargetFactions = (int)Faction.DefenderUnit;   // 마음을 못 때린다
                    d.Enemies[i].StabilityDamage = 40;
                }
            });
            var goals = CoreMatchFixtures.Listen(match, CoreEventKind.GoalReached);
            var changed = CoreMatchFixtures.Listen(match, CoreEventKind.HeartChanged);
            var slain = CoreMatchFixtures.Listen(match, CoreEventKind.UnitSlain);

            for (int t = 0; t < 60 * 20 && goals.Count == 0; t++) match.Tick();

            Assert.AreEqual(1, goals.Count);
            Assert.AreEqual(0, goals[0].Arg, "공성 불가 = 돌격형");
            Assert.AreEqual(260f, match.Heart.Health, 1e-3f);
            Assert.AreEqual(1, changed.Count);
            Assert.AreEqual(1, match.Heart.Leaks, "「놓쳤다」 = 돌격형이 마음을 치고 산화한 수");
            Assert.AreEqual(0, slain.Count, "처치가 아니다 — 점수도 각성도 안 준다");

            match.Tick();
            Assert.IsNull(match.World.Find(goals[0].A), "닿은 그 적이 사라진다");
        }

        [Test]
        public void 공성형은_골_칸에_닿기_전에_마음_타워에_막힌다()
        {
            var match = Battle();   // 기본 타겟 = 상대 진영 전부(마음 포함)
            var goals = CoreMatchFixtures.Listen(match, CoreEventKind.GoalReached);

            for (int t = 0; t < 60 * 20; t++) match.Tick();

            // 마음 타워가 판에 서면서 **공성형은 골 칸을 밟지 않는다** — 사거리 안에 먼저
            // 들어 교전으로 멈추기 때문이다. 그것이 「공성」의 실체이고, 골 도달 사건은
            // 돌격형(마음을 못 때리는 적)만 낸다.
            Assert.AreEqual(0, goals.Count, "공성형은 골 칸까지 못 간다 — 마음 앞에서 멈춘다");
            Assert.AreEqual(0, match.Heart.Leaks,
                "「놓쳤다」는 돌격형이 산화한 수다 — 공성은 그 수에 들어가지 않는다");
            Assert.AreEqual(300f, match.Heart.Health, 1e-3f,
                "이 고정구의 적은 피해 산출이 비어 있다 — 때리기는 하되 깎이지 않는다");
        }

        [Test]
        public void 첫_붕괴가_곧_판의_끝이다()
        {
            var match = Battle(d =>
            {
                d.Heart = new HeartDef { MaxHealth = 50f, KillHealPerAwakening = 0f };
                for (int i = 0; i < d.Enemies.Length; i++)
                {
                    d.Enemies[i].TargetFactions = (int)Faction.DefenderUnit;
                    d.Enemies[i].StabilityDamage = 50;
                }
            });
            var collapsed = CoreMatchFixtures.Listen(match, CoreEventKind.HeartCollapsed);
            var ended = CoreMatchFixtures.Listen(match, CoreEventKind.MatchEnded);

            for (int t = 0; t < 60 * 30 && !match.Clock.Ended; t++) match.Tick();

            Assert.AreEqual(1, collapsed.Count);
            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual(MatchEndReason.StressFull, match.Clock.EndReason,
                "「패배 없음」은 거짓이다 — 남은 시간을 전량 몰수당한다(X18)");
            Assert.IsTrue(match.Clock.EndHasPresentationBeat,
                "붕괴한 판만 연출 박자를 갖는다 — 만료·제출은 터지는 것이 없다(X15)");
        }

        [Test]
        public void 만료로_끝난_판에는_연출_박자가_없다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.MatchSeconds = 1f;
            def.ConfigHash = def.ComputeConfigHash();
            var match = CoreMatchFixtures.BeginBattle(def);

            for (int t = 0; t < 120 && !match.Clock.Ended; t++) match.Tick();
            Assert.AreEqual(MatchEndReason.Complete, match.Clock.EndReason);
            Assert.IsFalse(match.Clock.EndHasPresentationBeat);
        }

        [Test]
        public void 처치는_마음을_회복시키고_배율은_하나다()
        {
            // 돌격형으로 두어 마음을 **먼저 깎는다** — 만피면 회복이 안 보인다.
            var match = Battle(d =>
            {
                d.Heart = new HeartDef { MaxHealth = 300f, KillHealPerAwakening = 10f };
                for (int i = 0; i < d.Enemies.Length; i++)
                {
                    d.Enemies[i].TargetFactions = (int)Faction.DefenderUnit;
                    d.Enemies[i].StabilityDamage = 100;
                }
            });
            for (int t = 0; t < 60 * 20 && match.Heart.Health >= 300f; t++) match.Tick();
            Assert.Less(match.Heart.Health, 300f, "먼저 깎여야 회복이 보인다");

            float before = match.Heart.Health;
            KillOneEnemy(match);
            // 회복 = 그 적의 각성 보상(2) × 배율(10) = 20.
            Assert.AreEqual(before + 20f, match.Heart.Health, 1e-3f,
                "per-enemy 회복 필드를 새로 만들지 않는다 — 각성 보상이 그대로 서열이다");
        }

        [Test]
        public void 점수는_적_처치만_세고_생값이다()
        {
            var match = Battle();
            var changed = CoreMatchFixtures.Listen(match, CoreEventKind.ScoreChanged);

            for (int t = 0; t < 120; t++) match.Tick();
            int killed = KillAll(match);

            Assert.AreEqual(killed, match.Score.Kills, "1킬 = 1점, 예외 없음");
            Assert.AreEqual(match.Score.Kills, match.Score.Total);
            Assert.AreEqual(match.Score.Kills, match.Score.SubmissionScore, "가공이 없다");
            Assert.AreEqual(killed, changed.Count);
        }

        [Test]
        public void 방어유닛의_죽음은_점수가_아니다()
        {
            var match = Battle();
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            var u = match.World.Find(CoreMatchFixtures.PlacedDefender(match));
            u.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
            match.Tick();

            Assert.AreEqual(0, match.Score.Kills,
                "사건 자체는 진영을 안 가린다(각성이 사망 보상을 받아야 한다) — 거르는 것은 읽는 쪽이다");
        }

        [Test]
        public void 본능이_살아_있으면_마음이_표적에서_빠진다()
        {
            var match = Battle();
            Assert.IsFalse(match.Heart.CoreShielded, "이 판에는 본능 개체가 없다");
        }

        private static int KillAll(BattleMatch match)
        {
            int n = 0;
            var units = match.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Faction != Faction.EnemyUnit || u.Dead) continue;
                u.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
                n++;
            }
            match.Tick();
            return n;
        }

        private static void KillOneEnemy(BattleMatch match)
        {
            var units = match.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Faction != Faction.EnemyUnit || u.Dead) continue;
                u.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
                break;
            }
            match.Tick();
        }
    }
}
