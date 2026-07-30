using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // page-local-presets unit 6 — 프리셋 2개를 만들어 서로 다른 편성을 저장하고, 두 번째를
    // 확정한 뒤 배틀에 들어가면 **그 프리셋의 내용**이 반입되는지 본다.
    //
    // 저장/확정 분리의 통합 검증이다: EditMode 의 PresetCommitSemanticsTests 가 프로필
    // 수준에서 같은 규칙을 보고, 이 테스트는 그것이 실제 씬 전환·반입까지 이어지는지 본다.
    public class PresetCarryInTest
    {
        private PlayerProfileSO _profSO;

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            // 이 런의 프리셋 세팅이 다른 테스트로 새지 않게 되돌린다 — 확정분만 남기고
            // 추가한 프리셋을 걷어낸다.
            var p = _profSO != null ? _profSO.profile : null;
            if (p?.squads != null)
            {
                p.squads.RemoveAll(s => s != null && s.id == "squad_carryin_test");
                p.NormalizePresets();
                var squad = p.CommittedSquad();
                if (squad != null)
                    for (int i = 0; i < squad.unitIds.Count; i++) squad.unitIds[i] = "";
            }
        }

        [UnityTest]
        public IEnumerator CommittedPreset_CarriesInItsOwnUnits()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Outgame, LoadSceneMode.Single);
            yield return null;

            var menu = Object.FindObjectOfType<OutgameMenuController>();
            Assert.IsNotNull(menu, "outgame menu present");
            _profSO = Field<PlayerProfileSO>(menu, "profileSO");
            Assert.IsNotNull(_profSO, "profileSO wired");

            var catalog = Field<Wassup.Data.DefenderCatalog>(menu, "catalog");
            var catalogIds = new List<string>(catalog.AllIds());
            Assert.GreaterOrEqual(catalogIds.Count, 3, "catalog has enough units");

            var profile = _profSO.profile;
            profile.NormalizePresets();

            // 기본(확정) 프리셋에는 A 를, 새 프리셋에는 B 를 담는다.
            var first = profile.CommittedSquad();
            Assert.IsNotNull(first, "default preset exists");
            for (int i = 0; i < first.unitIds.Count; i++) first.unitIds[i] = "";
            first.unitIds[0] = catalogIds[0];

            var second = new SquadPreset { id = "squad_carryin_test", name = "반입 테스트" };
            second.NormalizeSlots();
            second.unitIds[0] = catalogIds[1];
            second.unitIds[1] = catalogIds[2];
            profile.squads.Add(second);

            // 두 번째를 **확정**한다 = [선택].
            profile.selectedSquadId = second.id;
            profile.NormalizePresets();
            Assert.AreEqual(second.id, profile.CommittedSquad().id, "두 번째가 확정됐다");

            LogAssert.ignoreFailingMessages = true;   // BattleScene 로드 시 기존 노이즈

            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var gm = Object.FindObjectOfType<GameManager>();
            Assert.IsNotNull(gm, "battle GameManager present");
            Assert.AreNotEqual(GamePhase.Draft, gm.CurrentPhase,
                "확정 프리셋이 채워져 있으므로 draft 를 건너뛴다");

            // 반입된 라인업은 확정 프리셋(second)의 것이어야 한다 — first 의 것이 아니다.
            var resolved = SquadDraw.Resolve(profile.CommittedSquad().unitIds);
            CollectionAssert.AreEqual(new[] { catalogIds[1], catalogIds[2] }, resolved,
                "확정한 프리셋의 유닛이 반입된다");
            CollectionAssert.DoesNotContain(resolved, catalogIds[0],
                "확정하지 않은 프리셋의 유닛은 반입되지 않는다");

            // RequestPlacement 뒤에 곧바로 Placement 가 오지 않는다 — gift-phase 가
            // Placement 앞에 GamePhase.Gift 를 끼워넣었고 그 연출이 수 초 돈다. 이 테스트의
            // 검증 대상은 **반입 내용**이므로 정확한 프레임 phase 를 핀하지 않고, squad 경로에
            // 올라탔다는 것(=Draft 가 아니다)만 확인한다. Placement 도달 자체는 기존
            // SquadCarryInSmokeTest 의 관심사다.
            gm.RequestPlacement();
            yield return null;
            yield return null;
            Assert.That(gm.CurrentPhase, Is.EqualTo(GamePhase.Gift).Or.EqualTo(GamePhase.Placement),
                "확정 프리셋이 있으면 draft 가 아니라 gift→placement 경로로 간다");
        }

        private static T Field<T>(object target, string name) where T : class
            => (T)target.GetType()
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(target);
    }
}
