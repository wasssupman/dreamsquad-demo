using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Cards;
using Wassup.BattleCoreUnity.Hud;
using Wassup.BattleCoreUnity.Input;
using Wassup.BattleCoreUnity.View;
using Wassup.Data;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 7c — **카드가 손에 잡히나.** 사건 → 뷰 수 일치 · 회수 · 제스처 사슬(선택 → 손패 → 탭/끌기 → 커맨드 →
    // receipt → 카드 줄) · 거절 문구 = 코어 답 · 표식은 숙주 소멸로 거둔다.
    //
    // ⚠ 포인터 장치를 흉내 내지 않는다(`CoreDragPreviewTests` 와 같은 이유). 제스처가 부르는 **창구**를 직접 부른다 —
    // `SelectionInput.SelectAt` · `CoreCardDragSlot.Tap`/`BeginDragAt`/`DragTo`/`EndDragAt`.
    //
    // 덱은 저작 덮어쓰기(`BattleDriver._cards`)로 건다 — 프로필 저장 덱은 머신 상태를 상속한다(골든 코퍼스와 같은 함정).
    // 카드 화면 컴포넌트가 씬에 아직 없으면(씬 배선 전) **여기서 세운다** — 컴포넌트는 빈 칸을 같은 씬에서 찾는다.
    public sealed class CoreCardViewTests
    {
        private const string TremorPlate = "Assets/_Project/Data/Dreamcatcher/Card_TremorPlate.asset";
        private const string Offering = "Assets/_Project/Data/Dreamcatcher/Card_FattenedOffering.asset";
        private const string Meteor = "Assets/_Project/Data/Dreamcatcher/Active_Meteor.asset";
        private const string FocusConfig = "Assets/_Project/Data/Dreamcatcher/DreamcatcherFocusConfig.asset";

        private sealed class Ctx
        {
            public BattleDriver Driver;
            public CoreHandView Hand;
            public SelectionInput Selection;
            public CoreSelectionPanel Panel;
            public CoreUnitOverheadUiLayer Overhead;
            public CoreStatusFxSpawner StatusFx;
            public CoreMapOverlay Overlay;
            public readonly List<CoreEvent> Cards = new List<CoreEvent>();
        }

        [UnityTest]
        public IEnumerator 탭_부착이면_카드_줄이_서고_퇴근하면_거둔다()
        {
            CoreSceneFixture.BeginErrorWatch();
            var c = new Ctx();
            yield return Boot(c, TremorPlate, Offering, Meteor);
            c.Driver.Apply(Command.FinishPlacement());
            SimEntityId host = SimEntityId.None;
            yield return PlaceAndActivate(c, id => host = id);
            c.Driver.Match.Hand.Gain(80f);

            c.Selection.SelectAt(host);
            yield return WaitHandSettled(c);
            Assert.IsTrue(c.Hand.IsOpen, "유닛을 골랐는데 손패가 안 열렸다(손패 진입구 = 유닛 선택)");
            int slot = SlotOfCard(c, TremorPlate);
            Assert.GreaterOrEqual(slot, 0, "손패에 시험 카드가 없다");
            int entry = c.Hand.Slots[slot].entryId;

            c.Hand.Slots[slot].dragSlot.Tap();
            yield return WaitFor(() => c.Cards.Exists(e => e.Kind == CoreEventKind.CardAttached), 4f);
            Assert.AreEqual(1, CountKind(c, CoreEventKind.CardAttached), "탭 한 번에 부착 사건이 정확히 한 건이어야 한다");
            Assert.AreEqual(1, c.Driver.Match.Hand.CountAttachedTo(host));
            yield return null;
            Assert.AreEqual(1, c.Overhead.CardIconCountOf(host), "부착 1 → 오버헤드 카드 아이콘 1");
            Assert.AreEqual(1, c.Panel.AttachRowCount, "부착 1 → 선택 패널 카드 줄 1");
            Assert.Less(c.Hand.IndexOfEntry(entry), 0, "붙인 카드가 손패에 남아 있다(창은 코어의 것)");

            // 퇴근 → `CardDetached` → 두 자리의 줄이 거둬진다(카드는 큐로 돌아간다 — D8).
            Assert.IsTrue(c.Driver.Apply(Command.Retire(host)).Accepted);
            yield return null;
            Assert.AreEqual(1, CountKind(c, CoreEventKind.CardDetached), "퇴근에 떨어짐 사건이 한 건");
            Assert.AreEqual(0, c.Overhead.CardIconCountOf(host), "숙주가 떠났는데 오버헤드 카드 아이콘이 남았다");
            Assert.AreEqual(0, c.Panel.AttachedCountOf(host), "숙주가 떠났는데 패널 목록이 남았다");

            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, "콘솔 에러: " + string.Join(" | ", CoreSceneFixture.Errors));
        }

        [UnityTest]
        public IEnumerator 끌어서_부착하면_범위_링이_host_몸으로_재진다()
        {
            var c = new Ctx();
            yield return Boot(c, TremorPlate, Offering, Meteor);
            c.Driver.Apply(Command.FinishPlacement());
            SimEntityId host = SimEntityId.None;
            yield return PlaceAndActivate(c, id => host = id);
            c.Driver.Match.Hand.Gain(80f);
            c.Selection.SelectAt(host);
            yield return WaitHandSettled(c);
            int slot = SlotOfCard(c, TremorPlate);
            var drag = c.Hand.Slots[slot].dragSlot;
            int cardIndex = c.Hand.Slots[slot].cardIndex;

            Assert.IsTrue(c.Hand.Targets.TryGetUnitScreenRect(host, out var rect), "숙주의 화면 렉트가 없다");
            drag.BeginDragAt(c.Hand.SlotScreenCenter(slot));
            Assert.IsTrue(drag.IsDragging, "끌기가 시작되지 않았다(딤·전이 게이트)");
            drag.DragTo(rect.center);
            yield return null;
            Assert.AreEqual(host, drag.Hover, "숙주 위로 끌었는데 락온이 안 됐다");

            // 반경 = 판정과 같은 함수(`RangeCatalog` → `RadiusWithOrigin(host 몸)`). 이 카드는 **몸에서 나오는 것**이라 원점 항 = 몸.
            var spec = CoreCardDragSlot.CardRangeOf(c.Driver.Definition, cardIndex);
            Assert.AreNotEqual(Wassup.BattleCore.Trigger.RangeShape.None, spec.Shape, "시험 카드에 공간 도형이 없다 — 링을 증언할 수 없다");
            Assert.IsTrue(c.Overlay.TryGetCardArea(out float r, out var center), "유효 락온인데 부착 범위 링이 없다");
            var u = c.Driver.Find(host);
            Assert.AreEqual(spec.RadiusTiles + u.HitRadius, r, 1e-4f, "링 반경 = 반경 N + 숙주 몸(대상 몸은 안 그린다)");
            Assert.Less(math.distance(center, u.Position), 1e-3f, "링 중심 = 숙주 발밑");

            drag.EndDragAt(rect.center);
            yield return WaitFor(() => c.Cards.Exists(e => e.Kind == CoreEventKind.CardAttached), 4f);
            Assert.AreEqual(1, c.Driver.Match.Hand.CountAttachedTo(host), "끌어 놓았는데 안 붙었다");
            Assert.IsFalse(c.Overlay.TryGetCardArea(out _, out _), "드래그가 끝났는데 링이 남았다");
        }

        [UnityTest]
        public IEnumerator 거절_문구는_코어_답과_같은_문자열이다()
        {
            var c = new Ctx();
            yield return Boot(c, TremorPlate, Offering, Meteor);
            // 배치 국면 — 액티브는 **전투 중에만** 시전된다(코어 `HandDeck.Cast` 의 국면 거절 · preflight 밖의 사유).
            // 이 모드는 배치 국면에 손 배치를 닫아 두므로(`inputEnabledDuringPlacement`) 디버그 스폰으로 세운다.
            Assert.IsTrue(TryFindFreeCell(c.Driver, out int defIndex, out int2 cell0), "빈 칸이 없다");
            Assert.IsTrue(c.Driver.Apply(Command.DebugSpawnDefender(defIndex, cell0)).Accepted, "디버그 스폰 거절");
            var host = LastOfKind(c.Driver, UnitKind.Defender);
            yield return null;
            Assert.AreEqual(MatchPhase.Placement, c.Driver.Match.Clock.Phase, "시험 전제: 배치 국면");
            c.Selection.SelectAt(host);
            yield return WaitHandSettled(c);
            int slot = SlotOfCard(c, Meteor);
            Assert.GreaterOrEqual(slot, 0);
            var drag = c.Hand.Slots[slot].dragSlot;

            var cell = c.Driver.Match.Map.CellOf(c.Driver.Find(host).Position);
            Assert.IsTrue(c.Hand.Targets.TryGetCellScreenCenter(cell, out var screen));
            drag.BeginDragAt(c.Hand.SlotScreenCenter(slot));
            drag.DragTo(screen);
            drag.EndDragAt(screen);

            Assert.IsFalse(drag.LastReceipt.Accepted, "배치 국면인데 시전이 받아들여졌다");
            Assert.AreEqual(RejectReason.NotRunningOrPlacementClosed, drag.LastReceipt.Reason);
            StringAssert.Contains(CoreCardText.RejectTextOf(drag.LastReceipt.Reason), c.Hand.BriefingStatus,
                "화면 거절 문구가 코어 답을 옮겨 적지 않았다");
            Assert.AreEqual(0, CountKind(c, CoreEventKind.CardCast), "거절된 시전이 사건을 냈다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 표식은_적에게_서고_숙주가_사라지면_거둔다()
        {
            var c = new Ctx();
            yield return Boot(c, TremorPlate, Offering, Meteor);
            c.Driver.Apply(Command.FinishPlacement());
            SimEntityId host = SimEntityId.None;
            yield return PlaceAndActivate(c, id => host = id);
            Assert.IsTrue(c.Driver.Apply(Command.DebugSpawnEnemyInLane(0, 0)).Accepted, "적 스폰 거절");
            var enemy = LastOfKind(c.Driver, UnitKind.Enemy);
            yield return null;
            c.Driver.Match.Hand.Gain(80f);
            c.Selection.SelectAt(host);
            yield return WaitHandSettled(c);
            int slot = SlotOfCard(c, Offering);
            Assert.GreaterOrEqual(slot, 0);
            var drag = c.Hand.Slots[slot].dragSlot;
            Assert.AreEqual(CoreCardAim.EnemyMark, c.Hand.Input.AimOf(c.Hand.Slots[slot].cardIndex),
                "적을 겨누는 카드인데 조준이 적 표식이 아니다(코어 `TargetsEnemies`)");

            drag.BeginDragAt(c.Hand.SlotScreenCenter(slot));
            Vector2 at = EnemyScreen(c, enemy);
            drag.DragTo(at);
            drag.EndDragAt(EnemyScreen(c, enemy));
            Assert.IsTrue(drag.LastReceipt.Accepted, "적 위에서 놓았는데 표식이 거절됐다: " + drag.LastReceipt.Reason);
            yield return null;
            yield return null;
            Assert.IsTrue(c.StatusFx.IsShown(enemy, StatusFxKind.Marked), "표식 붙인 적 위에 표식이 없다");

            Assert.IsTrue(c.Driver.Apply(Command.DebugDestroy(enemy)).Accepted);
            yield return null;
            Assert.IsFalse(c.StatusFx.IsShown(enemy, StatusFxKind.Marked), "숙주가 사라졌는데 표식이 남았다");
            Assert.AreEqual(0, c.StatusFx.WantedCountOfKind(StatusFxKind.Marked));
        }

        [UnityTest]
        public IEnumerator 손패는_오버헤드_뒤에_사건을_받는다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            var log = new List<string>();
            System.Action<CoreEvent> hand = _ => log.Add("hand");
            System.Action<CoreEvent> over = _ => log.Add("overhead");
            System.Action<CoreEvent> status = _ => log.Add("status");
            // 구독 순서를 **뒤집어** 건다 — 순서는 등록 차례가 아니라 `ViewOrder` 가 말해야 한다.
            driver.Subscribe(ViewOrder.Hand, hand);
            driver.Subscribe(ViewOrder.Overhead, over);
            driver.Subscribe(ViewOrder.Status, status);
            try
            {
                driver.Apply(Command.FinishPlacement());
                log.Clear();
                driver.Apply(Command.DebugSpawnEnemyInLane(0, 0));
                Assert.GreaterOrEqual(log.Count, 3);
                CollectionAssert.AreEqual(new[] { "status", "overhead", "hand" }, log.GetRange(0, 3));
            }
            finally
            {
                driver.Unsubscribe(hand);
                driver.Unsubscribe(over);
                driver.Unsubscribe(status);
            }
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

        private static IEnumerator Boot(Ctx c, params string[] cardPaths)
        {
            yield return CoreSceneFixture.LoadAndBoot(d => c.Driver = d);
            Assert.IsNotNull(c.Driver, "BattleCoreScene 에 BattleDriver 가 없다");
#if UNITY_EDITOR
            var cards = new DreamcatcherCard[cardPaths.Length];
            for (int i = 0; i < cardPaths.Length; i++)
            {
                cards[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(cardPaths[i]);
                Assert.IsNotNull(cards[i], "카드 에셋이 없다: " + cardPaths[i]);
            }
            typeof(BattleDriver).GetField("_cards", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c.Driver, cards);
            EnsureCardViews(UnityEditor.AssetDatabase.LoadAssetAtPath<Wassup.UI.DreamcatcherFocusConfig>(FocusConfig));
#endif
            c.Hand = Object.FindAnyObjectByType<CoreHandView>();
            c.Selection = Object.FindAnyObjectByType<SelectionInput>();
            c.Panel = Object.FindAnyObjectByType<CoreSelectionPanel>();
            c.Overhead = Object.FindAnyObjectByType<CoreUnitOverheadUiLayer>();
            c.StatusFx = Object.FindAnyObjectByType<CoreStatusFxSpawner>();
            c.Overlay = Object.FindAnyObjectByType<CoreMapOverlay>();
            Assert.IsNotNull(c.Hand); Assert.IsNotNull(c.Selection); Assert.IsNotNull(c.Panel);
            Assert.IsNotNull(c.Overhead); Assert.IsNotNull(c.StatusFx); Assert.IsNotNull(c.Overlay);

            // 새 덱으로 판을 다시 짓는다(부팅 판은 저작 덱 없이 섰다). 카드 사건은 기록해 둔다.
            c.Driver.Begin();
            c.Driver.Subscribe(ViewOrder.Trace, e =>
            {
                if (e.Kind == CoreEventKind.CardAttached || e.Kind == CoreEventKind.CardDetached
                    || e.Kind == CoreEventKind.CardCast) c.Cards.Add(e);
            });
            Assert.AreEqual(cardPaths.Length, c.Driver.Definition.Cards.Length, "카드가 정의표에 안 들어갔다");
            yield return null;
        }

        // 씬에 카드 화면이 아직 배선되지 않았으면 세운다(부착 조준 포커스 저작도 건다 — 포커스 경로를 같이 태운다).
        private static void EnsureCardViews(Wassup.UI.DreamcatcherFocusConfig focus)
        {
            if (Object.FindAnyObjectByType<CoreAwakeningGaugeView>() == null)
                new GameObject("TestJarDock").AddComponent<CoreAwakeningGaugeView>();
            if (Object.FindAnyObjectByType<CoreHandView>() != null) return;
            var go = new GameObject("TestCardHand");
            go.SetActive(false);
            var hand = go.AddComponent<CoreHandView>();
            typeof(CoreHandView).GetField("_focusConfig", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hand, focus);
            go.SetActive(true);
        }

        private static IEnumerator PlaceAndActivate(Ctx c, System.Action<SimEntityId> found)
        {
            Assert.IsTrue(TryFindPlaceable(c.Driver, out int defIndex, out int2 anchor), "놓을 곳이 없다");
            Assert.IsTrue(c.Driver.Apply(Command.PlaceDefender(defIndex, anchor)).Accepted);
            var id = LastOfKind(c.Driver, UnitKind.Defender);
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

        private static IEnumerator WaitFor(System.Func<bool> cond, float seconds)
        {
            float t = 0f;
            while (!cond() && t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
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

        private static Vector2 EnemyScreen(Ctx c, SimEntityId enemy)
        {
            var cam = c.Hand.MainCamera;
            var u = c.Driver.Find(enemy);
            var p = cam.WorldToScreenPoint((Vector3)Wassup.Core.BoardSpace.ToView(u.Position));
            return new Vector2(p.x, p.y);
        }

        private static int CountKind(Ctx c, CoreEventKind kind)
        {
            int n = 0;
            foreach (var e in c.Cards) if (e.Kind == kind) n++;
            return n;
        }

        private static SimEntityId LastOfKind(BattleDriver driver, UnitKind kind)
        {
            var units = driver.Match.World.Units;
            for (int i = units.Count - 1; i >= 0; i--)
                if (units[i].Kind == kind) return units[i].Id;
            Assert.Fail("판에 그 종류의 개체가 없다: " + kind);
            return SimEntityId.None;
        }

        private static bool TryFindFreeCell(BattleDriver driver, out int defIndex, out int2 cell)
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
                    if (placement.CellStateAt(c) != PlacementService.CellState.Free) continue;
                    defIndex = i;
                    cell = c;
                    return true;
                }
            }
            defIndex = -1;
            cell = default;
            return false;
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
