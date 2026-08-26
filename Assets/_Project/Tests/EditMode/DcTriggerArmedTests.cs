using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Battle.Combat;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // skill-layer-migration unit 8 — **문지기가 바뀌었으므로 그물도 바뀐다.**
    //
    // 예전 이 파일은 진영별 화이트리스트 2종을 전수 고정했고, 그 근거는 「적에게
    // OnShieldBreak 를 열면 보스가 자기 편을 때린다」였다. 그 위험은 실행이 스킬
    // 레이어로 가면서 사라졌다 — concrete 는 진영을 모르고, 호출자가 곧 소유자다.
    //
    // 그래서 **테스트를 지우는 게 아니라 질문을 바꾼다.** 남은 불변식은 하나다:
    // 「감지자가 없는 조합은 슬롯을 만들지 않는다」. 그것을 어기면 bake 가 조용히
    // 성공하고 아무도 그 트리거를 안 잡는다 — 화이트리스트가 존재한 이유의 나머지 절반.
    public class DcTriggerArmedTests
    {
        private static IEnumerable<DcTriggerKind> AllKinds()
            => (DcTriggerKind[])Enum.GetValues(typeof(DcTriggerKind));

        // 진영에 상관없이 열린 여섯 — 감지 시스템이 진영을 안 보는 것들이다.
        // (주기·경계·N번째 공격·피격 N회·처치·실드 파열)
        [Test]
        public void FactionAgnosticTriggers_AreOpenToBothSides()
        {
            var both = new[]
            {
                DcTriggerKind.PeriodicTimer, DcTriggerKind.HealthThreshold, DcTriggerKind.AttackN,
                DcTriggerKind.OnDamagedN, DcTriggerKind.OnKill, DcTriggerKind.OnShieldBreak,
            };
            foreach (var k in both)
            {
                Assert.IsTrue(DcTrigger.HasDetector(k, hostIsEnemy: true), $"{k} 가 적에게 닫혔다");
                Assert.IsTrue(DcTrigger.HasDetector(k, hostIsEnemy: false), $"{k} 가 방어유닛에 닫혔다");
            }
        }

        // ⚠ **이 셋이 이번에 열린 문이다.** 예전 화이트리스트는 자기진영 타격을 막으려고
        // 적 쪽을 잠갔다. 특히 `OnShieldBreak` 가 그 경고의 주인공이었다.
        [Test]
        public void ShieldBreakAndKill_AreNowOpenToEnemies()
        {
            Assert.IsTrue(DcTrigger.HasDetector(DcTriggerKind.OnShieldBreak, hostIsEnemy: true),
                "실드 파열이 적에게 열려야 한다 — 이것이 unit 8 이 연 문이고, " +
                "안전은 이제 concrete 의 진영 무지(無知)와 CasterFaction 스냅샷이 지킨다.");
            Assert.IsTrue(DcTrigger.HasDetector(DcTriggerKind.OnKill, hostIsEnemy: true));
            Assert.IsTrue(DcTrigger.HasDetector(DcTriggerKind.OnDamagedN, hostIsEnemy: true));
        }

        // 배치·퇴근은 **본질상** 적에게 없는 사건이다. 열면 슬롯만 생기고 JustDeployed 가
        // 영영 안 붙어 조용한 no-op 이 된다.
        [Test]
        public void PlacementEvents_StayClosedForEnemies()
        {
            Assert.IsFalse(DcTrigger.HasDetector(DcTriggerKind.OnPlace, hostIsEnemy: true));
            Assert.IsFalse(DcTrigger.HasDetector(DcTriggerKind.OnRetire, hostIsEnemy: true));
            Assert.IsTrue(DcTrigger.HasDetector(DcTriggerKind.OnPlace, hostIsEnemy: false));
        }

        // ⚠ **유일하게 남은 진짜 공백.** 자기 죽음 감지자(`UnitLifecycleSystem`)의 쿼리가
        // `WithAll<DeadTag, DefenderUnitTag>` 라 적의 작별 선물은 아직 아무도 안 잡는다.
        // 여는 것은 새 기능이라 별도 결정이고, 여는 사람은 그 생산자의 `CasterFaction`
        // 리터럴도 같이 고쳐야 한다 — 안 그러면 적이 자기 진영을 때린다.
        [Test]
        public void OnDeath_StaysClosedForEnemies_UntilTheDetectorWidens()
        {
            Assert.IsFalse(DcTrigger.HasDetector(DcTriggerKind.OnDeath, hostIsEnemy: true),
                "적 OnDeath 를 열려면 UnitLifecycleSystem 의 쿼리를 먼저 넓혀야 한다. " +
                "술어만 열면 슬롯이 생기고 아무도 안 잡는다.");
            Assert.IsTrue(DcTrigger.HasDetector(DcTriggerKind.OnDeath, hostIsEnemy: false));
        }

        // fail-closed — 배선 안 된 kind 는 닫혀 있다. 새 kind 를 더하면 여기서 걸린다.
        [Test]
        public void UnwiredKinds_AreClosed()
        {
            Assert.IsFalse(DcTrigger.HasDetector(DcTriggerKind.None, hostIsEnemy: true));
            Assert.IsFalse(DcTrigger.HasDetector(DcTriggerKind.None, hostIsEnemy: false));
        }

        // 전역성 — 모든 kind 에 대해 예외 없이 답이 나온다(빠뜨린 분기가 없다).
        [Test]
        public void HasDetector_IsTotalOverAllKinds()
        {
            foreach (var k in AllKinds())
            {
                Assert.DoesNotThrow(() => DcTrigger.HasDetector(k, true), $"{k}");
                Assert.DoesNotThrow(() => DcTrigger.HasDetector(k, false), $"{k}");
            }
        }
    }
}
