using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Hud;
using Wassup.BattleCoreUnity.Input;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild 5b 수정 — **배치된 유닛을 눌러 상세를 보고 거기서 퇴근시키는가.**
    //
    // 사용자 플레이 3차의 문장: **「배치된 유닛 터치/유닛 셀 누르면 상세 UI 아직 미구현인가?
    // 퇴근이 확인 불가」**.
    //
    // 5b 는 이 패널을 unit 7 로 미루고 퇴근을 **길게 누르기로 새로** 지었다 — 옛 게임에 없던
    // 축이다. 퇴근은 **선택 패널의 액션 슬롯 버튼**이고(`defender-clock-out/2`), 그 길이
    // 없으면 플레이어는 퇴근을 **발견할 수 없다**.
    //
    // ⚠ 포인터 제스처는 흉내 내지 않는다. 제스처가 부르는 그 함수들(`SelectionInput` 의
    // 진입·닫기, 패널의 액션 슬롯)을 직접 부른다.
    public sealed class CoreSelectionPanelTests
    {
        [UnityTest]
        public IEnumerator 판_위_유닛을_누르면_상세가_열리고_퇴근이_보인다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");

            var panel = Object.FindAnyObjectByType<CoreSelectionPanel>();
            var selection = Object.FindAnyObjectByType<SelectionInput>();
            Assert.IsNotNull(panel, "선택 패널이 씬에 없다 — 퇴근을 확인할 길이 없다");
            Assert.IsNotNull(selection, "선택 입력이 씬에 없다");
            Assert.IsFalse(panel.IsVisible, "아무것도 안 눌렀는데 패널이 떠 있다");

            driver.Apply(Command.FinishPlacement());
            Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out int2 anchor),
                "로스터의 어떤 유닛도 이 판 어디에도 놓을 수 없다");
            Assert.IsTrue(driver.Apply(Command.PlaceDefender(defIndex, anchor)).Accepted);
            var id = LastDefender(driver);

            // 배치 모션이 끝나 활성화될 때까지 — 배치 중에는 버튼이 잠긴다(코어 답의 미리 보기).
            float t = 0f;
            while (driver.Find(id) != null && driver.Find(id).Deploying && t < 4f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // ① 판 위 유닛 탭.
            selection.SelectAt(id);
            yield return null;
            Assert.IsTrue(panel.IsVisible, "판 위 유닛을 눌렀는데 상세가 안 열렸다");
            Assert.AreEqual(id, selection.Selected);

            var asset = driver.DefenderAssets[defIndex];
            string want = asset != null && !string.IsNullOrEmpty(asset.displayName)
                ? asset.displayName : driver.Definition.Units[defIndex].Id;
            Assert.AreEqual(want, panel.ShownName, "다른 유닛의 이름이 떠 있다");
            Assert.IsTrue(panel.ActionEnabled, "퇴근 버튼이 잠겨 있다");

            // ③ 퇴근 버튼 = `Retire` 커맨드 1회 → 유닛 소멸 → 트레이 쿨타임.
            var retired = new List<SimEntityId>();
            System.Action<CoreEvent> probe = e =>
            {
                if (e.Kind == CoreEventKind.Retired) retired.Add(e.A);
            };
            driver.Subscribe(ViewOrder.Trace, probe);
            try
            {
                panel.InvokeAction();
                Assert.AreEqual(1, retired.Count, "퇴근 사건이 정확히 한 번 나야 한다");
                Assert.AreEqual(id, retired[0]);
                Assert.IsNull(driver.Find(id), "퇴근했는데 유닛이 판에 남아 있다");
                Assert.Greater(driver.Match.Placement.CooldownRemaining(defIndex), 0f,
                    "퇴근 뒤 재배치 대기가 안 걸렸다");
            }
            finally
            {
                driver.Unsubscribe(probe);
            }

            yield return null;
            Assert.IsFalse(panel.IsVisible, "대상이 사라졌는데 상세가 남아 있다");

            CoreSceneFixture.EndErrorWatch();
            Assert.AreEqual(0, CoreSceneFixture.Errors.Count,
                "콘솔 에러: " + string.Join(" | ", CoreSceneFixture.Errors));
        }

        [UnityTest]
        public IEnumerator 트레이_소진_칸을_누르면_그_유닛의_상세가_열린다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);
            var panel = Object.FindAnyObjectByType<CoreSelectionPanel>();
            var selection = Object.FindAnyObjectByType<SelectionInput>();
            Assert.IsNotNull(panel);
            Assert.IsNotNull(selection);

            driver.Apply(Command.FinishPlacement());
            Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out int2 anchor));
            Assert.IsTrue(driver.Apply(Command.PlaceDefender(defIndex, anchor)).Accepted);
            var id = LastDefender(driver);

            // 그 종류가 판에 있으니 트레이 칸은 「소진」이다 — 그 칸은 집어 들 수 없고, 탭은
            // **그 유닛을 여는** 것이 된다(`selection-entry-narrowing` unit 1).
            Assert.AreEqual(RejectReason.LimitReached, driver.Match.Placement.SlotBlock(defIndex),
                "그 칸이 소진이 아니다 — 이 진입구를 증언할 수 없다");

            Assert.IsTrue(selection.SelectByTraySlot(defIndex),
                "소진 칸을 눌렀는데 그 유닛이 안 열렸다");
            yield return null;
            Assert.IsTrue(panel.IsVisible);
            Assert.AreEqual(id, selection.Selected, "트레이 칸이 다른 유닛을 열었다");

            // ④ 빈 곳 탭 = 닫기.
            selection.CloseSelection();
            yield return null;
            Assert.IsFalse(panel.IsVisible, "빈 곳을 눌렀는데 상세가 안 닫혔다");
            Assert.IsFalse(selection.Selected.IsEntity);
        }

        [UnityTest]
        public IEnumerator 길게_누르기_퇴근은_은퇴했다()
        {
            yield return null;
            // 옛 게임에 없던 축이라 이식이 아니라 **발명**이었다. 패널 버튼이 정본 경로다.
            var legacy = System.Type.GetType(
                "Wassup.BattleCoreUnity.Input.RetireInput, Wassup.Runtime", throwOnError: false);
            Assert.IsNull(legacy, "길게 누르기 퇴근이 아직 살아 있다 — 퇴근 통로가 둘이다");
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

        private static SimEntityId LastDefender(BattleDriver driver)
        {
            var units = driver.Match.World.Units;
            for (int i = units.Count - 1; i >= 0; i--)
                if (units[i].Kind == UnitKind.Defender) return units[i].Id;
            Assert.Fail("판에 방어유닛이 없다");
            return SimEntityId.None;
        }

        private static bool TryFindPlaceable(BattleDriver driver, out int defIndex, out int2 anchor)
        {
            var placement = driver.Match.Placement;
            var size = driver.GridSize;
            for (int i = 0; i < driver.Definition.Units.Length; i++)
            {
                if (!placement.InRoster(i)) continue;
                for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    var c = new int2(x, y);
                    if (placement.Judge(i, c) != RejectReason.None) continue;
                    defIndex = i;
                    anchor = c;
                    return true;
                }
            }
            defIndex = -1;
            anchor = default;
            return false;
        }
    }
}
