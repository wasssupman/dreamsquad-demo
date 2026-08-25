using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // on-place-skill-rework unit 5 — 말파이트 배치 스킬의 **저작 계약**.
    //
    // 원래 PlayMode 파일에 `[Test]` 로 있었는데, 그 어셈블리에서 씬 로드 없이
    // `Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0]` 을 인덱싱해 **단독 실행 시
    // IndexOutOfRange** 였다(같은 클래스의 [UnityTest] 가 먼저 씬을 로드해 준 덕에 우연히
    // 통과했다 = 테스트 순서 의존). 씬이 필요 없는 순수 저작 검증이므로 EditMode 로 옮기고
    // `AssetDatabase` 로 직접 읽는다.
    public class MalphiteKnockupAuthoringTests
    {
        // ⚠ **띄움은 스턴보다 짧아야 한다.** 스턴 3초 내내 떠 있으면 지진 충격이 아니라
        // 무중력이다. 뷰 체공 시간을 직접 재긴 어려우므로 그 시간을 정하는 두 값의 관계를
        // 못박는다(브리지가 `min(knockupOnHitSec, onPlaceDuration)` 으로 체공을 정한다).
        [Test]
        public void KnockupHopIsShorterThanTheStun()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(
                "Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsNotNull(catalog, "DefenderCatalog");
            var unit = catalog.ById("malphite");
            Assert.IsNotNull(unit, "malphite");

            // unit 2g — 「레거시 배치 필드가 꺼져 있다」 단언은 은퇴했다.
            // 그 필드군 자체가 철거돼 켤 방법이 없다.
            var spec = unit.GetAbility<UnitSkillAbility>()?.mechanics[0].payload;
            Assert.IsNotNull(spec, "말파이트에 배치 스킬(UnitSkillAbility)이 배선돼야 한다");
            Assert.AreEqual(DcPayloadKind.AreaCc, spec.Value.kind);
            Assert.AreEqual(DcCcKind.Stun, spec.Value.ccKind);
            float stunSec = spec.Value.duration;

            Assert.Greater(unit.knockupVisualHeight, 0f, "띄우는 유닛이어야 이 계약이 의미가 있다");
            Assert.Greater(unit.knockupOnHitSec, 0f,
                "knockupOnHitSec 이 0 이면 체공이 스턴 길이로 떨어져 3초를 떠 있는다");
            Assert.Less(unit.knockupOnHitSec, stunSec,
                $"체공({unit.knockupOnHitSec}s)이 스턴({stunSec}s)보다 짧아야 한다 — " +
                "튀어올랐다 착지한 뒤 남은 시간은 땅에서 굳어 있는 그림이다");
        }
    }
}
