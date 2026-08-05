using System;
using NUnit.Framework;
using Wassup.Core.Session;
using Wassup.Sim.Match;

namespace Wassup.Tests.EditMode
{
    // battle-sim-extraction unit 12 — 세션 계약 타입의 회귀 핀.
    //
    // 어댑터의 커맨드 번역은 BattleBridge 인스턴스를 요구하므로 PlayMode 영역이고 unit 13 과 함께
    // 온다. 여기서는 **순수하게 고정 가능한 것**만 단정한다 — 특히 semantic/projection 분류는
    // enum 순서에 의존하므로(IsPresentation 이 범위 비교) 재정렬 시 조용히 깨진다.
    public class MatchSessionContractTests
    {
        [Test]
        public void CommandFactories_SetKindAndPayload()
        {
            var deploy = MatchCommand.DeployDefender(1, new SimCell(3, 4), "archer");
            Assert.AreEqual(CommandKind.DeployDefender, deploy.Kind);
            Assert.AreEqual(3, deploy.Cell.X);
            Assert.AreEqual(4, deploy.Cell.Y);
            Assert.AreEqual("archer", deploy.UnitDefId);
            Assert.AreEqual(1u, deploy.ClientSeq);

            var facing = MatchCommand.SetDeployFacing(2, 77, new SimCell(0, 1));
            Assert.AreEqual(CommandKind.SetDeployFacing, facing.Kind);
            Assert.AreEqual(77, facing.TargetSimId);
            Assert.AreEqual(1, facing.Facing.Y);

            var wave = MatchCommand.ForceNextWave(3);
            Assert.AreEqual(CommandKind.ForceNextWave, wave.Kind);

            var pause = MatchCommand.SetPaused(4, true);
            Assert.AreEqual(CommandKind.SetPaused, pause.Kind);
            Assert.IsTrue(pause.Flag);
        }

        [Test]
        public void Relocate_PutsDestinationInCell_AndSourceInCell2()
        {
            // 규약: 대상 셀은 항상 Cell(Deploy 와 일치), 출발은 Cell2. 뒤집히면 재배치가
            // 원위치로 되돌아가는 조용한 버그가 된다.
            var relocate = MatchCommand.RelocateDefender(1, from: new SimCell(1, 1), to: new SimCell(5, 6));
            Assert.AreEqual(5, relocate.Cell.X, "Cell = 도착");
            Assert.AreEqual(6, relocate.Cell.Y);
            Assert.AreEqual(1, relocate.Cell2.X, "Cell2 = 출발");
            Assert.AreEqual(1, relocate.Cell2.Y);
        }

        [Test]
        public void CardVariants_CarryTheirOwnTarget()
        {
            Assert.AreEqual(CardVariant.Attach, MatchCommand.PlayCardAttach(1, 9, 42).Variant);
            Assert.AreEqual(42, MatchCommand.PlayCardAttach(1, 9, 42).TargetSimId);
            Assert.AreEqual(9, MatchCommand.PlayCardAttach(1, 9, 42).CardHandle);

            Assert.AreEqual(CardVariant.MarkEnemy, MatchCommand.PlayCardMarkEnemy(1, 9, 7).Variant);
            Assert.AreEqual(CardVariant.ActiveTile, MatchCommand.PlayCardActiveTile(1, 9, new SimCell(2, 2)).Variant);

            var portal = MatchCommand.PlayCardActivePortal(1, 9, new SimCell(1, 2), new SimCell(3, 4));
            Assert.AreEqual(CardVariant.ActivePortal, portal.Variant);
            Assert.AreEqual(1, portal.Cell.X, "entry = Cell");
            Assert.AreEqual(3, portal.Cell2.X, "exit = Cell2");
        }

        [Test]
        public void Receipt_Ok_And_Rejected_HaveDisjointShapes()
        {
            var ok = CommandReceipt.Ok(5, tick: 12, order: 2);
            Assert.IsTrue(ok.Accepted);
            Assert.AreEqual(CommandReject.None, ok.Reject);
            Assert.AreEqual(12, ok.AcceptedTick);
            Assert.AreEqual(2, ok.OrderInTick);

            var no = CommandReceipt.Rejected(6, CommandReject.Place_Occupied);
            Assert.IsFalse(no.Accepted);
            Assert.AreEqual(CommandReject.Place_Occupied, no.Reject);
            Assert.AreEqual(-1, no.AcceptedTick, "거절은 실행 tick 이 없다");
        }

        [Test]
        public void SessionEvent_PresentationClassification_MatchesEnumGrouping()
        {
            // projection 6종은 semantic 의 파생이므로 리플레이 정본에서 빠진다. 이 경계가
            // 틀어지면 AMR 이 연출 신호를 권위 사실로 저장한다.
            var semantic = new[]
            {
                SessionEventKind.EnemySpawned, SessionEventKind.DefenderDeployed,
                SessionEventKind.WaypointUpdate, SessionEventKind.DamageApplied,
                SessionEventKind.EnemyKilled, SessionEventKind.EnemyLeaked,
                SessionEventKind.HazardSpawned, SessionEventKind.AttackResolved,
                SessionEventKind.CardTriggered, SessionEventKind.MatchEnded,
            };
            foreach (var k in semantic)
                Assert.IsFalse(new SessionEvent(0, 0, k).IsPresentation, $"{k} 는 semantic 이다");

            var projection = new[]
            {
                SessionEventKind.VfxDamageNumber, SessionEventKind.VfxShieldGranted,
                SessionEventKind.VfxUnitAttack, SessionEventKind.VfxKnockup,
                SessionEventKind.VfxBossLeap, SessionEventKind.VfxUltimateLeap,
            };
            foreach (var k in projection)
                Assert.IsTrue(new SessionEvent(0, 0, k).IsPresentation, $"{k} 는 projection 이다");
        }

        [Test]
        public void CommandReject_CoversEveryPlacementRejectReason()
        {
            // 통합 enum 이 기존 사유를 **값 손실 없이** 흡수했는지 — PlacementRejectReason 멤버가
            // 늘면 이 테스트가 먼저 깨져서 매핑 누락을 알린다(어댑터 MapPlacement 의 짝).
            foreach (var name in Enum.GetNames(typeof(Wassup.Sim.Match.PlacementRejectReason)))
            {
                if (name == "None") continue;
                bool mapped =
                    Enum.IsDefined(typeof(CommandReject), "Place_" + name) ||
                    Enum.IsDefined(typeof(CommandReject), "Relocate_" + name);
                Assert.IsTrue(mapped, $"PlacementRejectReason.{name} 에 대응하는 CommandReject 가 없다");
            }
        }

        [Test]
        public void ReadModel_UnsupportedFlags_AreIndependent_SoViewsDoNotDrawZeros()
        {
            // 플래그 없이 0 을 흘리면 HUD 가 0 을 그린다 — 그래서 Supported* 가 계약이다.
            // unit 13-A3 이 코스트를 번역으로 채웠고(SupportedCost), 점수는 unit 14 · 게이지는
            // unit 16 이 채운다. **플래그가 분리돼 있어야** 코스트를 채운 순간 게이지 0 이
            // "지원됨"으로 거짓 신고되지 않는다.
            var rm = new MatchReadModel(
                tick: 1, battleClock: 0.5, phase: MatchPhase.Battle, timerRemaining: 10f,
                nextWaveAvailable: true, nextWaveHasNext: true, nextWaveNumber: 2, nextWaveClearReady: false,
                supportedScore: false, scoreKill: 0, goals: 0, effectiveLeakLimit: 0,
                stressAccrued: 0, stressLimit: 0,
                supportedCost: true, costCurrent: 7.9f, costMax: 20f, costCurrentInt: 7,
                supportedGauge: false, gaugeCurrent: 0, gaugeMax: 0,
                anyPlacementCooldown: true);
            // 이 단정은 **손으로 만든 스냅샷**에 대한 것이다 — 생성자에 false 를 넣었으니 false 다.
            // 어댑터가 실제로 무엇을 서빙하는지는 여기서 검증하지 않는다(unit 14 이후 어댑터는
            // `supportedScore: true` 를 낸다 — `LegacyMatchSessionAdapter.ReadModel`).
            Assert.IsFalse(rm.SupportedScore, "생성자에 넣은 값이 그대로 보존된다");
            Assert.IsTrue(rm.SupportedCost, "코스트는 A3 에서 번역으로 채워졌다");
            Assert.IsFalse(rm.SupportedGauge, "게이지는 unit 16 — 코스트와 같은 플래그를 쓰지 않는다");
            Assert.AreEqual(MatchPhase.Battle, rm.Phase);

            // 지불 판정(raw)과 표시(floor)가 다른 값이라는 계약. 한 필드로 합치면 max 근처에서
            // 판정이 1 씩 어긋난다.
            Assert.AreEqual(7.9f, rm.CostCurrent, 0.0001f);
            Assert.AreEqual(7, rm.CostCurrentInt);
            Assert.IsTrue(rm.AnyPlacementCooldown);
        }
    }
}
