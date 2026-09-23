using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 4 — 배치 판정.
    [TestFixture]
    public class MatchPlacementTests
    {
        [Test]
        public void 판정_순서는_구조가_자원보다_먼저다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 999;                       // 자원은 확실히 모자라게
            var match = CoreMatchFixtures.BeginBattle(def);

            // 판 밖 칸 — 자원이 모자라도 **공간**이 먼저 답한다. 「돈이 없다」고 답하면
            // 플레이어가 배우는 것이 틀린다.
            var outOfBounds = match.Apply(Command.PlaceDefender(0, new int2(99, 99)));
            Assert.AreEqual(RejectReason.OutOfBounds, outOfBounds.Reason);

            // 정상 칸 — 이제야 자원이 답한다.
            var poor = match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            Assert.AreEqual(RejectReason.InsufficientCost, poor.Reason);
        }

        [Test]
        public void 정의표_밖_유닛은_공간을_묻기_전에_거절된다()
        {
            var match = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            Assert.AreEqual(RejectReason.InvalidUnit,
                match.Apply(Command.PlaceDefender(9, new int2(3, 1))).Reason,
                "footprint 도 층도 모르는데 공간을 물을 수는 없다");
        }

        [Test]
        public void 다칸은_전_칸이_통과해야_한다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].FootprintWidth = 2;
            def.Units[0].FootprintHeight = 2;
            var map = def.Map;
            // 2×2 의 오른쪽 위 한 칸만 막는다. 앵커 칸은 멀쩡하다.
            CoreMapFixtures.Block(map, new int2(4, 2));
            def.ConfigHash = def.ComputeConfigHash();

            var match = CoreMatchFixtures.BeginBattle(def);
            Assert.AreEqual(RejectReason.NotBuildable,
                match.Apply(Command.PlaceDefender(0, new int2(3, 1))).Reason,
                "한 칸만 보면 건물이 벽을 파고든다");
        }

        [Test]
        public void 배치_층과_유닛_층의_교집합이_0이면_못_놓는다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].PlacementLayers = LayerBits.Air;   // 이 판에는 Air 전용 칸이 없다
            def.ConfigHash = def.ComputeConfigHash();

            var match = CoreMatchFixtures.BeginBattle(def);
            // 걷는 칸은 Ground|Path|Air 를 다 여므로(`OpenPlacement`) Air 도 통과한다.
            Assert.AreEqual(RejectReason.None,
                match.Apply(Command.PlaceDefender(0, new int2(3, 1))).Reason);

            // 막힌 칸은 Air 만 연다 — 그래서 Ground 전용 유닛이 거기 못 선다.
            var def2 = CoreMatchFixtures.Definition();
            def2.Units[0].PlacementLayers = LayerBits.Ground;
            CoreMapFixtures.Block(def2.Map, new int2(5, 1));
            def2.ConfigHash = def2.ComputeConfigHash();
            var match2 = CoreMatchFixtures.BeginBattle(def2);
            Assert.AreEqual(RejectReason.NotBuildable,
                match2.Apply(Command.PlaceDefender(0, new int2(5, 1))).Reason);
        }

        [Test]
        public void 점유는_주인과_쌍으로_바뀐다()
        {
            var match = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            Assert.IsTrue(match.Apply(Command.PlaceDefender(0, new int2(3, 1))).Accepted);

            Assert.AreEqual(RejectReason.Occupied,
                match.Apply(Command.PlaceDefender(0, new int2(3, 1))).Reason);
            Assert.AreEqual(1, match.Map.Occupancy.OwnerCount);

            // 퇴근하면 칸이 풀린다 — 쌍이 깨지면 죽은 유닛이 칸을 영영 문다.
            Assert.IsTrue(match.Apply(Command.Retire(CoreMatchFixtures.PlacedDefender(match))).Accepted);
            Assert.AreEqual(0, match.Map.Occupancy.OwnerCount);
            Assert.IsFalse(match.Map.Occupancy.IsOccupied(new int2(3, 1)));
        }

        [Test]
        public void 판_상한은_유닛_저작과_모드_상한을_둘_다_본다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].MaxOnBoard = 2;
            def.Mode.BoardCap = 0;
            def.ConfigHash = def.ComputeConfigHash();
            var match = CoreMatchFixtures.BeginBattle(def);

            Assert.IsTrue(match.Apply(Command.PlaceDefender(0, new int2(3, 1))).Accepted);
            Assert.IsTrue(match.Apply(Command.PlaceDefender(0, new int2(4, 1))).Accepted);
            Assert.AreEqual(RejectReason.LimitReached,
                match.Apply(Command.PlaceDefender(0, new int2(5, 1))).Reason);

            var def2 = CoreMatchFixtures.Definition();
            def2.Units[0].MaxOnBoard = 10;
            def2.Mode.BoardCap = 1;
            def2.ConfigHash = def2.ComputeConfigHash();
            var match2 = CoreMatchFixtures.BeginBattle(def2);
            Assert.IsTrue(match2.Apply(Command.PlaceDefender(0, new int2(3, 1))).Accepted);
            Assert.AreEqual(RejectReason.LimitReached,
                match2.Apply(Command.PlaceDefender(0, new int2(4, 1))).Reason);
        }

        [Test]
        public void 재배치_대기가_끝나기_전에는_못_놓는다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].PlacementCooldown = 1f;
            def.Units[0].MaxOnBoard = 4;
            def.ConfigHash = def.ComputeConfigHash();
            var match = CoreMatchFixtures.BeginBattle(def);

            Assert.IsTrue(match.Apply(Command.PlaceDefender(0, new int2(3, 1))).Accepted);
            Assert.AreEqual(RejectReason.OnCooldown,
                match.Apply(Command.PlaceDefender(0, new int2(4, 1))).Reason);

            for (int t = 0; t < 61; t++) match.Tick();
            Assert.IsTrue(match.Placement.IsReady(0), "1초면 풀린다");
            Assert.IsTrue(match.Apply(Command.PlaceDefender(0, new int2(4, 1))).Accepted);
        }

        [Test]
        public void 퇴근_대기는_사망_대기의_비율이고_뒤집힐_수_없다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].DeathCooldown = 10f;
            def.Units[0].RetireCooldownRatio = 5f;   // 저작 실수(시트는 Range 를 안 탄다)
            def.ConfigHash = def.ComputeConfigHash();

            Assert.AreEqual(10f, def.Units[0].EffectiveRetireCooldown, 1e-4f,
                "Clamp01 이 진짜 방어선이다 — 퇴근이 사망보다 길어질 방법이 없어야 한다");

            def.Units[0].RetireCooldownRatio = 0.4f;
            Assert.AreEqual(4f, def.Units[0].EffectiveRetireCooldown, 1e-4f);
        }

        [Test]
        public void 퇴근은_사망이_아니다()
        {
            var match = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            var slain = CoreMatchFixtures.Listen(match, CoreEventKind.UnitSlain);
            var retired = CoreMatchFixtures.Listen(match, CoreEventKind.Retired);
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));

            Assert.IsTrue(match.Apply(Command.Retire(CoreMatchFixtures.PlacedDefender(match))).Accepted);

            Assert.AreEqual(1, retired.Count);
            Assert.AreEqual(0, slain.Count,
                "`Dead` 를 안 켜므로 사직서·작별 선물·각성이 배제 코드 0 으로 안 일어난다");
            Assert.AreEqual(4f, match.Placement.CooldownRemaining(0), 1e-3f, "퇴근 대기 = 10 × 0.4");
        }

        [Test]
        public void 퇴근이_꺼진_모드에서는_퇴근이_안_된다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.RetireEnabled = false;
            def.ConfigHash = def.ComputeConfigHash();
            var match = CoreMatchFixtures.BeginBattle(def);
            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));

            Assert.IsFalse(match.Apply(Command.Retire(CoreMatchFixtures.PlacedDefender(match))).Accepted);
        }

        [Test]
        public void 배치_모션이_있으면_착지_뒤에_활성화된다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].DeployMotionSeconds = 1f;
            def.ConfigHash = def.ComputeConfigHash();
            var match = CoreMatchFixtures.BeginBattle(def);
            var activated = CoreMatchFixtures.Listen(match, CoreEventKind.DefenderActivated);

            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            var u = match.World.Find(CoreMatchFixtures.PlacedDefender(match));
            Assert.IsTrue(u.Deploying, "배치 중에는 표적도 사냥판 소스도 아니다");
            Assert.AreEqual(0, activated.Count);

            // 뷰가 착지를 알리면 **그 순간부터** 모션 길이를 다시 잰다.
            for (int t = 0; t < 30; t++) match.Tick();
            Assert.IsTrue(match.Apply(Command.LandDefender(u.Id)).Accepted);
            for (int t = 0; t < 59; t++) match.Tick();
            Assert.IsTrue(u.Deploying, "착지 기준 1초가 아직 안 찼다");
            match.Tick();
            Assert.IsFalse(u.Deploying);
            Assert.AreEqual(1, activated.Count);
        }

        [Test]
        public void 배치_모션이_0이면_페이즈_자체가_없다()
        {
            var match = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            var activated = CoreMatchFixtures.Listen(match, CoreEventKind.DefenderActivated);

            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));

            Assert.IsFalse(match.World.Find(CoreMatchFixtures.PlacedDefender(match)).Deploying,
                "한 틱짜리 대기로 흉내 내면 그 틱 동안 「놓았는데 유령」이 된다");
            Assert.AreEqual(1, activated.Count);
        }

        [Test]
        public void 배치_중에_죽으면_활성화도_배치_스킬도_없다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].DeployMotionSeconds = 2f;
            def.ConfigHash = def.ComputeConfigHash();
            var match = CoreMatchFixtures.BeginBattle(def);
            var activated = CoreMatchFixtures.Listen(match, CoreEventKind.DefenderActivated);

            match.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            match.Apply(Command.DebugDestroy(CoreMatchFixtures.PlacedDefender(match)));
            for (int t = 0; t < 200; t++) match.Tick();

            Assert.AreEqual(0, activated.Count, "시체는 배치되지 않는다(E5)");
            Assert.AreEqual(0, match.Placement.PendingActivations);
        }

        [Test]
        public void 배치_입력이_꺼진_창에서는_거절된다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.PlacementInputEnabled = false;
            def.Mode.PlacementSeconds = 3f;          // 카운트다운 변형
            def.ConfigHash = def.ComputeConfigHash();

            var match = new BattleMatch(def);
            match.Begin();
            Assert.AreEqual(MatchPhase.Placement, match.Clock.Phase);
            Assert.AreEqual(RejectReason.NotRunningOrPlacementClosed,
                match.Apply(Command.PlaceDefender(0, new int2(3, 1))).Reason);

            for (int t = 0; t < 180; t++) match.Tick();
            Assert.AreEqual(MatchPhase.Battle, match.Clock.Phase, "3초 뒤 자동으로 닫힌다");
            Assert.IsTrue(match.Apply(Command.PlaceDefender(0, new int2(3, 1))).Accepted);
        }

        [Test]
        public void 배치_창은_길이가_0이어도_열림_신호를_낸다()
        {
            var def = CoreMatchFixtures.Definition();   // 입력 on · 길이 0
            var phases = new System.Collections.Generic.List<CoreEvent>();
            var match = new BattleMatch(def);
            match.Bus.Subscribe(CoreEventKind.PlacementPhaseChanged, 0, phases.Add);
            match.Begin();

            Assert.AreEqual(1, phases.Count, "이 신호가 트레이를 만든다 — 없으면 전투 내내 빈다");
            Assert.AreEqual(1, phases[0].Arg);

            match.Apply(Command.FinishPlacement());
            Assert.AreEqual(2, phases.Count);
            Assert.AreEqual(0, phases[1].Arg);

            Assert.IsFalse(match.Apply(Command.FinishPlacement()).Accepted,
                "같은 국면으로 다시 들어가는 것은 무시한다(G1)");
        }

        [Test]
        public void 배치_페이즈가_없는_모드는_전투로_시작한다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.PlacementInputEnabled = false;
            def.Mode.PlacementSeconds = 0f;
            def.ConfigHash = def.ComputeConfigHash();

            var phases = new System.Collections.Generic.List<CoreEvent>();
            var match = new BattleMatch(def);
            match.Bus.Subscribe(CoreEventKind.PlacementPhaseChanged, 0, phases.Add);
            match.Begin();

            Assert.AreEqual(MatchPhase.Battle, match.Clock.Phase);
            Assert.AreEqual(0, phases.Count);
        }

        [Test]
        public void 효과_타일은_한_번만_소비된다()
        {
            var def = CoreMatchFixtures.Definition();
            def.EffectTileCount = 3;
            def.ConfigHash = def.ComputeConfigHash();
            var match = CoreMatchFixtures.BeginBattle(def);

            int armed = match.Placement.ArmedEffectTiles.Count;
            Assert.AreEqual(3, armed);

            var cell = match.Placement.ArmedEffectTiles[0];
            Assert.IsTrue(match.Apply(Command.PlaceDefender(0, cell)).Accepted);
            Assert.AreEqual(2, match.Placement.ArmedEffectTiles.Count, "회수도 재무장도 없다");

            // 같은 자리에 다시 놓아도 재무장되지 않는다.
            match.Apply(Command.Retire(CoreMatchFixtures.PlacedDefender(match)));
            for (int t = 0; t < 300; t++) match.Tick();
            match.Apply(Command.PlaceDefender(0, cell));
            Assert.AreEqual(2, match.Placement.ArmedEffectTiles.Count);
        }
    }
}
