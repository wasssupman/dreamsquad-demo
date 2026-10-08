using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.BattleCoreUnity.Cards;
using Somnia.Battle.BattleCoreUnity.Hud;
using Somnia.Battle.BattleCoreUnity.Input;
using Somnia.Battle.Data;

namespace Somnia.Battle.Tests.PlayMode.Core
{
    // battle-core-rebuild 8b 이식 제외 G16 — **조준 모드 배타.** 옛 규칙: 스킬 탭 조준 중에는 다른 조작(배치 드래그·선택)이
    // 끼어들지 못한다. 새 층에서 그 배타를 지는 곳은 셋이고, 전용 잠금은 없다 — 구조가 배타다:
    //   · 카드 조준은 손패가 열려 있어야 시작된다(`CoreHandView.CanStartDrag` — `State == Hand`).
    //   · 손패가 열리면 트레이 칸 줄이 접히고, 접힌 동안 칸 픽이 없다(`CoreDefenderTray.TryPickSlot`) → 배치 누름이 안 선다.
    //   · 배치 드래그·집어 듦이 시작되면 선택이 닫히고 선택 기인 손패도 닫힌다(`SelectionInput.Update`) → 카드 조준이 안 선다.
    //
    // ⚠ 포인터 장치를 흉내 내지 않는다(`CoreCardViewTests`·`CoreDragPreviewTests` 와 같은 이유) — 제스처가 부르는 창구를 부른다.
    // 배치 쪽 진입은 **제스처 입구**(`DragPlacementInput.TryBeginPress` — 칸 판정 포함)를 반사로 부른다. 공개 `BeginPress` 는
    // 칸 판정 뒤의 단계라 배타를 우회한다. 음성 단언마다 같은 입구의 양성 대조를 둬 「원래 안 서는 것」이 아님을 보인다.
    public sealed class CoreAimExclusivityTests
    {
        private const string TremorPlate = "Assets/_Project/Runtime/Battle/Data/Dreamcatcher/Card_TremorPlate.asset";
        private const string Offering = "Assets/_Project/Runtime/Battle/Data/Dreamcatcher/Card_FattenedOffering.asset";
        private const string Meteor = "Assets/_Project/Runtime/Battle/Data/Dreamcatcher/Active_Meteor.asset";

        private static readonly MethodInfo TryBeginPress =
            typeof(DragPlacementInput).GetMethod("TryBeginPress", BindingFlags.NonPublic | BindingFlags.Instance);

        private sealed class Ctx
        {
            public BattleDriver Driver;
            public CoreHandView Hand;
            public SelectionInput Selection;
            public DragPlacementInput Placement;
            public CoreDefenderTray Tray;
        }

        [UnityTest]
        public IEnumerator 카드_조준_중에는_배치_드래그가_시작되지_않는다()
        {
            CoreSceneFixture.BeginErrorWatch();
            var c = new Ctx();
            yield return Boot(c);
            SimEntityId host = SimEntityId.None;
            yield return PlaceAndActivate(c, id => host = id);
            c.Driver.Match.Hand.Gain(80f);

            // 양성 대조 — 손패가 닫힌 동안 같은 입구는 트레이 칸 누름을 받는다.
            Vector2 slotScreen = default;
            int trayDef = -1;
            yield return FindTraySlot(c, (d, s) => { trayDef = d; slotScreen = s; });
            Assert.IsTrue(PressAt(c, slotScreen), "손패가 닫혔는데 트레이 칸 누름이 안 선다 — 음성 단언이 비어 버린다");
            c.Placement.Release(slotScreen);   // 이동 없는 뗌 = 집어 들기
            c.Placement.Disarm();
            yield return null;

            // 카드 조준 — 액티브(칸 조준)는 선택을 놓고 손패를 유지한다(가장 약한 경우: 선택이 비어도 배타가 서야 한다).
            c.Selection.SelectAt(host);
            yield return WaitHandSettled(c);
            int slot = SlotOfCard(c, Meteor);
            Assert.GreaterOrEqual(slot, 0, "손패에 시험 카드가 없다");
            var drag = c.Hand.Slots[slot].dragSlot;
            Assert.AreEqual(CoreCardAim.TileAim, c.Hand.Input.AimOf(c.Hand.Slots[slot].cardIndex), "시험 전제: 칸 조준 카드");
            drag.BeginDragAt(c.Hand.SlotScreenCenter(slot));
            Assert.IsTrue(drag.IsDragging, "카드 조준이 시작되지 않았다");
            yield return null;
            Assert.IsFalse(c.Selection.Selected.IsEntity, "시험 전제: 칸 조준은 선택을 놓는다");
            Assert.IsTrue(c.Hand.IsOpen, "시험 전제: 칸 조준 중에도 손패는 열려 있다");

            // 조준 중 트레이 칸을 눌러도 배치 제스처가 서지 않는다.
            Assert.IsFalse(PressAt(c, slotScreen), "카드 조준 중인데 트레이 칸 누름이 배치 제스처를 열었다");
            Assert.IsFalse(c.Placement.IsDragging, "카드 조준 중 배치 드래그가 섰다");
            Assert.IsFalse(c.Placement.IsArmed, "카드 조준 중 유닛을 집어 들었다");
            Assert.IsTrue(drag.IsDragging, "배치 시도가 카드 조준을 끊었다");

            drag.CancelDrag();
            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, "콘솔 에러: " + string.Join(" | ", CoreSceneFixture.Errors));
        }

        [UnityTest]
        public IEnumerator 배치_드래그_중에는_카드_조준이_시작되지_않는다()
        {
            CoreSceneFixture.BeginErrorWatch();
            var c = new Ctx();
            yield return Boot(c);
            SimEntityId host = SimEntityId.None;
            yield return PlaceAndActivate(c, id => host = id);
            c.Driver.Match.Hand.Gain(80f);
            Vector2 slotScreen = default;
            int trayDef = -1;
            yield return FindTraySlot(c, (d, s) => { trayDef = d; slotScreen = s; });

            // 양성 대조 — 손패가 열려 있고 배치 제스처가 없으면 카드 조준은 설 수 있다.
            c.Selection.SelectAt(host);
            yield return WaitHandSettled(c);
            int slot = SlotOfCard(c, TremorPlate);
            Assert.GreaterOrEqual(slot, 0, "손패에 시험 카드가 없다");
            Assert.IsTrue(c.Hand.CanStartDrag(slot), "배치 제스처가 없는데 카드 조준이 막혀 있다 — 음성 단언이 비어 버린다");

            // 배치 드래그 — 칸 판정 뒤의 창구로 연다(손패가 연 칸 줄 접힘은 앞 테스트가 증언한다. 여기는 그 반대쪽 배타).
            c.Placement.BeginPress(trayDef, slotScreen);
            for (int i = 1; i <= 6; i++)
            {
                c.Placement.StepDrag(slotScreen + new Vector2(0f, 12f * i));
                yield return null;
            }
            Assert.IsTrue(c.Placement.IsDragging, "배치 드래그로 승격되지 않았다");
            Assert.IsFalse(c.Hand.IsOpen, "배치 드래그가 시작됐는데 손패가 열려 있다");
            Assert.IsFalse(c.Selection.Selected.IsEntity, "배치 드래그가 시작됐는데 선택이 남아 있다");

            var drag = c.Hand.Slots[slot].dragSlot;
            drag.BeginDragAt(c.Hand.SlotScreenCenter(slot));
            Assert.IsFalse(drag.IsDragging, "배치 드래그 중 카드 조준이 섰다");

            // 드래그 중 선택이 다시 열려도(같은 프레임 외 경로) 다음 프레임에 닫힌다.
            c.Selection.SelectAt(host);
            c.Placement.StepDrag(slotScreen + new Vector2(0f, 80f));
            yield return null;
            Assert.IsFalse(c.Hand.IsOpen, "배치 드래그 중 선택이 손패를 다시 열었다");
            Assert.IsFalse(c.Hand.CanStartDrag(slot), "배치 드래그 중 카드 조준 창이 열려 있다");

            c.Placement.enabled = true;
            c.Placement.Release(new Vector2(2f, Screen.height - 2f));   // 판 밖 = 취소
            yield return null;
            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, "콘솔 에러: " + string.Join(" | ", CoreSceneFixture.Errors));
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

        private static IEnumerator Boot(Ctx c)
        {
            yield return CoreSceneFixture.LoadAndBoot(d => c.Driver = d);
            Assert.IsNotNull(c.Driver, "BattleCoreScene 에 BattleDriver 가 없다");
#if UNITY_EDITOR
            var paths = new[] { TremorPlate, Offering, Meteor };
            var cards = new DreamcatcherCard[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                cards[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(paths[i]);
                Assert.IsNotNull(cards[i], "카드 에셋이 없다: " + paths[i]);
            }
            CoreSceneFixture.OverrideDeck(c.Driver, cards);
#endif
            c.Hand = Object.FindAnyObjectByType<CoreHandView>();
            c.Selection = Object.FindAnyObjectByType<SelectionInput>();
            c.Placement = Object.FindAnyObjectByType<DragPlacementInput>();
            c.Tray = Object.FindAnyObjectByType<CoreDefenderTray>();
            Assert.IsNotNull(c.Hand, "씬에 손패가 없다");
            Assert.IsNotNull(c.Selection); Assert.IsNotNull(c.Placement); Assert.IsNotNull(c.Tray);
            Assert.IsNotNull(TryBeginPress, "배치 제스처 입구(`TryBeginPress`)가 사라졌다 — 이 테스트의 창구를 고칠 것");

            c.Driver.Begin();
            // 배치 입력의 `Update` 는 실제 포인터를 읽는다(없으면 매 프레임 제스처를 끝낸다) — 제스처는 이 테스트가 몬다.
            // 선택 입력은 켜 둔다: 배치 드래그가 선택을 닫는 배타가 그 `Update` 에 있다.
            c.Placement.enabled = false;
            yield return null;
            c.Driver.Apply(Command.FinishPlacement());
            yield return null;
        }

        private static bool PressAt(Ctx c, Vector2 screen) => (bool)TryBeginPress.Invoke(c.Placement, new object[] { screen });

        // 트레이에 칸이 선 로스터 유닛 하나와 그 칸의 화면 중심.
        private static IEnumerator FindTraySlot(Ctx c, System.Action<int, Vector2> found)
        {
            Assert.Greater(c.Tray.SlotCount, 0, "트레이에 칸이 없다");
            for (int frame = 0; frame < 20; frame++)
            {
                for (int d = 0; d < c.Driver.Definition.Units.Length; d++)
                {
                    if (!c.Driver.Match.Placement.InRoster(d)) continue;
                    if (!c.Tray.TryGetSlotScreenCenter(d, null, out var s)) continue;
                    found(d, s);
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("트레이 칸의 화면 자리가 없다");
        }

        private static IEnumerator PlaceAndActivate(Ctx c, System.Action<SimEntityId> found)
        {
            Assert.IsTrue(TryFindPlaceable(c.Driver, out int defIndex, out int2 anchor), "놓을 곳이 없다");
            Assert.IsTrue(c.Driver.Apply(Command.PlaceDefender(defIndex, anchor)).Accepted);
            SimEntityId id = SimEntityId.None;
            var units = c.Driver.Match.World.Units;
            for (int i = units.Count - 1; i >= 0; i--)
                if (units[i].Kind == UnitKind.Defender) { id = units[i].Id; break; }
            Assert.IsTrue(id.IsEntity, "놓은 유닛이 없다");
            float t = 0f;
            while (c.Driver.Find(id) != null && c.Driver.Find(id).Deploying && t < 5f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsFalse(c.Driver.Find(id).Deploying, "배치가 끝나지 않았다");
            found(id);
        }

        private static IEnumerator WaitHandSettled(Ctx c)
        {
            float t = 0f;
            while ((!c.Hand.IsOpen || c.Hand.Transitioning) && t < 3f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            c.Hand.TryFastForwardDeal();
            yield return null;
        }

        private static int SlotOfCard(Ctx c, string path)
        {
#if UNITY_EDITOR
            var card = UnityEditor.AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(path);
            var slots = c.Hand.Slots;
            for (int i = 0; i < slots.Count; i++) if (slots[i].card == card && slots[i].entryId >= 0) return i;
#endif
            return -1;
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
                    var cell = new int2(x, y);
                    if (placement.Judge(i, cell) != RejectReason.None) continue;
                    defIndex = i;
                    anchor = cell;
                    return true;
                }
            }
            defIndex = -1;
            anchor = default;
            return false;
        }
    }
}
