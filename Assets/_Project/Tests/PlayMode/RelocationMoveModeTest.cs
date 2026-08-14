using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // defender-relocation unit 5 — 이동모드 진입/취소/쿨다운/타임아웃 검증. 진입은 홀드가 아니라
    // 외부 BeginMoveModeFor(플립북 이동모드 버튼 대체). 컨트롤러를 disable 후 Step 을 reflection 으로
    // 구동해 쿨다운/타임아웃 틱을 진행한다(원격 검증 경로).
    public class RelocationMoveModeTest
    {
        static MethodInfo _stepMethod;

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        // 표면은 보드 타일이 아니라 **트레이 슬롯**이다(사용자 문장: "배치된 유닛의 **초상화**를 D&D").
        //
        // 계보: defender-board-limit 계약 5 가 소진 슬롯의 탭·드래그를 **둘 다** "판 위 그 유닛 선택"
        // 으로 묶었고 → defender-relocation unit 10 이 "드래그 = 집어들기(이동모드)" 로 갈랐다가 →
        // **defender-clock-out unit 0 이 다시 합쳤다**(이동은 퇴근으로 대체, 진입구 차단).
        //
        // 그래서 이 테스트는 지금 **배선이 끊겼다는 유일한 자동 증거**다. 이동을 되살리려면
        // DcInspectController.RelocationEnabled 를 true 로 되돌리고 아래 드래그 단정을 뒤집는다.
        [UnityTest]
        public IEnumerator ExhaustedSlot_BothGestures_GoToUnit_NotMoveMode()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var reloc = Object.FindObjectOfType<DefenderRelocationController>();
            reloc.enabled = false; // 진입만 본다 — 목적지 제스처는 이 테스트 소관이 아니다

            var fast = ScriptableObject.CreateInstance<RelocationSettings>();
            fast.entryCooldownSeconds = 0f;
            fast.moveModeTimeoutSeconds = 30f;
            SetField(reloc, "settings", fast);

            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            Assert.AreEqual(1, unit.EffectiveMaxOnBoard, "상한 1 이어야 1기 배치로 슬롯이 소진된다");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            // 트레이 슬롯은 GameManager 의 페이즈 전환이 만든다(bridge.BeginPlacement 로는 안 생긴다).
            gm.SetPhase(GamePhase.Placement);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place defender — 이제 슬롯이 소진 상태");
            gm.SetPhase(GamePhase.Battle);
            yield return null;

            var slot = FindSlotFor(unit);
            Assert.IsNotNull(slot, "레인저 트레이 슬롯");

            var es = UnityEngine.EventSystems.EventSystem.current;
            Assert.IsNotNull(es, "EventSystem");
            var ped = new UnityEngine.EventSystems.PointerEventData(es)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, slot.transform.position),
            };

            // defender-clock-out unit 0 — 이동 진입구가 끊겼다. 두 제스처가 **다시 합쳐진다**
            // (board-limit 계약 5 로 복귀: 소진 셀의 모든 제스처 = 판 위 그 유닛 선택).
            // 이 단정이 곧 "배선이 실제로 끊겼다"는 유일한 증거다. 이동을 되살릴 때 여기를 뒤집는다.
            slot.OnPointerClick(ped);
            Assert.IsFalse(reloc.InMoveMode, "탭은 데려가기다");

            slot.OnBeginDrag(ped);
            Assert.IsFalse(reloc.InMoveMode, "드래그도 데려가기다 — 이동 진입구가 꺼져 있다");

            Object.Destroy(fast);
        }

        // unit 10 — 코스트가 모자라면 이동모드에 **들어가지 못한다**. 들여보내면 슬로모까지 걸고
        // 아무 칸에도 못 놓는 상태가 되어, 보드 밖 탭이나 타임아웃으로만 빠져나올 수 있다.
        // 선택 패널의 "이동" 버튼 잠금이 이 술어를 그대로 읽는다.
        [UnityTest]
        public IEnumerator MoveModeEntry_GatedByCost()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var controller = Object.FindObjectOfType<DefenderRelocationController>();
            controller.enabled = false;

            var fast = ScriptableObject.CreateInstance<RelocationSettings>();
            fast.entryCooldownSeconds = 0f;
            fast.moveModeTimeoutSeconds = 30f;
            SetField(controller, "settings", fast);

            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            Assert.Greater(unit.cost, 0, "코스트 게이트를 보려면 유닛 코스트가 0 이 아니어야 한다");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place defender");
            gm.SetPhase(GamePhase.Battle);
            yield return null;

            var cell = SoleCell(bridge);
            Assert.IsTrue(bridge.TryGetDefenderAt(cell, out var entity, out _, out _), "resolve entity");

            // 코스트를 바닥낸다 — 재생성이 끼지 않게 멈춘다(Battle 진입이 켜 놓는다).
            gm.CostRuntime.StopRegen();
            while (gm.CostRuntime.CurrentInt > 0) gm.CostRuntime.TrySpend(1);

            Assert.IsFalse(controller.CanBeginMoveModeFor(entity, cell), "코스트가 없으면 진입 불가");
            Assert.IsFalse(controller.BeginMoveModeFor(entity, cell), "게이트가 실제 진입도 막는다");
            Assert.IsFalse(controller.InMoveMode, "이동모드에 들어가지 않았다");

            gm.CostRuntime.AddCost(unit.cost);
            Assert.IsTrue(controller.CanBeginMoveModeFor(entity, cell), "코스트가 차면 다시 열린다");
            Assert.IsTrue(controller.BeginMoveModeFor(entity, cell), "진입 성공");

            controller.CancelMoveMode();
            Object.Destroy(fast);
        }

        // 버그 재현 — 사용자 문장: "재배치 모드 들어갈 때 배치 가능 타일 하이라이트가 동작 안 한다".
        // 단언은 그 문장 그대로다: **이동모드에 있는 동안 하이라이트가 켜져 있다.**
        // 진입 직후 1프레임이 핵심이다 — 재배치는 진입 시 한 번만 켜는데, 배치 드래그 컨트롤러는
        // 매 프레임 하이라이트를 자기 상태로 되돌린다. 프레임이 흐르지 않으면 증상이 안 보인다.
        [UnityTest]
        public IEnumerator MoveMode_KeepsPlacementHighlight_AcrossFrames()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var controller = Object.FindObjectOfType<DefenderRelocationController>();
            controller.enabled = false; // 재배치의 Update 는 막고 — 배치 컨트롤러 Update 는 살려둔다(그게 범인 후보)

            var fast = ScriptableObject.CreateInstance<RelocationSettings>();
            fast.entryCooldownSeconds = 0.5f;
            fast.moveModeTimeoutSeconds = 30f;
            SetField(controller, "settings", fast);

            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            // 트레이 슬롯은 **GameManager 의 페이즈 전환**이 만든다(bridge.BeginPlacement 로는 안 생긴다).
            // 아래 자기치유 검증이 슬롯 하나를 필요로 해서 여기서 태운다.
            gm.SetPhase(GamePhase.Placement);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place defender");
            gm.SetPhase(GamePhase.Battle);
            yield return null;

            var cell = SoleCell(bridge);
            Assert.IsTrue(bridge.TryGetDefenderAt(cell, out var entity, out _, out _), "resolve entity");
            Assert.IsTrue(controller.BeginMoveModeFor(entity, cell), "enter move mode");

            Assert.IsTrue(bridge.IsPlacementHighlightShown, "진입 프레임엔 하이라이트가 켜진다");

            // 프레임을 흘린다 — 여기서 꺼지면 누군가 매 프레임 되돌리고 있는 것이다.
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(controller.InMoveMode, "여전히 이동모드 (전제)");
            Assert.IsTrue(bridge.IsPlacementHighlightShown,
                "이동모드가 유지되는 동안 하이라이트도 유지돼야 한다 — 꺼졌다면 배치 컨트롤러가 되돌린 것");

            controller.CancelMoveMode();
            Assert.IsFalse(bridge.IsPlacementHighlightShown, "이동모드를 나가면 하이라이트도 꺼진다");

            // 반대 방향(자기치유)도 함께 잠근다 — 이걸 깨면서 위를 고치기 쉽기 때문이다.
            // placement-mask unit 4(588a99c4)가 넣은 계약: **배치 쪽이 하이라이트를 원하는 동안**
            // 누가 밖에서 꺼도 배치 컨트롤러가 되살린다.
            //
            // ⚠ arm 을 반사로 세울 땐 `_armedUnit` 과 `_armedSlot` 을 **둘 다** 세워야 한다.
            // UpdateBoardGesture 첫 줄이 `_armedSlot == null → Disarm()` 이라, 유닛만 세우면
            // 다음 프레임에 arm 이 조용히 풀려 이 테스트가 엉뚱한 실패를 낸다(실제로 그랬다).
            var drag = Object.FindObjectOfType<DefenderDragPlacementController>();
            Assert.IsNotNull(drag, "DefenderDragPlacementController wired in scene");
            var slot = Object.FindObjectOfType<DefenderDragSlot>(includeInactive: true);
            Assert.IsNotNull(slot, "트레이 슬롯이 있어야 arm 을 흉내낼 수 있다");
            SetField(drag, "_armedUnit", unit);
            SetField(drag, "_armedSlot", slot);
            yield return null;
            Assert.IsTrue(drag.HasArmedUnit, "arm 이 프레임을 넘겨 유지된다 (전제)");
            Assert.IsTrue(bridge.IsPlacementHighlightShown, "arm 하면 배치 하이라이트가 뜬다 (전제)");

            bridge.HidePlacementHighlight();   // 밖에서 끈다(재배치 취소가 하는 일)
            yield return null;
            Assert.IsTrue(bridge.IsPlacementHighlightShown,
                "배치가 원하는 동안 밖에서 꺼도 되살아난다 (자기치유 — 켜는 방향은 유지)");

            SetField(drag, "_armedUnit", null);
            SetField(drag, "_armedSlot", null);
            Object.Destroy(fast);
        }

        [UnityTest]
        public IEnumerator BeginMoveMode_Slomo_Cancel_Cooldown_Timeout()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var controller = Object.FindObjectOfType<DefenderRelocationController>();
            Assert.IsNotNull(controller, "DefenderRelocationController wired in scene");
            controller.enabled = false; // 실제 입력 Update 차단 — Step 을 테스트가 단독 구동(쿨다운/타임아웃 틱)

            var fast = ScriptableObject.CreateInstance<RelocationSettings>();
            fast.entryCooldownSeconds = 0.5f;
            fast.moveModeTimeoutSeconds = 1.0f;
            SetField(controller, "settings", fast);

            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place defender");
            gm.SetPhase(GamePhase.Battle); // 진입 게이트 = Battle 페이즈
            yield return null;

            var cell = SoleCell(bridge);
            Assert.IsTrue(bridge.TryGetDefenderAt(cell, out var entity, out _, out _), "resolve entity");
            Vector2 z = Vector2.zero;

            // 1) BeginMoveModeFor → 이동모드 + 슬로모
            Assert.IsTrue(controller.BeginMoveModeFor(entity, cell), "BeginMoveModeFor enters move mode");
            Assert.IsTrue(controller.InMoveMode, "in move mode");
            Assert.AreEqual(cell, controller.MoveSourceCell, "source cell captured");
            Assert.Less(TimeManager.Instance.ScaleOf(TimeDomain.Battle), 0.999f, "slowmo lease active");

            // 2) 취소 → 슬로모 해제
            controller.CancelMoveMode();
            Assert.IsFalse(controller.InMoveMode, "cancel exits move mode");
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 0.001f, "slowmo released on cancel");

            // 3) 진입 쿨다운 — 직후 재진입 거부
            Assert.IsFalse(controller.BeginMoveModeFor(entity, cell), "entry cooldown blocks immediate re-entry");
            Assert.IsFalse(controller.InMoveMode, "still not in move mode during cooldown");

            // 4) 쿨다운 소진(Step 틱) 후 재진입
            for (int i = 0; i < 8; i++) Step(controller, false, false, z, 0.1f); // 0.5s 쿨다운 소진
            Assert.IsTrue(controller.BeginMoveModeFor(entity, cell), "re-entry after cooldown");
            Assert.IsTrue(controller.InMoveMode, "re-entered");

            // 5) 타임아웃 자동 취소
            for (int i = 0; i < 12; i++) Step(controller, false, false, z, 0.1f); // >1.0s 누적
            Assert.IsFalse(controller.InMoveMode, "timeout auto-cancels move mode");
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 0.001f, "slowmo released on timeout");

            Object.Destroy(fast);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        // 트레이 슬롯은 런타임 생성이라 이름으로 못 찾는다 — 바인딩된 유닛으로 고른다.
        private static DefenderDragSlot FindSlotFor(DefenderUnitData unit)
        {
            var f = typeof(DefenderDragSlot).GetField("_unitData",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "_unitData seam");
            foreach (var s in Object.FindObjectsOfType<DefenderDragSlot>(true))
                if (ReferenceEquals(f.GetValue(s), unit)) return s;
            return null;
        }

        private static void Step(DefenderRelocationController c, bool pressStarted, bool pressed, Vector2 screen, float dt)
        {
            _stepMethod ??= typeof(DefenderRelocationController)
                .GetMethod("Step", BindingFlags.NonPublic | BindingFlags.Instance);
            _stepMethod.Invoke(c, new object[] { pressStarted, pressed, screen, dt });
        }

        private static void SetField(object obj, string name, object value)
            => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
        }

        private static Vector2Int SoleCell(BattleBridge bridge)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (System.Collections.DictionaryEntry de in (System.Collections.IDictionary)f.GetValue(bridge))
                return (Vector2Int)de.Key;
            return new Vector2Int(int.MinValue, int.MinValue);
        }
    }
}
