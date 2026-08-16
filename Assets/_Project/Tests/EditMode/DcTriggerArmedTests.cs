using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Battle.Combat;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // on-place-skill-rework unit 0 — **진영별 트리거 화이트리스트의 전수 고정.**
    //
    // 이 두 술어는 "조용한 no-op 금지"의 문지기다: 여기를 통과하지 못한 트리거는 슬롯 자체가
    // 만들어지지 않고 bake 가 loud 하게 거절한다. 그래서 목록이 조용히 넓어지는 것이 곧 사고다.
    //
    // 특히 `EnemyTriggerArmed` 는 **자기진영 타격을 막는 유일한 문**이다(DcTrigger.cs 주석:
    // 이 줄을 완화하면 보스의 파열 폭발이 자기 진영을 때린다). 방어유닛 전용 트리거를 그
    // 목록에 섞지 않는다는 것이 unit 0 의 계약이며, 이 테스트가 그것을 고정한다.
    public class DcTriggerArmedTests
    {
        private static IEnumerable<DcTriggerKind> AllKinds()
            => (DcTriggerKind[])Enum.GetValues(typeof(DcTriggerKind));

        // 적 bake 가 여는 문 — boss-mamemo 리뷰 M3 이후 3종 고정.
        [Test]
        public void EnemyArmed_IsExactlyThreeKinds()
        {
            var armed = new List<DcTriggerKind>();
            foreach (var k in AllKinds())
                if (DcTrigger.EnemyTriggerArmed(k)) armed.Add(k);

            CollectionAssert.AreEquivalent(
                new[] { DcTriggerKind.PeriodicTimer, DcTriggerKind.HealthThreshold, DcTriggerKind.AttackN },
                armed,
                "적 화이트리스트가 바뀌었다. 넓히면 보스의 자기진영 타격 경로가 열린다 — " +
                "DcTrigger.cs 의 경고를 읽고, 정말 필요하면 근거와 함께 이 테스트를 갱신하라.");
        }

        // 방어유닛 bake 가 여는 문 — v1 은 배치 하나.
        [Test]
        public void DefenderArmed_IsExactlyOnPlace()
        {
            var armed = new List<DcTriggerKind>();
            foreach (var k in AllKinds())
                if (DcTrigger.DefenderTriggerArmed(k)) armed.Add(k);

            CollectionAssert.AreEquivalent(new[] { DcTriggerKind.OnPlace }, armed,
                "방어유닛 화이트리스트가 바뀌었다. 레거시 OnPlaceEffectType 이관은 payload 어휘를 " +
                "늘리는 작업이지 트리거를 늘리는 작업이 아니다.");
        }

        // 두 목록이 겹치면 «어느 쪽 근거로 완화해도 되는지» 가 흐려진다 — 분해한 이유 그 자체.
        [Test]
        public void EnemyAndDefenderWhitelists_AreDisjoint()
        {
            foreach (var k in AllKinds())
                Assert.IsFalse(
                    DcTrigger.EnemyTriggerArmed(k) && DcTrigger.DefenderTriggerArmed(k),
                    $"'{k}' 가 두 화이트리스트에 동시에 들어 있다. 두 술어는 서로 다른 질문에 " +
                    "답하므로 겹치면 안 된다(적: 자기진영 타격 방지 / 방어유닛: 배관 존재).");
        }

        // 배치 트리거는 **적에게 열리지 않는다.** 적은 배치되지 않으므로 슬롯이 생겨도
        // JustDeployed 가 영영 안 붙어 조용한 no-op 이 된다.
        [Test]
        public void OnPlace_IsNotArmedForEnemies()
        {
            Assert.IsFalse(DcTrigger.EnemyTriggerArmed(DcTriggerKind.OnPlace));
        }

        // 퇴근은 방어유닛 사건이지만 **카드 전용**이다 — 유닛 자기 규칙으로는 아직 못 연다.
        // 여는 순간 브리지 RetireDefender 경로와 UnitSkillAbility bake 를 함께 배선해야 한다.
        [Test]
        public void OnRetire_IsNotArmedForUnitOwnRules()
        {
            Assert.IsFalse(DcTrigger.DefenderTriggerArmed(DcTriggerKind.OnRetire));
        }
    }
}
