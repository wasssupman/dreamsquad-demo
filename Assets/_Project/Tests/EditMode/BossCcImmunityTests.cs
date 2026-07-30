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
        public void ImmunityMatchesApprovedScope()
        {
            foreach (CcKind kind in Enum.GetValues(typeof(CcKind)))
                Assert.AreEqual(
                    ApprovedImmune.Contains(kind),
                    CcActionLock.IsBossImmune(kind),
                    $"{kind}: 면역 여부가 승인 범위와 어긋났다. " +
                    "새 CcKind 를 추가했다면 ApprovedImmune 을 사람이 결정해 갱신하라.");
        }

        // unit 8 — 출처 축 은퇴. 예전에는 스택 임계가 유발한 CC 가 kind 불문 통과했고
        // (Ice 5중첩 스턴이 보스를 멈추는 근거였다), 그 예외의 이유는 "스택 DoT 가 CC 버퍼를
        // 공유한다" 였다. dot-effect-extraction 이 DoT 를 전용 채널로 빼면서 이유가 사라졌으므로
        // 스턴은 출처와 무관하게 막힌다. 이 테스트는 그 계약이 되돌려지지 않게 고정한다.
        [Test]
        public void StackThresholdStunIsAlsoImmune()
        {
            Assert.IsTrue(CcActionLock.IsBossImmune(CcKind.Stun),
                "스택 임계가 만든 스턴도 보스에게는 막혀야 한다 — 술어에 출처 축을 되살리지 말 것");
        }

        // 스택 카드가 보스전에서 통째로 죽는 것은 아니다: 감속은 StatModifier(MoveSpeedMul),
        // 지속 피해는 DotApplyEvents 라 둘 다 이 술어를 지나지 않는다. CcKind 에 남아 있는
        // Slow/DoT 토큰이 면역 목록에 섞여 들어가면 그 계약이 깨진다.
        [Test]
        public void SlowAndDotTokensAreNotImmune()
        {
            Assert.IsFalse(CcActionLock.IsBossImmune(CcKind.Slow));
            Assert.IsFalse(CcActionLock.IsBossImmune(CcKind.DoT));
        }
    }
}
