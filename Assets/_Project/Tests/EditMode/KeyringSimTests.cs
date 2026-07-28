using NUnit.Framework;
using UnityEngine;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // keyring-unify 0 — KeyringSim 순수 계산 회귀 테스트.
    // FallStep 케이스는 lobby-keyring-drag 2 의 LobbyKeyringFallStepTests 재조준.
    // SpringStep/LeanAngle 은 추출 전 인라인 수학의 전사(레퍼런스)와 bit-exact 비교로 동작 무변경을 고정.
    public class KeyringSimTests
    {
        private const float Gravity = 4000f;
        private const float BounceDamping = 0.35f;
        private const float BounceMinSpeed = 300f;
        private const float Floor = -400f;
        private const float Dt = 1f / 60f;

        [Test]
        public void HighDrop_BouncesAtLeastOnce_ThenSettlesOnFloor()
        {
            float y = Floor + 600f;
            float vy = 0f;
            bool bounced = false;
            bool landed = false;
            for (int i = 0; i < 10000 && !landed; i++)
            {
                landed = KeyringSim.FallStep(ref y, ref vy, Floor, Dt,
                    Gravity, BounceDamping, BounceMinSpeed);
                if (!landed && vy > 0f) bounced = true; // 상승 속도 = 반동 발생
            }
            Assert.IsTrue(bounced, "높은 낙하는 최소 1회 반동해야 한다");
            Assert.IsTrue(landed, "반동 후 결국 정착해야 한다");
            Assert.AreEqual(Floor, y, 1e-3f);
            Assert.AreEqual(0f, vy);
        }

        [Test]
        public void SlowImpact_SettlesImmediately_WithoutBounce()
        {
            float y = Floor + 1f; // 1px 위 — 착지 속도가 bounceMinSpeed 미만
            float vy = 0f;
            bool landed = false;
            for (int i = 0; i < 100 && !landed; i++)
            {
                landed = KeyringSim.FallStep(ref y, ref vy, Floor, Dt,
                    Gravity, BounceDamping, BounceMinSpeed);
                Assert.IsFalse(vy > 0f, "느린 착지는 반동이 없어야 한다");
            }
            Assert.IsTrue(landed);
            Assert.AreEqual(Floor, y, 1e-3f);
            Assert.AreEqual(0f, vy);
        }

        [Test]
        public void ReleaseBelowFloor_SettlesOnFloor_WithoutFlyingOff()
        {
            // 손가락을 화면 하단에 두면 캐릭터가 바닥선 아래에서 놓일 수 있다 —
            // 위로 튀지 않고 바닥선으로 정착해야 한다(상승 착지는 impact 음수 → 반동 없음).
            float y = Floor - 20f;
            float vy = 500f;
            bool landed = false;
            for (int i = 0; i < 100 && !landed; i++)
            {
                landed = KeyringSim.FallStep(ref y, ref vy, Floor, Dt,
                    Gravity, BounceDamping, BounceMinSpeed);
            }
            Assert.IsTrue(landed);
            Assert.AreEqual(Floor, y, 1e-3f);
            Assert.AreEqual(0f, vy);
        }

        // --- SpringStep: 추출 전 인라인 수학 전사 레퍼런스 ---
        // DefenderDragPlacementController(d197bc79, Update) / LobbyKeyringDrag(2643383b, TickDrag)
        // 의 연산 순서를 그대로 옮긴 것. KeyringSim 이 이와 bit-exact 이어야 "동작 무변경".

        private static void ReferenceSpringStep(ref Vector3 pos, ref Vector3 vel, Vector3 target,
            float spring, float damping, float maxSpeed, float dt)
        {
            Vector3 accel = (target - pos) * spring - vel * damping;
            vel += accel * dt;
            if (maxSpeed > 0f)
            {
                float sp = vel.magnitude;
                if (sp > maxSpeed) vel *= maxSpeed / sp;
            }
            pos += vel * dt;
        }

        private static void ReferenceSpringStep2(ref Vector2 pos, ref Vector2 vel, Vector2 target,
            float spring, float damping, float maxSpeed, float dt)
        {
            Vector2 accel = (target - pos) * spring - vel * damping;
            vel += accel * dt;
            if (maxSpeed > 0f)
            {
                float sp = vel.magnitude;
                if (sp > maxSpeed) vel *= maxSpeed / sp;
            }
            pos += vel * dt;
        }

        [Test]
        public void SpringStep_BitExact_AgainstIngameInlineMath()
        {
            // 인게임 상수(DragSwaySettings 기본): spring 100 / damping 2.5 / maxSpeed 12.
            Vector3 posA = new Vector3(1f, 0f, -2f), velA = Vector3.zero;
            Vector3 posB = posA, velB = velA;
            for (int i = 0; i < 120; i++)
            {
                var target = new Vector3(Mathf.Sin(i * 0.1f) * 3f, 0.35f, Mathf.Cos(i * 0.1f) * 3f);
                KeyringSim.SpringStep(ref posA, ref velA, target, 100f, 2.5f, 12f, Dt);
                ReferenceSpringStep(ref posB, ref velB, target, 100f, 2.5f, 12f, Dt);
                Assert.AreEqual(posB.x, posA.x, 0f, $"pos.x step {i}");
                Assert.AreEqual(posB.y, posA.y, 0f, $"pos.y step {i}");
                Assert.AreEqual(posB.z, posA.z, 0f, $"pos.z step {i}");
                Assert.AreEqual(velB.x, velA.x, 0f, $"vel.x step {i}");
                Assert.AreEqual(velB.y, velA.y, 0f, $"vel.y step {i}");
                Assert.AreEqual(velB.z, velA.z, 0f, $"vel.z step {i}");
            }
        }

        [Test]
        public void SpringStep_Vector2Path_BitExact_AgainstOutgameInlineMath()
        {
            // 아웃게임 상수(LobbyKeyringSettings 기본): spring 100 / damping 2.5 / maxSpeed 2400.
            // 아웃게임은 Vector2 — KeyringSim(Vector3, z=0) 경유가 bit-exact 이고 z 가 0 에 머무는지 고정.
            Vector2 pos2 = new Vector2(40f, -120f), vel2 = Vector2.zero;
            Vector3 pos3 = pos2, vel3 = vel2;
            for (int i = 0; i < 120; i++)
            {
                Vector2 target = new Vector2(Mathf.Sin(i * 0.2f) * 300f, -160f + i * 2f);
                ReferenceSpringStep2(ref pos2, ref vel2, target, 100f, 2.5f, 2400f, Dt);
                KeyringSim.SpringStep(ref pos3, ref vel3, target, 100f, 2.5f, 2400f, Dt);
                Assert.AreEqual(pos2.x, pos3.x, 0f, $"pos.x step {i}");
                Assert.AreEqual(pos2.y, pos3.y, 0f, $"pos.y step {i}");
                Assert.AreEqual(0f, pos3.z, 0f, $"z 잔류 step {i}");
                Assert.AreEqual(vel2.x, vel3.x, 0f, $"vel.x step {i}");
                Assert.AreEqual(vel2.y, vel3.y, 0f, $"vel.y step {i}");
            }
        }

        // hand-drag-tooltip unit 6 — float 오버로드(카메라 헤드룸 가중치 0↔1 추종).
        // Vector3 본체 경유가 bit-exact 이고 y/z 가 0 에 머무는지 고정한다.
        [Test]
        public void SpringStep_FloatOverload_BitExact_AgainstVector3Path()
        {
            // 헤드룸 기본 상수: spring 90 / damping 14 / maxSpeed 0(무제한).
            float posF = 0f, velF = 0f;
            Vector3 pos3 = Vector3.zero, vel3 = Vector3.zero;
            for (int i = 0; i < 240; i++)
            {
                float target = (i / 60) % 2 == 0 ? 1f : 0f; // 1초마다 열림/닫힘 토글
                KeyringSim.SpringStep(ref posF, ref velF, target, 90f, 14f, 0f, Dt);
                KeyringSim.SpringStep(ref pos3, ref vel3, new Vector3(target, 0f, 0f),
                    90f, 14f, 0f, Dt);
                Assert.AreEqual(pos3.x, posF, 0f, $"pos step {i}");
                Assert.AreEqual(vel3.x, velF, 0f, $"vel step {i}");
                Assert.AreEqual(0f, pos3.y, 0f, $"y 잔류 step {i}");
                Assert.AreEqual(0f, pos3.z, 0f, $"z 잔류 step {i}");
            }
        }

        // 언더댐핑(damping < 2√spring ≈ 19)이라 목표를 넘어섰다가 안착해야 한다.
        // 이 성질이 깨지면 헤드룸의 "스프링 맛"이 사라진다(사용자 요구사항).
        [Test]
        public void SpringStep_HeadroomDefaults_Overshoot_ThenSettle()
        {
            float pos = 0f, vel = 0f;
            bool overshot = false;
            for (int i = 0; i < 600; i++)
            {
                KeyringSim.SpringStep(ref pos, ref vel, 1f, 90f, 14f, 0f, Dt);
                if (pos > 1f) overshot = true;
            }
            Assert.IsTrue(overshot, "damping 14 < 2*sqrt(90) 이므로 오버슈트해야 한다");
            Assert.AreEqual(1f, pos, 1e-3f, "결국 목표에 안착해야 한다");
            Assert.AreEqual(0f, vel, 1e-3f, "안착 후 속도는 0 에 수렴해야 한다");
        }

        [Test]
        public void SpringStep_Vector2Overload_BitExact_AgainstOutgameInlineMath()
        {
            // 리뷰 반영 — 아웃게임 호출측 마샬링을 흡수한 Vector2 오버로드도 동일하게 고정.
            Vector2 posRef = new Vector2(40f, -120f), velRef = Vector2.zero;
            Vector2 posOv = posRef, velOv = velRef;
            for (int i = 0; i < 120; i++)
            {
                Vector2 target = new Vector2(Mathf.Sin(i * 0.2f) * 300f, -160f + i * 2f);
                ReferenceSpringStep2(ref posRef, ref velRef, target, 100f, 2.5f, 2400f, Dt);
                KeyringSim.SpringStep(ref posOv, ref velOv, target, 100f, 2.5f, 2400f, Dt);
                Assert.AreEqual(posRef.x, posOv.x, 0f, $"pos.x step {i}");
                Assert.AreEqual(posRef.y, posOv.y, 0f, $"pos.y step {i}");
                Assert.AreEqual(velRef.x, velOv.x, 0f, $"vel.x step {i}");
                Assert.AreEqual(velRef.y, velOv.y, 0f, $"vel.y step {i}");
            }
        }

        [Test]
        public void CubicBezier_Endpoints_Exact_And_Midpoint()
        {
            // defender-tap-to-place unit 6 — 3차 던지기 곡선. endpoints 정확 + 중점 가중치 회귀.
            var a = new Vector3(1f, 0f, -2f);
            var c1 = new Vector3(0.5f, 3f, -1f);
            var c2 = new Vector3(-0.5f, 1f, 1f);
            var b = new Vector3(-1f, 0f, 2f);

            var at0 = KeyringSim.CubicBezier(a, c1, c2, b, 0f);
            Assert.AreEqual(a.x, at0.x, 0f, "t=0 x"); Assert.AreEqual(a.y, at0.y, 0f, "t=0 y"); Assert.AreEqual(a.z, at0.z, 0f, "t=0 z");

            var at1 = KeyringSim.CubicBezier(a, c1, c2, b, 1f);
            Assert.AreEqual(b.x, at1.x, 0f, "t=1 x"); Assert.AreEqual(b.y, at1.y, 0f, "t=1 y"); Assert.AreEqual(b.z, at1.z, 0f, "t=1 z");

            var mid = 0.125f * a + 0.375f * c1 + 0.375f * c2 + 0.125f * b;
            var atH = KeyringSim.CubicBezier(a, c1, c2, b, 0.5f);
            Assert.AreEqual(mid.x, atH.x, 1e-5f, "t=0.5 x");
            Assert.AreEqual(mid.y, atH.y, 1e-5f, "t=0.5 y");
            Assert.AreEqual(mid.z, atH.z, 1e-5f, "t=0.5 z");
        }

        [Test]
        public void LeanAngle_BitExact_AgainstInlineFormula()
        {
            // 추출 전 공식: clamp(-atan2(x, max(y, 1e-3)) * Rad2Deg, ±maxAngle).
            // 단위벡터(아웃게임)·비단위 투영값(인게임)·eps 퇴화·클램프 영역을 모두 커버.
            (float x, float y)[] cases =
            {
                (0f, 1f),          // 수직 — 기울임 0
                (0.05f, 0.99f),    // 미세 스윙
                (-0.05f, 0.99f),
                (0.3f, 0.2f),      // 비단위 투영값(인게임 카메라 기울임)
                (1f, 0f),          // 수평 — eps floor + 클램프
                (-1f, -0.5f),      // 역방향(y<0) — eps floor
                (0.0004f, 0.0003f) // 퇴화 소벡터
            };
            foreach (var c in cases)
            {
                float expected = Mathf.Clamp(
                    -Mathf.Atan2(c.x, Mathf.Max(c.y, 1e-3f)) * Mathf.Rad2Deg, -8f, 8f);
                Assert.AreEqual(expected, KeyringSim.LeanAngle(c.x, c.y, 8f), 0f, $"({c.x},{c.y})");
            }
        }

        // defender-relocation unit 6 — 던지기 곡선 제어점(탭 배치 · 재배치 공유).
        // 핵심 회귀 가드: 아치 lift 가 camUp(화면 세로)로 가야 한다. 보드 평면(start-end + boardRight)에
        // 갇히면 "평면 이동"으로 보인다(sim 공간 아치의 옛 버그). camUp ⟂ 보드일 때 control 의 camUp 성분이 양수여야.
        [Test]
        public void ThrowArcControls_LiftsAlongCamUp_NotInBoardPlane()
        {
            Vector3 start = Vector3.zero, end = new Vector3(0f, 0f, 4f); // 진행은 z, 보드 평면 = XZ
            Vector3 camUp = Vector3.up, boardRight = Vector3.right;       // camUp=y(보드 밖), lateral=x
            KeyringSim.ThrowArcControls(start, end, camUp, boardRight,
                0.32f, 0f, new Vector2(0.18f, 1f), new Vector2(0.72f, 0.22f), 0,
                out var cA, out var cB);
            float arc = 4f * 0.32f;
            Assert.AreEqual(arc * 1.0f, cA.y, 1e-4f, "controlA camUp lift(launch.y=1)");
            Assert.AreEqual(arc * 0.22f, cB.y, 1e-4f, "controlB camUp lift(landing.y=0.22)");
            Assert.Greater(cA.y, 0.5f, "아치가 화면 세로로 떠야 한다(평면 아님)");
            Assert.AreEqual(0f, cA.x, 1e-5f, "lateral 0 → boardRight 성분 없음");
            Assert.AreEqual(0f, cB.x, 1e-5f);
            Assert.AreEqual(0.18f * 4f, cA.z, 1e-4f, "controlA 진행(launch.x)");
            Assert.AreEqual(0.72f * 4f, cB.z, 1e-4f, "controlB 진행(landing.x)");
        }

        // --- defender-drop-dismount unit 0 — DismountPoint (반동 Hermite + 수직착지 아치) ---
        // 공통 픽스처: 보드=XZ, camUp=+Y. start 는 매달린 발점, end 는 화면 위쪽(z+) 타일.

        private static Vector3 Dismount(Vector3 start, Vector3 startVel, Vector3 end, float t,
            float recoilFrac = 0.267f, float dip = 0.35f,
            float factor = 0.5f, float minH = 1.5f, float landingH = 0.25f)
        {
            return KeyringSim.DismountPoint(start, startVel, end, Vector3.up,
                recoilFrac, dip, factor, minH, new Vector2(0.25f, 1f), landingH, t);
        }

        [Test]
        public void DismountPoint_Endpoints_Exact()
        {
            var start = new Vector3(1f, 0.2f, -2f);
            var end = new Vector3(0.6f, 0f, 0.8f);
            var vel = new Vector3(2f, 0f, 1f);
            var p0 = Dismount(start, vel, end, 0f);
            Assert.AreEqual(0f, Vector3.Distance(p0, start), 1e-4f, "t=0 → start");
            var p1 = Dismount(start, vel, end, 1f);
            Assert.AreEqual(0f, Vector3.Distance(p1, end), 1e-4f, "t=1 → end");
        }

        [Test]
        public void DismountPoint_RecoilBoundary_C0Continuous_AtDip()
        {
            var start = new Vector3(0f, 0f, 0f);
            var end = new Vector3(0f, 0f, 3f);
            var dipPoint = start - Vector3.up * 0.35f;
            var atFrac = Dismount(start, Vector3.zero, end, 0.267f);
            Assert.AreEqual(0f, Vector3.Distance(atFrac, dipPoint), 1e-3f, "t=recoilFrac → dip");
            var justAfter = Dismount(start, Vector3.zero, end, 0.267f + 1e-4f);
            Assert.AreEqual(0f, Vector3.Distance(justAfter, dipPoint), 1e-2f, "경계 직후 C0 연속");
        }

        [Test]
        public void DismountPoint_EndTangent_IsVerticalDescent()
        {
            var start = new Vector3(0.4f, 0f, -1f);
            var end = new Vector3(0f, 0f, 2.5f);
            var a = Dismount(start, Vector3.zero, end, 1f - 1e-3f);
            var b = Dismount(start, Vector3.zero, end, 1f);
            var dir = (b - a).normalized;
            Assert.Less(Vector3.Dot(dir, Vector3.up), -0.99f, "착지 접선은 순수 -camUp(수직 스틱)");
        }

        [Test]
        public void DismountPoint_ApexFloor_EngagesWhenDistanceProportionalIsFlat()
        {
            // 거리비례 항이 납작(3×0.01=0.03)해도 절대 하한(minH 2)이 apex 를 세운다 — "솟음" 계약.
            var start = Vector3.zero;
            var end = new Vector3(0f, 0f, 3f);
            float apexFloored = 0f, apexNoFloor = 0f;
            for (int i = 0; i <= 400; i++)
            {
                float t = i / 400f;
                apexFloored = Mathf.Max(apexFloored,
                    Dismount(start, Vector3.zero, end, t, factor: 0.01f, minH: 2f).y);
                apexNoFloor = Mathf.Max(apexNoFloor,
                    Dismount(start, Vector3.zero, end, t, factor: 0.01f, minH: 0f).y);
            }
            // minArcHeight 는 **제어점 높이** semantics(ThrowArcControls 와 동일) — 이 노브 조합의
            // 실제 apex ≈ 0.41×제어점높이(dip 의 (1−u)³ 항 + lerp 감쇠). 경계 0.75 는 러프 하한.
            Assert.GreaterOrEqual(apexFloored, 0.75f, "하한 2 → apex ≥ 0.375×minH");
            Assert.LessOrEqual(apexNoFloor, 0.05f, "하한 없으면 납작 — 하한이 실제로 작동함을 대비로 증명");
        }

        [Test]
        public void DismountPoint_ZeroVelocity_RecoilDescendsMonotonically()
        {
            var start = new Vector3(0f, 1f, 0f);
            var end = new Vector3(0f, 0f, 3f);
            float prevY = float.MaxValue;
            for (int i = 0; i <= 40; i++)
            {
                float t = 0.267f * i / 40f;
                float y = Dismount(start, Vector3.zero, end, t).y;
                Assert.LessOrEqual(y, prevY + 1e-5f, $"반동 구간 단조 하강 위반 t={t:F3}");
                prevY = y;
            }
        }

        [Test]
        public void DismountPoint_ResidualVelocity_ShapesRecoilStart()
        {
            // 릴리스 잔여 스윙 속도(접선)가 반동 초입 이동 방향을 지배해야 한다(F-2 흡수).
            var start = Vector3.zero;
            var end = new Vector3(0f, 0f, 3f);
            var vel = new Vector3(5f, 0f, 0f); // 반동구간 정규화 접선
            var p = Dismount(start, vel, end, 0.267f * 0.05f);
            var dir = (p - start).normalized;
            Assert.Greater(Vector3.Dot(dir, Vector3.right), 0.99f, "초입 방향 ≈ startVel");
        }

        [Test]
        public void ThrowArcControls_Deterministic_AndLateralVariesBySeq()
        {
            Vector3 start = Vector3.zero, end = new Vector3(0f, 0f, 4f);
            Vector3 camUp = Vector3.up, boardRight = Vector3.right;
            KeyringSim.ThrowArcControls(start, end, camUp, boardRight, 0.32f, 0.22f,
                new Vector2(0.18f, 1f), new Vector2(0.72f, 0.22f), 3, out var a1, out var b1);
            KeyringSim.ThrowArcControls(start, end, camUp, boardRight, 0.32f, 0.22f,
                new Vector2(0.18f, 1f), new Vector2(0.72f, 0.22f), 3, out var a2, out var b2);
            Assert.AreEqual(a1.x, a2.x, 0f, "같은 seq → 결정론적 A.x");
            Assert.AreEqual(b1.x, b2.x, 0f, "같은 seq → 결정론적 B.x");
            KeyringSim.ThrowArcControls(start, end, camUp, boardRight, 0.32f, 0.22f,
                new Vector2(0.18f, 1f), new Vector2(0.72f, 0.22f), 4, out var a3, out _);
            Assert.AreNotEqual(a1.x, a3.x, "다른 seq → 좌우 변주(boardRight=x)가 달라야 한다");
        }
    }
}
