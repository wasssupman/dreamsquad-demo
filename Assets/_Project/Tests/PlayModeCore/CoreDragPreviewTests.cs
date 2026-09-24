using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Hud;
using Wassup.BattleCoreUnity.Input;
using Wassup.BattleCoreUnity.View;
using Wassup.Presentation;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild 5b 수정 — **끄는 동안 판 위에 그 유닛의 그림이 서는가.**
    //
    // 사용자 플레이 2차의 문장: 「배치 중에 하이라이트 타일 위로 배치할 유닛이 drag 모션을 하면서
    // 실루엣이 나와야 하는데 현재 미노출」.
    //
    // 증상이 「끄는 동안」이라 **입력이 실루엣을 미는 경로**가 재현 대상이다 — 프리젠터만 옳아도
    // 입력이 안 밀면 화면엔 안 나온다. 그래서 프리젠터를 직접 부르지 않고, 포인터 제스처가 부르는
    // **입력의 창구**(`BeginPress` → `StepDrag` 매 프레임 → `Release`)를 탄다.
    //
    // ⚠ 가상 Input System 마우스로 흘리지 않는다. 처음엔 그렇게 썼는데 **에디터 포커스·다른
    // 포인터 사용자에 따라 이벤트가 안 흘러** 머신마다 빨강/초록이 갈렸다(리드 실행에서 2건 빨강).
    public sealed class CoreDragPreviewTests
    {
        private DragPlacementInput _input;
        private int _defIndex;
        private Vector2 _finger;

        [UnityTest]
        public IEnumerator 드래그하면_실루엣이_하나_서고_손끝을_따라가며_판_밖에서_사라진다()
        {
            CoreSceneFixture.BeginErrorWatch();
            yield return Boot(out var ctx);
            var c = ctx();

            // ① 트레이 칸을 누르고 판으로 끈다.
            Press(c.SlotScreen);
            Vector2 a = c.BoardScreen(-2);
            yield return DragTo(a);
            yield return Hold(4);

            Assert.IsTrue(c.Input.IsDragging, "드래그로 승격되지 않았다 — 입력 경로가 안 탔다");
            Assert.IsTrue(c.Preview.IsShowing, "끄는 동안 판 위에 유닛 실루엣이 없다(사용자 증상)");
            Assert.AreEqual(1, ActiveSilhouettes(c.Preview), "실루엣은 정확히 하나여야 한다");

            // ② 정렬 — 하이라이트(못 놓는 칸)·고스트 칸보다 **위**.
            Assert.Greater(c.Preview.RenderSortingOrder, BoardSortOrder.PlacementHighlightOrder,
                "실루엣이 배치 하이라이트 밑에 깔린다");
            Assert.Greater(c.Preview.RenderSortingOrder, MaxOverlayOrder(c.Overlay),
                "실루엣이 고스트 칸과 같거나 밑이다 — 발이 칸에 덮인다");

            // ③ 손끝을 따라간다 — 목표가 바뀌고 그림이 그 목표로 수렴한다.
            Vector3 targetA = c.Preview.TargetViewPos;
            AssertNearFingerX(c, a, targetA);
            Vector2 b = c.BoardScreen(+2);
            yield return DragTo(b);
            // 칸 확정은 시간 스로틀(`_snapIntervalSec`)을 탄다 — 손가락이 멈춘 뒤 한 박자 늦게 마지막
            // 칸이 들어온다. 그래서 목표를 **매 프레임 다시 읽고** 그 목표로 수렴하는지를 본다.
            for (int i = 0; i < 90 && Vector3.Distance(c.Preview.Current.position, c.Preview.TargetViewPos) > 0.02f; i++)
                yield return Hold(1);
            Vector3 targetB = c.Preview.TargetViewPos;
            Assert.AreNotEqual(targetA, targetB, "손끝을 옮겼는데 실루엣 목표가 그대로다");
            AssertNearFingerX(c, b, targetB);
            Assert.Less(Vector3.Distance(c.Preview.Current.position, targetB), 0.02f,
                "실루엣이 손끝 칸으로 수렴하지 않는다");

            // ④ 판 밖 = 숨김(재진입하면 다시 선다).
            yield return DragTo(new Vector2(2f, Screen.height - 2f));
            yield return Hold(3);
            Assert.IsFalse(c.Preview.IsShowing, "판 밖으로 나갔는데 실루엣이 남아 있다");
            yield return DragTo(b);
            yield return Hold(3);
            Assert.IsTrue(c.Preview.IsShowing, "판으로 돌아왔는데 실루엣이 다시 안 선다");

            // ⑤ 판 밖에서 뗌 = 취소 → 사라진다(커맨드도 없다).
            yield return DragTo(new Vector2(2f, Screen.height - 2f));
            yield return Release(new Vector2(2f, Screen.height - 2f));
            Assert.AreEqual(0, ActiveSilhouettes(c.Preview), "취소했는데 실루엣이 남았다");
            Assert.AreEqual(0, c.Flight.FlightCount, "취소인데 비행이 떴다");

            CoreSceneFixture.EndErrorWatch();
            Assert.AreEqual(0, CoreSceneFixture.Errors.Count,
                "콘솔 에러: " + string.Join(" | ", CoreSceneFixture.Errors));
        }

        [UnityTest]
        public IEnumerator 판_위에서_떼면_실루엣은_사라지고_유닛은_트레이_칸에서_난다()
        {
            yield return Boot(out var ctx);
            var c = ctx();

            Press(c.SlotScreen);
            // 판정 포인터는 손가락보다 오프셋만큼 위다 — 합격 앵커 칸에 떨어지도록 그만큼 내려 잡는다.
            Vector2 at = c.BoardScreen(0) - new Vector2(0f, PointerOffsetPx(c.Input));
            yield return DragTo(at);
            yield return Hold(4);
            Assert.IsTrue(c.Preview.IsShowing, "끄는 동안 실루엣이 없다");

            yield return Release(at);
            Assert.AreEqual(0, ActiveSilhouettes(c.Preview), "드롭했는데 실루엣이 남았다 — 착지 유닛과 둘이 보인다");
            // 옛 결정(unit 9 — 「실루엣 모드는 손끝에 유닛이 없다」): 비행은 트레이 칸에서 뜬다.
            Assert.AreEqual(1, c.Flight.FlightCount, "판 위 드롭인데 배치 비행이 안 떴다");
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

        private sealed class Ctx
        {
            public BattleDriver Driver;
            public DragPlacementInput Input;
            public CoreDragPreviewPresenter Preview;
            public CoreDeployFlightPresenter Flight;
            public CoreMapOverlay Overlay;
            public Camera Cam;
            public Vector2 SlotScreen;
            public int2 Anchor;
            public float TilePx;

            // 판정 앵커 칸의 화면 자리에서 `dx` 칸 옆. 오프셋은 세로라 가로 비교에 영향이 없다.
            public Vector2 BoardScreen(int dx)
            {
                float ts = Driver.TileSize;
                var w = (Vector3)Wassup.Core.BoardSpace.ToView(new float3((Anchor.x + dx) * ts, 0f, Anchor.y * ts));
                return Cam.WorldToScreenPoint(w);
            }
        }

        private IEnumerator Boot(out System.Func<Ctx> ctx)
        {
            var box = new Ctx();
            ctx = () => box;
            return BootInto(box);
        }

        private IEnumerator BootInto(Ctx c)
        {
            yield return CoreSceneFixture.LoadAndBoot(d => c.Driver = d);
            Assert.IsNotNull(c.Driver, "BattleCoreScene 에 BattleDriver 가 없다");
            c.Input = Object.FindAnyObjectByType<DragPlacementInput>();
            c.Preview = Object.FindAnyObjectByType<CoreDragPreviewPresenter>();
            c.Flight = Object.FindAnyObjectByType<CoreDeployFlightPresenter>();
            c.Overlay = Object.FindAnyObjectByType<CoreMapOverlay>();
            var tray = Object.FindAnyObjectByType<CoreDefenderTray>();
            c.Cam = Camera.main;
            Assert.IsNotNull(c.Input);
            Assert.IsNotNull(c.Preview, "드래그 실루엣 프리젠터가 씬에 없다");
            Assert.IsNotNull(c.Flight);
            Assert.IsNotNull(c.Overlay);
            Assert.IsNotNull(tray);
            Assert.IsNotNull(c.Cam);

            c.Driver.Apply(Command.FinishPlacement());
            yield return null;

            // 판 가운데쯤, 좌우 두 칸이 전부 판 안인 합격 앵커를 가진 로스터 유닛.
            Assert.IsTrue(TryFindPlaceable(c.Driver, out _defIndex, out c.Anchor),
                "로스터의 어떤 유닛도 이 판에 놓을 수 없다");
            bool slot = false;
            for (int i = 0; i < 20 && !(slot = tray.TryGetSlotScreenCenter(_defIndex, null, out c.SlotScreen)); i++)
                yield return null;
            Assert.IsTrue(slot, "트레이에 그 유닛의 칸이 없다");
            c.TilePx = Mathf.Abs(c.BoardScreen(1).x - c.BoardScreen(0).x);

            _input = c.Input;
            // 입력 자신의 `Update` 는 **실제 포인터**를 읽는다 — 포인터 장치가 없으면(배치 러너)
            // 매 프레임 제스처를 끝내고, 누가 진짜 마우스를 클릭하면 뗌으로 읽는다. 제스처를
            // 이 테스트가 몰고 있으니 그 폴링을 끈다(`OnDisable` 의 정리는 아직 빈 상태라 무해).
            // 사건 구독도 같이 내려가므로 **떼는 순간 다시 켠다** — `Release`.
            _input.enabled = false;
        }

        // 트레이 칸을 누른다(칸 판정은 테스트가 이미 했다 — 그 칸의 화면 중심).
        private void Press(Vector2 p)
        {
            _finger = p;
            _input.BeginPress(_defIndex, p);
        }

        private IEnumerator Release(Vector2 p)
        {
            _finger = p;
            // 배치가 성사되면 `Placed` 를 받아 비행을 띄우는 것은 입력의 구독이다 — 켠 **같은
            // 프레임**에 뗀다(그 프레임의 `Update` 는 이미 지나갔다).
            _input.enabled = true;
            _input.Release(p);
            yield return null;
        }

        // 임계(16px)와 오프셋 램프를 넘도록 여러 프레임에 걸쳐 끈다 — 프레임마다 한 걸음.
        private IEnumerator DragTo(Vector2 to)
        {
            Vector2 from = _finger;
            const int steps = 8;
            for (int i = 1; i <= steps; i++)
            {
                _finger = Vector2.Lerp(from, to, i / (float)steps);
                _input.StepDrag(_finger);
                yield return null;
            }
        }

        // 누른 채 멈춰 있다. 실제 손가락도 멈춘 동안 매 프레임 `StepDrag` 를 받는다 —
        // 칸 확정 스로틀은 그 호출에서만 진행한다.
        private IEnumerator Hold(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                _input.StepDrag(_finger);
                yield return null;
            }
        }

        private static float PointerOffsetPx(DragPlacementInput input)
        {
            var f = typeof(DragPlacementInput).GetField("_pointerOffsetPx",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(f, "입력의 포인터 오프셋 칸 이름이 바뀌었다");
            return (float)f.GetValue(input);
        }

        private static int ActiveSilhouettes(CoreDragPreviewPresenter preview)
        {
            int n = 0;
            foreach (Transform child in preview.transform)
                if (child.gameObject.activeSelf) n++;
            return n;
        }

        private static int MaxOverlayOrder(CoreMapOverlay overlay)
        {
            int max = int.MinValue;
            foreach (var sr in overlay.GetComponentsInChildren<SpriteRenderer>(false))
                if (sr.enabled) max = Mathf.Max(max, sr.sortingOrder);
            Assert.AreNotEqual(int.MinValue, max, "고스트 칸이 하나도 안 보인다 — 비교 대상이 없다");
            return max;
        }

        private static void AssertNearFingerX(Ctx c, Vector2 finger, Vector3 target)
        {
            float x = c.Cam.WorldToScreenPoint(target).x;
            Assert.Less(Mathf.Abs(x - finger.x), c.TilePx * 1.5f,
                "실루엣이 손끝 칸이 아닌 곳에 선다");
        }

        private static bool TryFindPlaceable(BattleDriver driver, out int defIndex, out int2 anchor)
        {
            var placement = driver.Match.Placement;
            var size = driver.GridSize;
            int cx = size.x / 2, cy = size.y / 2;
            for (int i = 0; i < driver.Definition.Units.Length; i++)
            {
                if (!placement.InRoster(i)) continue;
                // 판 가운데에서 바깥으로 — 좌우로 두 칸씩 끌어도 판 안이다.
                for (int r = 0; r < math.max(size.x, size.y); r++)
                for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 3 || y < 1 || x >= size.x - 3 || y >= size.y - 2) continue;
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
