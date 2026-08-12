using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // defender-board-limit 1 — 트레이 소진 **표현**의 가드.
    //
    // `BoardLimitPlacementTest` 는 판정(브리지)만 본다. 여기서 보는 것은 그 판정이 화면으로
    // 옮겨지는 층이다: 소진 셀에 테두리가 켜지고, 사망하면 꺼지고, 쿨타임 오버레이가 소진에
    // 밀려 내려가고(우선순위 소진 > 쿨타임), 튜토리얼이 소진 셀을 가리키지 않는가.
    //
    // 슬롯은 런타임 생성이라 계층에서 이름으로 찾는다(`BoardLimitRim` / `CooldownOverlay`) —
    // `_slotVisuals` 는 private struct 리스트라 리플렉션으로 읽어도 value-copy 함정이 있다.
    public class BoardLimitTrayStateTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExhaustedSlot_ShowsRim_HidesCooldown_AndLeavesTutorialCandidate()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var selector = Object.FindObjectOfType<DefenderSelector>();
            Assert.IsNotNull(selector, "DefenderSelector present");

            // 상한에 닿을 유닛(1기) + 상한이 안 닿는 유닛(100기). 후자는 튜토리얼 후보가
            // 살아남는지 확인하는 대조군이다.
            // 상한 유닛에는 쿨타임도 건다 — 저작 규칙상 같이 걸지 않지만(README 계약 10),
            // **가드가 실제로 있는지**는 둘이 겹칠 때만 관측된다.
            var capped = MakeUnit("ranger", 1);
            capped.placementCooldown = 30f;
            var free = MakeUnit("guardian", 100);

            bridge.SetDefenderPool(new[] { capped, free });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            // 트레이 슬롯을 짓는 신호는 bridge.BeginPlacement 가 아니라 **페이즈 전환**이다
            // (DefenderSelector.OnPhaseChanged → RebuildSlots). 라이브에선 선물 페이즈가 끝나며
            // 오는 신호라, 배치 판정만 구동하는 다른 테스트들은 슬롯 없이도 돈다.
            gm.SetPhase(GamePhase.Placement);
            for (int i = 0; i < 4; i++) yield return null;

            var cappedSlot = FindSlot(selector, capped);
            Assert.IsNotNull(cappedSlot, "상한 유닛 슬롯이 트레이에 있다");
            // 존재를 먼저 못박는다 — 없으면 아래 "꺼져 있다" 단정이 공허하게 통과한다
            // (config 에 rimFlowMaterial 이 빠지면 위젯 자체가 안 만들어진다).
            Assert.IsNotNull(FindChild(cappedSlot, "BoardLimitRim"), "테두리 위젯이 생성됐다");
            Assert.IsFalse(RimShown(cappedSlot), "배치 전에는 테두리가 꺼져 있다");

            Assert.IsTrue(PlaceFirstValid(bridge, capped), "상한 유닛 배치");
            for (int i = 0; i < 3; i++) yield return null;

            Assert.IsTrue(RimShown(cappedSlot), "소진되면 테두리가 켜진다");
            Assert.IsFalse(CooldownShown(cappedSlot),
                "우선순위 소진 > 쿨타임 — 소진 셀에 쿨타임 오버레이가 뜨면 안 된다(헛기다림)");

            // 코스트가 바뀌는 프레임에 도색 루프가 다시 도는데, 거기서 소진을 안 보면
            // 포트레이트가 흰색으로 되돌아가고 경고 글리프가 되살아난다(리뷰에서 짚은 자리).
            gm.CostRuntime.AddCost(50);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(RimShown(cappedSlot), "코스트 변화 프레임 뒤에도 소진 표현이 유지된다");
            Assert.IsFalse(CooldownShown(cappedSlot), "코스트 변화 프레임 뒤에도 쿨타임은 눌려 있다");

            // 튜토리얼 추천은 소진 셀을 가리키지 않는다 — 놓을 수 없는 칸을 가리키면 막힌다.
            Assert.IsTrue(selector.TryGetAffordableTutorialSlot(out var target), "추천 후보가 있다");
            Assert.AreNotSame(cappedSlot.transform, target, "추천이 소진 셀이 아니다");

            // 사망 → 자리가 비면 표현도 되돌아온다.
            bridge.StartBattle(); // 사망 드레인은 _running 게이트 아래
            yield return null;
            Assert.IsTrue(bridge.TryGetDeployedEntity(capped, out var entity), "배치된 엔티티 해석");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var h = em.GetComponentData<Health>(entity);
            em.SetComponentData(entity, new Health { value = 0f, max = h.max });
            for (int i = 0; i < 8; i++) yield return null;

            Assert.IsFalse(RimShown(cappedSlot), "사망하면 테두리가 꺼진다");
        }

        private static DefenderUnitData MakeUnit(string id, int maxOnBoard)
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            Assert.Greater(all.Length, 0, "DefenderCatalog present");
            var src = all[0].ById(id);
            Assert.IsNotNull(src, $"{id} in catalog");
            var copy = Object.Instantiate(src);   // 에셋을 직접 고치면 디스크에 박힌다
            copy.maxOnBoard = maxOnBoard;
            return copy;
        }

        // 슬롯 GO 이름은 RebuildSlots 가 `Slot_{displayName}` 으로 짓는다.
        private static GameObject FindSlot(DefenderSelector selector, DefenderUnitData unit)
        {
            var panel = selector.PanelGO;
            if (panel == null) return null;
            foreach (var rt in panel.GetComponentsInChildren<RectTransform>(true))
                if (rt.name == $"Slot_{unit.displayName}") return rt.gameObject;
            return null;
        }

        private static bool RimShown(GameObject slot) => ChildActive(slot, "BoardLimitRim");
        private static bool CooldownShown(GameObject slot) => ChildActive(slot, "CooldownOverlay");

        private static bool ChildActive(GameObject slot, string name)
        {
            var t = FindChild(slot, name);
            return t != null && t.gameObject.activeSelf;
        }

        private static Transform FindChild(GameObject slot, string name)
        {
            foreach (var t in slot.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
        }
    }
}
