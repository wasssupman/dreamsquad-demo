using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    // camera-direction unit 10 — 상태 레시피 → 절대 포즈.
    //
    // 투영 검증에는 실제 Camera 를 쓴다. 손으로 쓴 투영 헬퍼로 검사하면 구현과 같은 실수를
    // 공유해 통과해버릴 수 있다 — 화면에 실제로 그리는 주체에게 물어야 증거가 된다.
    public class CameraStatePoseTests
    {
        private const float Fov = 35.9834f;
        private const float Aspect16x9 = 16f / 9f;
        private const float Aspect195x9 = 19.5f / 9f;

        private Camera _probe;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("__CameraStatePoseProbe");
            _probe = go.AddComponent<Camera>();
            _probe.enabled = false; // 렌더링은 필요 없다 — 투영만 쓴다
        }

        [TearDown]
        public void TearDown()
        {
            if (_probe != null) Object.DestroyImmediate(_probe.gameObject);
        }

        private static Bounds Board(float w = 21f, float d = 12f)
            => new Bounds(Vector3.zero, new Vector3(w, 0f, d));

        private static CameraStateFraming Framing(
            float pitchDeg, float screenY, bool fitToBoard = true,
            float fitMargin = 1f, float fixedDistance = 20f, float fov = Fov)
            => new CameraStateFraming
            {
                state = CameraState.Battle,
                pitchDeg = pitchDeg,
                fitToBoard = fitToBoard,
                fitMargin = fitMargin,
                fixedDistance = fixedDistance,
                screenY = screenY,
                fov = fov,
            };

        private Vector3 Viewport(Vector3 pos, Quaternion rot, float fov, float aspect, Vector3 world)
        {
            _probe.transform.SetPositionAndRotation(pos, rot);
            _probe.fieldOfView = fov;
            _probe.aspect = aspect;
            return _probe.WorldToViewportPoint(world);
        }

        [Test]
        public void SolveStatePose_ScreenYHalf_PutsTargetAtViewportCenter()
        {
            var target = new Vector3(0f, 0f, 0f);
            Assert.IsTrue(CameraFramingMath.SolveStatePose(
                target, Framing(47f, 0.5f), Board(), Aspect16x9,
                out var pos, out var rot, out float fov));

            var v = Viewport(pos, rot, fov, Aspect16x9, target);
            Assert.AreEqual(0.5f, v.y, 1e-3f, "대상이 화면 세로 정중앙에 와야 한다");
            Assert.AreEqual(0.5f, v.x, 1e-3f, "yaw 가 없으므로 가로도 정중앙이다");
        }

        [Test]
        public void SolveStatePose_ScreenYAboveHalf_PutsTargetAboveCenter()
        {
            var target = Vector3.zero;
            Assert.IsTrue(CameraFramingMath.SolveStatePose(
                target, Framing(47f, 0.62f), Board(), Aspect16x9,
                out var pos, out var rot, out float fov));

            var v = Viewport(pos, rot, fov, Aspect16x9, target);
            Assert.AreEqual(0.62f, v.y, 1e-3f, "screenY 가 곧 대상의 화면 세로 위치다");
        }

        [Test]
        public void SolveStatePose_ScreenYShift_KeepsViewDepthToTarget()
        {
            // 판의 «크기» 를 정하는 것은 유클리드 거리가 아니라 카메라 정면 축 깊이다.
            // 로컬 up 으로 밀면 깊이가 보존돼 대상이 화면에서 자리만 옮긴다.
            // 월드 Y 로 밀면 깊이까지 변해 판이 커졌다 작아졌다 한다 — 그걸 여기서 잡는다.
            var target = Vector3.zero;
            CameraFramingMath.SolveStatePose(target, Framing(47f, 0.5f), Board(), Aspect16x9,
                out var centered, out var rotA, out float fovA);
            CameraFramingMath.SolveStatePose(target, Framing(47f, 0.62f), Board(), Aspect16x9,
                out var shifted, out var rotB, out float fovB);

            float depthCentered = Viewport(centered, rotA, fovA, Aspect16x9, target).z;
            float depthShifted = Viewport(shifted, rotB, fovB, Aspect16x9, target).z;

            Assert.Greater(depthCentered, 0f);
            Assert.AreEqual(depthCentered, depthShifted, 1e-3f,
                "화면 세로 위치를 옮겨도 시야 깊이는 불변이어야 한다 — 월드 Y 로 밀면 여기서 깨진다");
        }

        [Test]
        public void SolveStatePose_ScreenYShift_KeepsBoardApparentWidth()
        {
            var target = Vector3.zero;
            var edgeL = new Vector3(-10.5f, 0f, 0f);
            var edgeR = new Vector3(10.5f, 0f, 0f);

            CameraFramingMath.SolveStatePose(target, Framing(47f, 0.5f), Board(), Aspect16x9,
                out var centered, out var rotA, out float fovA);
            CameraFramingMath.SolveStatePose(target, Framing(47f, 0.62f), Board(), Aspect16x9,
                out var shifted, out var rotB, out float fovB);

            float wCentered = Viewport(centered, rotA, fovA, Aspect16x9, edgeR).x
                            - Viewport(centered, rotA, fovA, Aspect16x9, edgeL).x;
            float wShifted = Viewport(shifted, rotB, fovB, Aspect16x9, edgeR).x
                           - Viewport(shifted, rotB, fovB, Aspect16x9, edgeL).x;

            Assert.AreEqual(wCentered, wShifted, 1e-3f, "판의 화면 크기가 흔들리면 안 된다");
        }

        [Test]
        public void SolveStatePose_SameRecipe_GivesSameTargetViewportY_AcrossAspects()
        {
            var target = Vector3.zero;
            var framing = Framing(47f, 0.5543f);

            CameraFramingMath.SolveStatePose(target, framing, Board(), Aspect16x9,
                out var posWide, out var rotWide, out float fovWide);
            CameraFramingMath.SolveStatePose(target, framing, Board(), Aspect195x9,
                out var posTall, out var rotTall, out float fovTall);

            float yWide = Viewport(posWide, rotWide, fovWide, Aspect16x9, target).y;
            float yTall = Viewport(posTall, rotTall, fovTall, Aspect195x9, target).y;

            Assert.AreEqual(yWide, yTall, 1e-3f, "구도는 화면비와 무관해야 한다 (unit 9 의 교훈)");
        }

        [Test]
        public void SolveStatePose_FitToBoard_PutsEveryBoardCornerInsideFrustum()
        {
            var board = Board();
            Assert.IsTrue(CameraFramingMath.SolveStatePose(
                Vector3.zero, Framing(70f, 0.5f, fitMargin: 1.05f), board, Aspect16x9,
                out var pos, out var rot, out float fov));

            var e = board.extents;
            var corners = new[]
            {
                new Vector3(-e.x, 0f, -e.z), new Vector3(e.x, 0f, -e.z),
                new Vector3(-e.x, 0f,  e.z), new Vector3(e.x, 0f,  e.z),
            };
            foreach (var c in corners)
            {
                var v = Viewport(pos, rot, fov, Aspect16x9, c);
                Assert.Greater(v.z, 0f, "코너가 카메라 뒤에 있으면 안 된다");
                Assert.That(v.x, Is.InRange(0f, 1f), $"코너 {c} 가 가로로 화면 밖이다");
                Assert.That(v.y, Is.InRange(0f, 1f), $"코너 {c} 가 세로로 화면 밖이다");
            }
        }

        [Test]
        public void SolveStatePose_BiggerBoard_PullsCameraBack_WhenFitting()
        {
            CameraFramingMath.SolveStatePose(Vector3.zero, Framing(47f, 0.5f), Board(21f, 12f), Aspect16x9,
                out var near, out _, out _);
            CameraFramingMath.SolveStatePose(Vector3.zero, Framing(47f, 0.5f), Board(30f, 18f), Aspect16x9,
                out var far, out _, out _);

            Assert.Greater(far.magnitude, near.magnitude, "판이 커지면 더 물러나야 한다");
        }

        [Test]
        public void SolveStatePose_FixedDistance_IgnoresBoardSize()
        {
            var framing = Framing(70f, 0.5f, fitToBoard: false, fixedDistance: 14f);
            CameraFramingMath.SolveStatePose(Vector3.zero, framing, Board(21f, 12f), Aspect16x9,
                out var small, out _, out _);
            CameraFramingMath.SolveStatePose(Vector3.zero, framing, Board(30f, 18f), Aspect16x9,
                out var big, out _, out _);

            Assert.AreEqual(14f, small.magnitude, 1e-3f);
            Assert.AreEqual(small, big, "fit 을 안 쓰면 판 크기가 포즈를 바꾸지 않는다");
        }

        [Test]
        public void SolveStatePose_TargetOffCenter_MovesPoseWithTarget()
        {
            var framing = Framing(70f, 0.5f, fitToBoard: false, fixedDistance: 14f);
            CameraFramingMath.SolveStatePose(Vector3.zero, framing, Board(), Aspect16x9,
                out var atCenter, out _, out _);
            var target = new Vector3(3f, 0f, -2f);
            CameraFramingMath.SolveStatePose(target, framing, Board(), Aspect16x9,
                out var atTarget, out var rot, out float fov);

            Assert.AreEqual(target, atTarget - atCenter, "대상이 옮겨간 만큼 포즈도 평행이동한다");
            var v = Viewport(atTarget, rot, fov, Aspect16x9, target);
            Assert.AreEqual(0.5f, v.y, 1e-3f);
        }

        [Test]
        public void SolveStatePose_NullFraming_ReturnsFalse()
        {
            Assert.IsFalse(CameraFramingMath.SolveStatePose(
                Vector3.zero, null, Board(), Aspect16x9, out _, out _, out _),
                "레시피가 없는 상태로는 전환하지 않는다 — 호출부가 현재 포즈를 유지한다");
        }

        // 2026-08-21 사용자가 씬에서 손으로 잡은 전투 포즈를 **수식이 재현하는지** 고정한다.
        // 여기 리터럴은 에셋 값이 아니라 «이 입력 → 이 출력» 이라는 산식의 기준점이다 —
        // 에셋의 저작값은 튜닝으로 계속 움직이므로(현재 pitch 50) 이 테스트가 감시 대상이
        // 아니다. 산식을 건드렸을 때 이 값이 흔들리면 회귀다.
        [Test]
        public void SolveStatePose_KnownGoodInputs_ReproduceReferencePose()
        {
            var framing = Framing(47f, 0.5543f, fitMargin: 1.0058f);
            Assert.IsTrue(CameraFramingMath.SolveStatePose(
                Vector3.zero, framing, Board(21f, 12f), Aspect16x9,
                out var pos, out var rot, out float fov));

            Assert.AreEqual(0f, pos.x, 1e-3f);
            Assert.AreEqual(15.849f, pos.y, 0.01f);
            Assert.AreEqual(-15.860f, pos.z, 0.01f);
        }

        // camera-direction unit 12 — 배치 커서 추종. PanDelta 는 회전을 건드리지 않고 화면을
        // 커서 쪽으로 민다. 주석이 주장하는 "lead 1 이면 커서 지점이 화면 중앙" 을 고정한다.
        [Test]
        public void PanDelta_LeadOne_PutsCursorPointAtScreenCenter()
        {
            var framing = Framing(70f, 0.5f, fitMargin: 1.15f);
            var board = Board();
            Assert.IsTrue(CameraFramingMath.SolveStatePose(Vector3.zero, framing, board, Aspect16x9,
                out var pos, out var rot, out float fov));
            float depth = Vector3.Dot(board.center - pos, rot * Vector3.forward);

            // 화면 오른쪽 40% 지점(ndc.x 0.4)을 가리키는 커서 아래의 월드 점을 먼저 구한다.
            var cursorNdc = new Vector2(0.4f, -0.3f);
            var before = Viewport(pos, rot, fov, Aspect16x9, Vector3.zero);
            Assert.That(before.y, Is.EqualTo(0.5f).Within(1e-3f));

            var d = CameraComposeMath.PanDelta(cursorNdc, fov, Aspect16x9, 1f, 1f, depth);
            var panned = pos + rot * d.localPos;
            // 팬 이전에 cursorNdc 에 있던 월드 점이 이제 화면 중앙에 와야 한다.
            var v = Viewport(panned, rot, fov, Aspect16x9, WorldAtNdc(pos, rot, fov, Aspect16x9, cursorNdc, depth));
            Assert.AreEqual(0.5f, v.x, 2e-3f);
            Assert.AreEqual(0.5f, v.y, 2e-3f);
        }

        [Test]
        public void PanDelta_ZeroLead_DoesNotMoveCamera()
        {
            var d = CameraComposeMath.PanDelta(new Vector2(1f, 1f), Fov, Aspect16x9, 1f, 0f, 20f);
            Assert.AreEqual(Vector3.zero, d.localPos);
        }

        // 저작값 계약 — unit 12 완료 기준 "판 네 귀퉁이가 화면 안에 남는다".
        // 리터럴을 박지 않고 **에셋의 실제 값**으로 성질을 검사한다: 배치 fit 여유보다 팬 폭이
        // 크면 손가락을 끝으로 끌 때 반대쪽 판이 화면 밖으로 나간다.
        [Test]
        public void AuthoredPlacementLead_KeepsBoardOnScreen_AtFullPan()
        {
            var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<CameraDirectionConfig>(
                "Assets/_Project/Data/Camera/CameraDirectionConfig.asset");
            Assert.IsNotNull(cfg, "카메라 config 에셋을 찾지 못했다");
            CameraStateFraming placement = null;
            foreach (var f in cfg.stateFramings)
                if (f != null && f.state == CameraState.Placement) placement = f;
            Assert.IsNotNull(placement, "배치 상태 레시피가 등록돼 있어야 한다");

            var board = Board();
            Assert.IsTrue(CameraFramingMath.SolveStatePose(Vector3.zero, placement, board, Aspect16x9,
                out var pos, out var rot, out float fov));
            float depth = Vector3.Dot(board.center - pos, rot * Vector3.forward);
            var d = CameraComposeMath.PanDelta(new Vector2(1f, 0f), fov, Aspect16x9, 1f,
                cfg.placementFocusLead, depth);
            var panned = pos + rot * d.localPos;

            var e = board.extents;
            foreach (var c in new[]
            {
                new Vector3(-e.x, 0f, -e.z), new Vector3(e.x, 0f, -e.z),
                new Vector3(-e.x, 0f,  e.z), new Vector3(e.x, 0f,  e.z),
            })
            {
                var v = Viewport(panned, rot, fov, Aspect16x9, c);
                Assert.That(v.x, Is.InRange(-0.02f, 1.02f),
                    $"placementFocusLead {cfg.placementFocusLead} 가 배치 fitMargin {placement.fitMargin} 의 여유보다 크다 — 판이 화면 밖으로 나간다");
            }
        }

        // ndc 위치에 있는, 보드 평면 깊이의 월드 점.
        private static Vector3 WorldAtNdc(Vector3 pos, Quaternion rot, float fovDeg, float aspect,
                                          Vector2 ndc, float depth)
        {
            float tanV = Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * aspect;
            return pos + rot * new Vector3(ndc.x * tanH * depth, ndc.y * tanV * depth, depth);
        }
    }
}
