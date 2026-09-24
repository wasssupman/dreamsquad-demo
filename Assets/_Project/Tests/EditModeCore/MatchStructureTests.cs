using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 4 — **거점이 판에 선다.**
    //
    // 이 파일이 묻는 것은 하나다: 「공성형 적이 마음을 깎을 수 있나」. 그 전까지 규칙은
    // 전부 서 있었는데 **때릴 대상이 판에 없어서** 아무 일도 안 일어났다 — 배선만 있고
    // 생산자가 없던 자리다.
    [TestFixture]
    public class MatchStructureTests
    {
        // 「때릴 줄 아는 적」 — 고정구의 기본 적은 피해 산출이 비어 있어 아무도 못 깎는다.
        private static MatchDefinition Armed(float damage = 20f)
        {
            var def = CoreMatchFixtures.Definition();
            for (int i = 0; i < def.Enemies.Length; i++)
            {
                var a = AttackDef.Default();
                a.Mode = (int)TargetMode.Nearest;
                a.Outputs = new[]
                {
                    new AttackOutputDef { Kind = AttackOutputKind.Damage, Magnitude = damage },
                };
                def.Enemies[i].Attack = a;
            }
            return def;
        }

        private static BattleMatch Begin(MatchDefinition def)
        {
            def.ConfigHash = def.ComputeConfigHash();
            return CoreMatchFixtures.BeginBattle(def);
        }

        [Test]
        public void 마음_타워는_골마다_하나이고_체력을_안_든다()
        {
            var def = Armed();
            def.Map.Goals = new[] { new int2(11, 2), new int2(11, 4) };
            def.Map.CloseReservedPlacement();
            var match = Begin(def);

            Assert.AreEqual(2, match.Heart.TowerCount, "마음 타워는 골마다 하나다");

            int towers = 0;
            var units = match.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Faction != Faction.DefenderCore) continue;
                towers++;
                Assert.AreEqual(UnitKind.Structure, u.Kind);
                Assert.AreEqual(0f, u.MaxHealth, 1e-4f,
                    "체력 미러 금지(X29) — 마음의 체력은 담당자 하나가 든다");
                Assert.IsTrue(u.HealthExternal);
                Assert.AreEqual(0.5f, u.HitRadius, 1e-4f, "몸 = 1×1 점유의 내접원");
            }
            Assert.AreEqual(2, towers);
        }

        [Test]
        public void 마음_미저작이면_타워를_안_세운다()
        {
            var def = Armed();
            def.Heart = new HeartDef { MaxHealth = 0f, KillHealPerAwakening = 0f };
            var match = Begin(def);

            Assert.AreEqual(0, match.Heart.TowerCount,
                "덱이 마음을 저작하지 않은 판은 마음이 없는 판이다");
            Assert.AreEqual(0f, match.Heart.Stress, 1e-4f);
        }

        [Test]
        public void 공성형은_마음_타워를_때리고_그만큼_마음이_깎인다()
        {
            var match = Begin(Armed());
            var slain = CoreMatchFixtures.Listen(match, CoreEventKind.UnitSlain);
            var destroyed = CoreMatchFixtures.Listen(match, CoreEventKind.UnitDestroyed);
            var damage = CoreMatchFixtures.Listen(match, CoreEventKind.DamageApplied);

            // 골 옆에 세운다 — 걸어오는 시간을 묻는 테스트가 아니다.
            match.Apply(Command.DebugSpawnEnemy(0, new int2(10, 2)));
            for (int t = 0; t < 60 * 10 && match.Heart.Health >= match.Heart.MaxHealth; t++)
                match.Tick();

            Assert.Less(match.Heart.Health, match.Heart.MaxHealth, "마음이 깎였다");
            Assert.Greater(match.Heart.Stress, 0f, "스트레스는 그 체력의 표시 반전이다");
            Assert.AreEqual(0, match.Heart.Leaks,
                "공성은 「놓쳤다」가 아니다 — 마음 앞에서 아직 잡을 수 있다");

            // 타워는 **죽지도 사라지지도 않는다.** 체력이 개체에 없으므로 0 이 될 수 없고,
            // 판을 끝내는 것은 담당자의 저수지다.
            for (int i = 0; i < slain.Count; i++)
                Assert.AreNotEqual(Faction.DefenderCore, slain[i].Faction, "타워는 처치 대상이 아니다");
            for (int i = 0; i < destroyed.Count; i++)
                Assert.AreNotEqual(Faction.DefenderCore, destroyed[i].Faction, "타워는 사라지지 않는다");

            bool towerHit = false;
            for (int i = 0; i < damage.Count; i++)
                if (damage[i].Faction == Faction.DefenderCore) towerHit = true;
            Assert.IsTrue(towerHit, "피해 사건은 난다 — 화면이 숫자를 띄울 근거가 있어야 한다");
        }

        [Test]
        public void 마음이_다_깎이면_그_판이_끝난다()
        {
            var def = Armed(damage: 200f);
            def.Heart = new HeartDef { MaxHealth = 300f, KillHealPerAwakening = 0f };
            var match = Begin(def);
            var collapsed = CoreMatchFixtures.Listen(match, CoreEventKind.HeartCollapsed);

            match.Apply(Command.DebugSpawnEnemy(0, new int2(10, 2)));
            for (int t = 0; t < 60 * 20 && !match.Clock.Ended; t++) match.Tick();

            Assert.AreEqual(1, collapsed.Count);
            Assert.AreEqual(MatchEndReason.StressFull, match.Clock.EndReason,
                "공성으로도 「패배」에 닿는다 — 통로는 여전히 하나다(X18)");
        }

        [Test]
        public void 본능이_살아_있는_동안_마음이_표적에서_빠진다()
        {
            var def = Armed();
            // 방어 본능 하나 — 공격은 안 한다(방패로만 선다). 체력을 넉넉히 두는 이유:
            // 방패가 **제 일을 하면** 적이 본능을 때리므로, 얇게 두면 관찰 창 안에서 본능이
            // 먼저 죽어 「방패가 없었는지 방패가 깨졌는지」를 구분할 수 없게 된다.
            CoreMatchFixtures.AddStructure(def, new int2(8, 2), Faction.DefenderInstinct, health: 1000f);
            var match = Begin(def);

            Assert.IsTrue(match.Heart.CoreShielded);
            match.Apply(Command.DebugSpawnEnemy(0, new int2(10, 2)));
            for (int t = 0; t < 60 * 5; t++) match.Tick();

            Assert.AreEqual(match.Heart.MaxHealth, match.Heart.Health, 1e-3f,
                "본능이 서 있는 동안은 마음이 조준 후보가 아니다");
            var tower = FirstOf(match, Faction.DefenderCore);
            Assert.IsTrue(tower.Untargetable);

            // 본능을 부순다 — 마지막 본능이 무너지는 순간 방패가 떨어진다.
            var instinct = FirstOf(match, Faction.DefenderInstinct);
            instinct.Inbox.Damage.Add(new DamageEntry { Amount = 9999f, Source = SimEntityId.Match });
            for (int t = 0; t < 4; t++) match.Tick();

            Assert.IsFalse(match.Heart.CoreShielded);
            Assert.IsFalse(tower.Untargetable);

            for (int t = 0; t < 60 * 5 && match.Heart.Health >= match.Heart.MaxHealth; t++) match.Tick();
            Assert.Less(match.Heart.Health, match.Heart.MaxHealth, "방패가 깨지면 마음이 깎인다");
        }

        [Test]
        public void 방패_걸린_마음은_더_가까워도_목적지가_아니다()
        {
            var def = Armed();
            // 본능을 스폰 쪽에, 적을 마음 바로 앞에 — 거리로만 고르면 마음이 이긴다.
            CoreMatchFixtures.AddStructure(def, new int2(3, 2), Faction.DefenderInstinct, health: 1000f);
            var match = Begin(def);
            Assert.IsTrue(match.Heart.CoreShielded);

            match.Apply(Command.DebugSpawnEnemy(0, new int2(10, 2)));
            var enemy = FirstOf(match, Faction.EnemyUnit);
            match.Tick();

            Assert.IsTrue(enemy.Move.HasStructureDest);
            Assert.AreEqual(new int2(3, 2), enemy.Move.StructureDest,
                "방패가 서 있는 동안 적은 마음이 아니라 본능을 향한다 — 조준만 빼고 경로를 남기면 " +
                "적이 방패 걸린 마음 앞에서 때리지도 못하고 서 있는다");

            var instinct = FirstOf(match, Faction.DefenderInstinct);
            instinct.Inbox.Damage.Add(new DamageEntry { Amount = 9999f, Source = SimEntityId.Match });
            for (int t = 0; t < 4; t++) match.Tick();

            // 방패가 떨어지면 거점 목적지는 **비고** 골 흐름으로 돌아간다 — 마음은 골 자리에
            // 서므로 「가장 가까운 마음」은 골 슬롯(N-소스 흐름장)이 이미 안다. 마음에 닿았는지는
            // 마음이 깎이는 것으로 증언한다.
            Assert.IsFalse(enemy.Move.HasStructureDest, "본능이 무너지면 갈 거점이 없다 — 골 흐름 폴백");
            for (int t = 0; t < 60 * 5 && match.Heart.Health >= match.Heart.MaxHealth; t++) match.Tick();
            Assert.Less(match.Heart.Health, match.Heart.MaxHealth, "방패가 떨어진 뒤 적이 마음에 닿아 깎는다");
        }

        [Test]
        public void 본능의_죽음은_보통_유닛의_죽음이다()
        {
            var def = Armed();
            CoreMatchFixtures.AddStructure(def, new int2(8, 2), Faction.DefenderInstinct, health: 100f);
            var match = Begin(def);
            var destroyed = CoreMatchFixtures.Listen(match, CoreEventKind.UnitDestroyed);

            var instinct = FirstOf(match, Faction.DefenderInstinct);
            Assert.AreEqual(100f, instinct.MaxHealth, 1e-4f, "본능의 체력은 자기 것이다");
            Assert.AreEqual(1.5f, instinct.HitRadius, 1e-4f, "몸 = 3×3 점유의 내접원");
            Assert.IsFalse(instinct.HealthExternal);

            instinct.Inbox.Damage.Add(new DamageEntry { Amount = 9999f, Source = SimEntityId.Match });
            for (int t = 0; t < 4; t++) match.Tick();

            Assert.AreEqual(0, match.Score.Kills, "거점은 적이 아니다 — 점수를 안 준다");
            bool gone = false;
            for (int i = 0; i < destroyed.Count; i++)
                if (destroyed[i].Faction == Faction.DefenderInstinct) gone = true;
            Assert.IsTrue(gone, "모든 소멸은 소멸 이벤트를 낸다(계약 7)");
        }

        [Test]
        public void 무너진_본능은_더_이상_목적지가_아니다()
        {
            var def = Armed();
            CoreMatchFixtures.AddStructure(def, new int2(8, 2), Faction.DefenderInstinct, health: 100f);
            var match = Begin(def);

            match.Apply(Command.DebugSpawnEnemy(0, new int2(2, 2)));
            var enemy = FirstOf(match, Faction.EnemyUnit);
            match.Tick();
            Assert.IsTrue(enemy.Move.HasStructureDest, "살아 있는 본능은 목적지다");

            var instinct = FirstOf(match, Faction.DefenderInstinct);
            instinct.Inbox.Damage.Add(new DamageEntry { Amount = 9999f, Source = SimEntityId.Match });
            for (int t = 0; t < 3; t++) match.Tick();

            Assert.IsFalse(enemy.Move.HasStructureDest,
                "자리만 보면 적이 **잔해를 향해 계속 걸어간다** — 저작 자리는 판 내내 남는다");
        }

        [Test]
        public void 거점은_상태이상과_모디파이어에_전면_면역이다()
        {
            var def = Armed();
            CoreMatchFixtures.AddStructure(def, new int2(8, 2), Faction.DefenderInstinct, health: 100f);
            var match = Begin(def);

            var instinct = FirstOf(match, Faction.DefenderInstinct);
            var tower = FirstOf(match, Faction.DefenderCore);

            foreach (var victim in new[] { instinct, tower })
            {
                Assert.IsFalse(EffectEligibility.AcceptsCc(victim, CcRequestKind.Stun), "행동불능 거절(F3)");
                Assert.IsFalse(EffectEligibility.AcceptsModifier(victim), "스탯·스택 거절(F3)");
                Assert.IsTrue(EffectEligibility.AcceptsHeal(victim),
                    "회복 하나만 의도적으로 열려 있다");

                Assert.IsFalse(match.World.RequestCc(
                    CcRequest.Of(victim.Id, CcRequestKind.Stun, 1f, SimEntityId.Match)));
            }
            Assert.AreEqual(0, match.World.CcRequests.Count, "요청 줄에 한 건도 안 들어간다");

            // 유닛은 같은 문으로 들어간다 — 거절이 「아무도 못 건다」가 되면 안 된다.
            match.Apply(Command.DebugSpawnEnemy(0, new int2(5, 2)));
            var enemy = FirstOf(match, Faction.EnemyUnit);
            Assert.IsTrue(match.World.RequestCc(
                CcRequest.Of(enemy.Id, CcRequestKind.Stun, 1f, SimEntityId.Match)));
        }

        [Test]
        public void 스폰_골_본능_자리에는_못_놓는다()
        {
            var def = Armed();
            CoreMatchFixtures.AddStructure(def, new int2(8, 2), Faction.DefenderInstinct, health: 100f);
            var match = Begin(def);

            Assert.AreEqual(RejectReason.NotBuildable, match.Placement.Judge(0, new int2(0, 1)),
                "적이 튀어나오는 칸 위에는 못 세운다");
            Assert.AreEqual(RejectReason.NotBuildable, match.Placement.Judge(0, new int2(11, 2)),
                "마음이 선 칸 위에는 못 세운다");
            Assert.AreEqual(RejectReason.NotBuildable, match.Placement.Judge(0, new int2(8, 2)),
                "본능이 선 칸 위에는 못 세운다");
            Assert.AreEqual(RejectReason.NotBuildable, match.Placement.Judge(0, new int2(7, 1)),
                "3×3 본능은 **전 칸**이 닫힌다 — 한 칸만 닫으면 건물이 벽을 파고든다");
            Assert.AreEqual(RejectReason.None, match.Placement.Judge(0, new int2(6, 1)),
                "footprint 밖은 그대로 열려 있다(구 「주변 배치 배제」는 폐기됐다)");
        }

        [Test]
        public void 본능은_통행을_막지_않는다()
        {
            var def = Armed();
            CoreMatchFixtures.AddStructure(def, new int2(8, 2), Faction.DefenderInstinct, health: 100f);
            var match = Begin(def);
            match.Tick();

            Assert.IsFalse(match.Map.Obstacles.HasObstacles,
                "거점은 점유만 선언한다 — 「본능 footprint 는 벽」은 2026-08-12 에 폐기됐다");
        }

        private static Unit FirstOf(BattleMatch match, Faction faction)
        {
            var units = match.World.Units;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Faction == faction) return units[i];
            return null;
        }
    }
}
