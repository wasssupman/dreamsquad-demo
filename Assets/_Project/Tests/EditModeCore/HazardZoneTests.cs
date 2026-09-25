using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Skills;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6b — **존 장판.** 판 위에 깔려 밟은 대상에게 효과를 뿜는 개체.
    //
    // ⚠ 수치는 픽스처다(게임 값의 정본은 SO → 빌더 → 정의표).
    [TestFixture]
    public class HazardZoneTests
    {
        private const float Dt = BattleMatch.Dt;

        private static MatchDefinition Def(params HazardDef[] hazards)
        {
            var def = CoreMatchFixtures.Definition();
            def.Enemies[0].MoveSpeed = 0f;   // 제자리 — 멤버십만 묻는다
            def.Hazards = hazards;
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        private static HazardDef Zone(HazardShapeKind shape, float lifetime, params HazardEffectDef[] effects)
            => new HazardDef { Id = "fixture_zone", Shape = (int)shape, Radius = 1, Lifetime = lifetime, Effects = effects };

        private static HazardEffectDef Slow(float mul, float rest, int factions = (int)Faction.EnemyUnit)
            => new HazardEffectDef { Kind = (int)HazardEffectKind.Slow, Magnitude = mul, RestDuration = rest, TargetFactions = factions };

        private static HazardEffectDef Burn(float dps, float rest)
            => new HazardEffectDef
            {
                Kind = (int)HazardEffectKind.DoT, Magnitude = dps, RestDuration = rest,
                Element = (int)DotElement.Fire, TargetFactions = (int)Faction.EnemyUnit,
            };

        private static Unit SpawnGrunt(BattleMatch m, int2 cell)
        {
            Assert.IsTrue(m.Apply(Command.DebugSpawnEnemy(0, cell)).Accepted);
            var units = m.World.Units;
            return units[units.Count - 1];
        }

        [Test]
        public void 모양이_반경을_정하고_음수는_존_효과가_없다()
        {
            Assert.AreEqual(0, HazardShapeMath.RadiusTiles(HazardShapeKind.SingleCell, 5));
            Assert.AreEqual(1, HazardShapeMath.RadiusTiles(HazardShapeKind.Square3x3, 5));
            Assert.AreEqual(3, HazardShapeMath.RadiusTiles(HazardShapeKind.RadiusSquare, 3));
            Assert.AreEqual(1, HazardShapeMath.RadiusTiles(HazardShapeKind.RadiusSquare, 0), "사각은 최소 1");
            Assert.AreEqual(HazardShapeMath.NoZone, HazardShapeMath.RadiusTiles((HazardShapeKind)99, 1));
            Assert.Less(HazardShapeMath.NoZone, 0, "0 으로 바꾸면 한 칸 존이 전부 켜진다(F18)");

            // 음수 반경 개체는 서고 수명도 돌지만 아무에게도 안 건다.
            var def = Def(new HazardDef { Id = "bad", Shape = 99, Lifetime = 5f, Effects = new[] { Slow(0.5f, 1f) } });
            var m = CoreMatchFixtures.BeginBattle(def);
            var grunt = SpawnGrunt(m, new int2(5, 2));
            Assert.IsTrue(m.Apply(Command.DebugSpawnHazard(0, new int2(5, 2))).Accepted);
            m.Tick();
            Assert.AreEqual(1, m.World.Hazards.Count);
            Assert.IsFalse(grunt.Modifiers.Any);
        }

        [Test]
        public void 판정은_칸_반폭_자다_깐_자의_몸은_안_붙는다()
        {
            // 한 칸 존(반경 0) — 도달 = 0 + 칸 반폭 0.5 + 피해자 몸 0.25 = 0.75.
            var def = Def(Zone(HazardShapeKind.SingleCell, 10f, Slow(0.5f, 1f)));
            var m = CoreMatchFixtures.BeginBattle(def);
            var inside = SpawnGrunt(m, new int2(5, 2));
            var outside = SpawnGrunt(m, new int2(5, 2));
            outside.Position = inside.Position + new float3(0.8f, 0f, 0f);
            inside.Position += new float3(0.7f, 0f, 0f);
            m.Apply(Command.DebugSpawnHazard(0, new int2(5, 2)));
            m.Tick();

            Assert.AreEqual(0.5f, inside.Modifiers.Effective.MoveSpeedMul, 1e-5f, "0.7 < 0.75 — 안");
            Assert.AreEqual(1f, outside.Modifiers.Effective.MoveSpeedMul, 1e-5f, "0.8 > 0.75 — 밖");
        }

        [Test]
        public void 감속은_군중_제어가_아니라_이동속도_모디파이어다()
        {
            var def = Def(Zone(HazardShapeKind.Square3x3, 10f, Slow(0.5f, 0.2f)));
            var m = CoreMatchFixtures.BeginBattle(def);
            var grunt = SpawnGrunt(m, new int2(5, 2));
            m.Apply(Command.DebugSpawnHazard(0, new int2(5, 2)));
            m.Tick();

            Assert.IsFalse(grunt.Cc.Any, "행동 불능 슬롯이 아니다");
            Assert.AreEqual(1, grunt.Modifiers.Count);
            var slot = grunt.Modifiers.Slots[0];
            Assert.AreEqual(StatKind.MoveSpeedMul, slot.Key.Stat);
            Assert.AreEqual(CombineOp.Multiplicative, slot.Key.Op);
            Assert.AreEqual(ModifierOrigin.Zone, slot.Origin);
        }

        [Test]
        public void 나가면_restDuration_뒤에_꺼진다()
        {
            // F17 — 0.2초는 「나가면 0.2초 뒤」다. 위에 있는 동안은 계속 갱신된다.
            const float rest = 0.2f;
            var def = Def(Zone(HazardShapeKind.SingleCell, 30f, Slow(0.5f, rest)));
            var m = CoreMatchFixtures.BeginBattle(def);
            var grunt = SpawnGrunt(m, new int2(5, 2));
            m.Apply(Command.DebugSpawnHazard(0, new int2(5, 2)));

            CoreCombatFixtures.Tick(m, 60);   // 1초 — 총 지속으로 읽었다면 이미 꺼졌다
            Assert.AreEqual(0.5f, grunt.Modifiers.Effective.MoveSpeedMul, 1e-5f, "서 있는 동안은 계속 걸려 있다");

            grunt.Position += new float3(3f, 0f, 0f);   // 장판 밖으로
            int restTicks = (int)math.round(rest / Dt);
            CoreCombatFixtures.Tick(m, restTicks - 2);
            Assert.AreEqual(0.5f, grunt.Modifiers.Effective.MoveSpeedMul, 1e-5f, "나간 직후엔 아직 걸려 있다");
            CoreCombatFixtures.Tick(m, 4);
            Assert.AreEqual(1f, grunt.Modifiers.Effective.MoveSpeedMul, 1e-5f, "여유가 지나면 원속도");
        }

        [Test]
        public void 증상_장판_위의_적은_초당_저작값만큼_타고_나가면_멈춘다()
        {
            const float dps = 10f;
            var def = Def(Zone(HazardShapeKind.SingleCell, 30f, Burn(dps, 0.2f)));
            var m = CoreMatchFixtures.BeginBattle(def);
            var grunt = SpawnGrunt(m, new int2(5, 2));
            float start = grunt.Health;
            m.Apply(Command.DebugSpawnHazard(0, new int2(5, 2)));

            CoreCombatFixtures.Tick(m, 120);   // 2초
            Assert.AreEqual(start - dps * 2f, grunt.Health, 0.5f, "초당 저작값만큼 준다");

            grunt.Position += new float3(3f, 0f, 0f);
            CoreCombatFixtures.Tick(m, 30);    // 여유 0.2초를 넘긴다
            float after = grunt.Health;
            CoreCombatFixtures.Tick(m, 60);
            Assert.AreEqual(after, grunt.Health, 1e-4f, "나가면 멈춘다");
        }

        [Test]
        public void 지속_피해의_출처는_언제나_장판이다()
        {
            var def = Def(Zone(HazardShapeKind.SingleCell, 30f, Burn(5f, 0.2f)));
            var m = CoreMatchFixtures.BeginBattle(def);
            var grunt = SpawnGrunt(m, new int2(5, 2));
            m.Apply(Command.DebugSpawnHazard(0, new int2(5, 2)));
            m.Tick();
            Assert.AreEqual(1, grunt.Dot.Count);
            Assert.AreEqual(DotOrigin.Zone, grunt.Dot.Slots[0].Origin, "F16");
            Assert.AreEqual(DotElement.Fire, grunt.Dot.Slots[0].Element);
        }

        [Test]
        public void 진영은_저작_축이고_오늘의_저작은_옛_게이트와_같다()
        {
            // 오늘의 저작(적만) — 방어유닛은 안 맞는다(옛 하드 게이트와 같은 결과).
            var def = Def(Zone(HazardShapeKind.Square3x3, 10f, Slow(0.5f, 1f)));
            var m = CoreMatchFixtures.BeginBattle(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(5, 2)));
            var defender = CoreCombatFixtures.First(m, UnitKind.Defender);
            var grunt = SpawnGrunt(m, new int2(5, 1));
            m.Apply(Command.DebugSpawnHazard(0, new int2(5, 2)));
            m.Tick();
            Assert.IsFalse(defender.Modifiers.Any, "적 전용 저작 — 방어유닛 무영향");
            Assert.IsTrue(grunt.Modifiers.Any);

            // 축을 연 저작 — 아군 대상 장판이 **데이터로** 선다(제약 8).
            var def2 = Def(Zone(HazardShapeKind.Square3x3, 10f, Slow(0.5f, 1f, (int)Faction.DefenderUnit)));
            var m2 = CoreMatchFixtures.BeginBattle(def2);
            m2.Apply(Command.DebugSpawnDefender(0, new int2(5, 2)));
            var d2 = CoreCombatFixtures.First(m2, UnitKind.Defender);
            m2.Apply(Command.DebugSpawnHazard(0, new int2(5, 2)));
            m2.Tick();
            Assert.IsTrue(d2.Modifiers.Any, "진영 비트가 곧 대상이다");
        }

        [Test]
        public void 겹친_장판은_깐_순서와_무관하게_가장_강한_값이다()
        {
            foreach (bool strongFirst in new[] { true, false })
            {
                var weak = Zone(HazardShapeKind.Square3x3, 10f, Slow(0.8f, 1f));
                var strong = Zone(HazardShapeKind.Square3x3, 10f, Slow(0.5f, 1f));
                var def = Def(strongFirst ? new[] { strong, weak } : new[] { weak, strong });
                var m = CoreMatchFixtures.BeginBattle(def);
                var grunt = SpawnGrunt(m, new int2(5, 2));
                m.Apply(Command.DebugSpawnHazard(0, new int2(5, 2)));
                m.Apply(Command.DebugSpawnHazard(1, new int2(5, 2)));
                m.Tick();
                Assert.AreEqual(1, grunt.Modifiers.Count, "한 슬롯을 나눠 쓴다 — 곱으로 쌓이지 않는다");
                Assert.AreEqual(0.5f, grunt.Modifiers.Effective.MoveSpeedMul, 1e-5f, $"strongFirst={strongFirst}");
            }
        }

        [Test]
        public void 수명이_다하면_소멸_사건이_나고_그_틱에는_안_건다()
        {
            var def = Def(Zone(HazardShapeKind.Square3x3, 2f * Dt, Slow(0.5f, 0.5f)));
            var m = CoreMatchFixtures.BeginBattle(def);
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.HazardSpawned);
            var gone = CoreCombatFixtures.Listen(m, CoreEventKind.HazardDestroyed);
            var grunt = SpawnGrunt(m, new int2(5, 2));
            m.Apply(Command.DebugSpawnHazard(0, new int2(5, 2)));
            Assert.AreEqual(1, spawned.Count);
            Assert.AreEqual(0f, spawned[0].SiteFired.OriginBody, "자리형 — 몸이 없다(제약 13)");

            m.Tick();   // 남은 1틱 — 건다(그리고 같은 틱 끝에 한 틱만큼 깎인다)
            Assert.IsTrue(grunt.Modifiers.Any);
            Assert.AreEqual(0.5f - Dt, grunt.Modifiers.Slots[0].Remaining, 1e-5f);

            m.Tick();   // 0 — 사라지고 **안 건다**(걸었다면 여유가 0.5 − Dt 로 되돌아왔다)
            Assert.AreEqual(1, gone.Count, "계약 7 — 모든 소멸은 소멸 사건을 낸다");
            Assert.AreEqual(spawned[0].A, gone[0].A);
            Assert.AreEqual(0, m.World.Hazards.Count);
            Assert.AreEqual(0.5f - 2f * Dt, grunt.Modifiers.Slots[0].Remaining, 1e-5f, "마지막 틱에는 갱신이 없다");
        }

        [Test]
        public void 대상_통행층은_런타임_스냅샷이고_0은_필터_없음이다()
        {
            // F15 — 저작은 0(= 필터 없음). 까는 자가 층을 덮어쓰면 그 층만 맞는다.
            var def = Def(Zone(HazardShapeKind.Square3x3, 10f, Slow(0.5f, 1f)));
            var m = CoreMatchFixtures.BeginBattle(def);
            var grunt = SpawnGrunt(m, new int2(5, 2));
            var h = HazardSpawn.Spawn(m.World, m.Map, def, 0, new int2(5, 2), SimEntityId.None,
                                      Faction.DefenderUnit, targetLayers: 0x80, tick: 0);
            Assert.NotNull(h);
            m.Tick();
            Assert.IsFalse(grunt.Modifiers.Any, "다른 층만 거르는 장판은 안 먹는다");
            h.TargetLayers = 0;
            m.Tick();
            Assert.IsTrue(grunt.Modifiers.Any, "0 = 필터 없음");
        }

        [Test]
        public void 결정론_같은_입력은_같은_지문이다()
        {
            ulong Run()
            {
                var def = Def(Zone(HazardShapeKind.Square3x3, 1.5f, Slow(0.5f, 0.2f), Burn(8f, 0.2f)),
                              Zone(HazardShapeKind.SingleCell, 3f, Burn(4f, 0.5f)));
                def.Enemies[0].MoveSpeed = 1.5f;
                var m = CoreMatchFixtures.BeginBattle(def);
                m.Apply(Command.DebugSpawnEnemyInLane(0, 0));
                m.Apply(Command.DebugSpawnEnemyInLane(0, 1));
                m.Apply(Command.DebugSpawnHazard(0, new int2(2, 1)));
                m.Apply(Command.DebugSpawnHazard(1, new int2(3, 3)));
                CoreCombatFixtures.Tick(m, 240);
                return m.World.StateHash();
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void 디버그_커맨드_둘이_하네스에서_예약만으로_선다()
        {
            // 메뉴(6c)도 이 커맨드를 낸다 — 하네스·리플레이가 같은 길을 탄다(tools.md 원칙).
            var def = Def(Zone(HazardShapeKind.Square3x3, 0.25f, Slow(0.5f, 0.2f)));
            var row = BlockingHazardDef.Default();
            row.Id = "fixture_blocker";
            row.MaxHealth = 30f;
            row.DecayPerSec = 60f;
            def.BlockingHazards = new[] { row };
            def.ConfigHash = def.ComputeConfigHash();

            var schedule = new CommandSchedule()
                .Add(0, Command.FinishPlacement())
                .Add(1, Command.DebugSpawnHazard(0, new int2(5, 2)))
                .Add(2, Command.DebugSpawnBlocker(0, new int2(5, 0)));

            var a = CoreHarness.Run(def, schedule, 60, "board-debug");
            foreach (var r in a.Receipts) Assert.IsTrue(r.Accepted, r.Reason.ToString());

            int spawned = 0, destroyed = 0, blockerUp = 0, blockerDown = 0;
            foreach (var e in a.Trace.events)
            {
                if (e.channel == CoreTraceChannel.HazardSpawned) spawned++;
                if (e.channel == CoreTraceChannel.HazardDestroyed) destroyed++;
                if (e.channel == CoreTraceChannel.UnitSpawned && e.i == (int)UnitKind.BlockingHazard) blockerUp++;
                if (e.channel == CoreTraceChannel.UnitDestroyed && e.i == (int)UnitKind.BlockingHazard) blockerDown++;
            }
            Assert.AreEqual(1, spawned, "장판이 선다 — 트레이스가 증언한다");
            Assert.AreEqual(1, destroyed, "0.25초 수명이 끝난다");
            Assert.AreEqual(1, blockerUp, "길막이 선다");
            Assert.AreEqual(1, blockerDown, "30 ÷ 60 = 0.5초 뒤 부서진다");

            var b = CoreHarness.Run(def, schedule, 60, "board-debug");
            Assert.IsNull(a.Trace.DiffAgainst(b.Trace), "결정론 — 두 실행의 트레이스가 같다");
        }
    }
}
