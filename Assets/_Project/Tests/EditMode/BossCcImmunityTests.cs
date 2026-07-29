using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // boss-jjangssen unit 3 — 보스 CC 면역 술어 고정.
    //
    // 이 술어는 부여 지점 3곳(`CcApplySystem` 드레인 · `EffectSpawner.ApplyCc` ·
    // `AttackSystem` 넉업)이 공유하므로 한 곳에서 갈라지면 조용히 비대칭이 된다.
    // 그리고 `CcKind` 는 append-only 로 자라는 enum이라, 새 값이 추가될 때 사람이
    // "이것도 면역인가" 를 결정하도록 강제하는 것이 이 파일의 목적이다.
    //
    // 그래서 기대값을 **리터럴 집합으로 고정**한다. `IsLock(kind) || Impulse` 로 쓰면
    // 구현식 재작성 = 항진이 되어 어떤 새 kind 도 조용히 통과한다.
    public class BossCcImmunityTests
    {
        // 사용자 승인 범위(2026-07-29) = 직접 걸리는 행동정지(스턴·수면) + 넉백.
        private static readonly HashSet<CcKind> ApprovedImmune =
            new() { CcKind.Stun, CcKind.Sleep, CcKind.Impulse };

        [Test]
        public void DirectSourceImmunityMatchesApprovedScope()
        {
            foreach (CcKind kind in Enum.GetValues(typeof(CcKind)))
                Assert.AreEqual(
                    ApprovedImmune.Contains(kind),
                    CcActionLock.IsBossImmune(kind, CcSource.Direct),
                    $"{kind}: 직접 출처 면역 여부가 승인 범위와 어긋났다. " +
                    "새 CcKind 를 추가했다면 ApprovedImmune 을 사람이 결정해 갱신하라.");
        }

        // 규칙: "누적해서 임계를 넘긴 CC 는 통한다." Ice 5스택 스턴이 보스에게 통하는 근거이고,
        // Bleed(→DoT)가 보스 HP 를 깎는 근거다. kind 를 손으로 나열하지 않는다 — 6번째 값이
        // 추가될 때 커버리지가 조용히 비는 것을 막는다.
        [Test]
        public void StackThresholdSourceAlwaysPassesThrough()
        {
            foreach (CcKind kind in Enum.GetValues(typeof(CcKind)))
                Assert.IsFalse(
                    CcActionLock.IsBossImmune(kind, CcSource.StackThreshold),
                    $"{kind}: 스택 임계 출처는 kind 불문 통과해야 한다");
        }

        // 기본값 계약: source 를 채우지 않은 생산자는 "직접" 으로 취급돼야 한다.
        // 이게 깨지면 기존 CC 생산자 전부가 조용히 보스에게 통해버린다.
        [Test]
        public void DefaultSourceIsDirect()
        {
            Assert.AreEqual(CcSource.Direct, default(CcSource));
            Assert.AreEqual(CcSource.Direct, new EnemyCcEvent().source);
        }
    }
}
