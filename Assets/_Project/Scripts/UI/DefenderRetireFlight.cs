using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.UI
{
    // defender-clock-out unit 3 rev 4 — **"퇴근 중".**
    //
    // 컨셉(사용자 지정): 퇴근은 **시간이 걸리는 사건**이다. 키링 줄이 걸려 위로 당기는데
    // 유닛은 바닥에 박힌 채 **움찔거리며 버틴다**(나가기 싫다). 그러다 결국 **뽑혀서
    // 뱅글뱅글 돌며 화면 밖으로 튕겨 나간다.**
    //
    // 4막 (~2.4초):
    //   ① 연결(0.25s) — 줄이 위에서 내려와 걸린다. 걸리는 순간 유닛이 한 번 움찔.
    //   ② 저항(1.75s) — 줄이 팽팽해지고 고리는 올라가는데 **유닛은 거의 안 올라온다.**
    //                   발이 박힌 채 몸만 늘어나고, 고주파 진동(좌우 흔들림 + 미세 회전)이
    //                   장력과 함께 **점점 커진다.** 이 구간이 이 연출의 내용 전부다.
    //   ③ 뽑힘(0.40s) — 팡. 배치 링 + 급가속 + **뱅글뱅글 회전** + 좌우로 튕기며 화면 밖.
    //
    // rev 이력: rev 1(0.55s 아치)=밋밋함 · rev 2(0.28s 스냅)=구조는 맞음 · rev 3(스냅+키링)=
    // **부착이 안 읽힘**. rev 3 의 결론은 "0.3초에 부착 어휘는 못 들어간다" 였고, rev 4 는
    // **길이를 늘려 그 제약을 없앤다** — 저항이 곧 콘텐츠이므로 2초가 지루하지 않다.
    //
    // ⚠ 회전은 `Billboard`(Tilted) 컴포넌트가 매 LateUpdate 로 소유한다. 뱅글뱅글을 위해
    // 떼어낸 뷰에서 그것을 **끄고 회전을 인수**한다(안 끄면 다음 프레임에 덮인다).
    public class DefenderRetireFlight : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        [Tooltip("뽑히는 순간 떠난 칸에 칠 링. 배치 때 나는 그 링과 같은 것.")]
        [SerializeField] private VfxSpawner vfxSpawner;
        // 키링 하드웨어(고리+줄)의 소유자. 씬 직렬화 대상이 아니라 DefenderSelector 가 런타임
        // AddComponent 로 만들므로 재배치 컨트롤러와 같은 lazy 해석을 쓴다.
        [SerializeField] private DefenderSelector defenderSelector;

        private DefenderDragPlacementController _dragCached;
        private DefenderDragPlacementController DragController
        {
            get
            {
                if (_dragCached == null && defenderSelector != null)
                    _dragCached = defenderSelector.DragController;
                return _dragCached;
            }
        }

        [Header("① 연결")]
        [Tooltip("줄이 내려와 걸리기까지(초, Battle 도메인)")]
        [SerializeField] private float hookSeconds = 0.25f;
        [Tooltip("줄이 얼마나 위에서 내려오나(view 단위)")]
        [SerializeField] private float hookDropDistance = 6f;

        [Header("② 저항 — 이 연출의 본문")]
        [Tooltip("버티는 시간(초). 사용자 지정 ≈2초")]
        [SerializeField] private float resistSeconds = 1.75f;
        [Tooltip("저항 중 실제로 뽑혀 나오는 거리(view 단위). 작을수록 '박혀 있다'")]
        [SerializeField] private float resistRise = 0.5f;
        [Tooltip("움찔 진동 주기(Hz)")]
        [SerializeField] private float wiggleHz = 13f;
        [Tooltip("움찔 좌우 진폭(view 단위, 구간 끝 기준). 앞쪽은 작게 시작한다")]
        [SerializeField] private float wiggleAmplitude = 0.16f;
        [Tooltip("움찔 회전 진폭(도, 구간 끝 기준)")]
        [SerializeField] private float wiggleTiltDegrees = 9f;
        [Tooltip("장력으로 몸이 늘어나는 정도(구간 끝 세로 배율)")]
        [SerializeField] private float tensionStretchY = 1.18f;

        [Header("③ 뽑힘")]
        [SerializeField] private float popSeconds = 0.40f;
        [Tooltip("뽑힌 뒤 올라가는 거리(view 단위) — 화면 밖까지")]
        [SerializeField] private float popRise = 9f;
        [Tooltip("뽑히며 옆으로 튕기는 거리(view 단위)")]
        [SerializeField] private float popLateral = 2.6f;
        [Tooltip("뱅글뱅글 총 회전량(도)")]
        [SerializeField] private float popSpinDegrees = 900f;
        [Tooltip("뽑힌 직후 세로 배율 — 튕겨나가며 늘어난다")]
        [SerializeField] private float popStretchY = 1.5f;

        // 동시 퇴근이 가능하므로 **단일 슬롯이 아니라 목록**이다. 2.4초로 길어진 지금은 겹칠
        // 확률이 rev 2 보다 훨씬 높다. teardown 에서 뷰와 키링 루트를 **쌍으로** 정리한다.
        private readonly List<(SpineUnitView view, GameObject keyringRoot)> _inFlight =
            new List<(SpineUnitView, GameObject)>();

        // BattleBridge 가 퇴근 확정 시 호출한다. view 는 이미 풀에서 떼어진 상태이고,
        // **이 시점부터 수명은 여기 것**이다(SpineUnitPool.Detach 의 계약).
        //
        // simWorld = 링을 칠 좌표. VfxSpawner 가 진입부에서 ToView 하므로 **sim 을 넘긴다**
        // (이중 변환 금지). 뷰의 transform 은 view 공간이라 이 용도로 쓸 수 없다.
        public void Fly(SpineUnitView view, Vector3 simWorld, DefenderUnitData unitData)
        {
            if (view == null) return;
            // 그림자는 지면에 남는다 — 유닛이 뽑혀 올라가는 동안 원래 칸에 원본 크기로 눌러앉아
            // "아직 저기 있다"로 읽힌다. 떠나는 연출이므로 함께 걷어낸다.
            var blob = view.GetComponentInChildren<BlobShadow>(true);
            if (blob != null) Destroy(blob.gameObject);

            // 회전 인수 — Billboard 가 살아 있으면 매 LateUpdate 에 우리 회전이 덮인다.
            var billboard = view.GetComponent<Billboard>();
            if (billboard != null) billboard.enabled = false;

            StartCoroutine(Run(view, simWorld, unitData));
        }

        private IEnumerator Run(SpineUnitView view, Vector3 simWorld, DefenderUnitData unitData)
        {
            Vector3 basePos = view.transform.position;
            Vector3 baseScale = view.transform.localScale;
            Quaternion baseRot = view.transform.rotation; // Billboard 가 마지막에 세운 틸트가 기준
            var cam = mainCamera != null ? mainCamera : Camera.main;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;
            Vector3 right = cam != null
                ? Vector3.ProjectOnPlane(cam.transform.right, BoardSpace.RaycastPlane().normal).normalized
                : Vector3.right;
            Vector3 spinAxis = cam != null ? cam.transform.forward : Vector3.forward; // 화면 평면 회전

            // 뽑는 주체. 하드웨어는 배치·재배치와 **같은 소스**(CreateKeyringHardware →
            // BuildRingAndCord) 라 룩이 저절로 일치한다. 미배선(테스트·트레이 미준비)이면
            // 키링 없이 모션만 — 연출은 게임 규칙을 하나도 소유하지 않는다.
            var drag = DragController;
            var keyring = drag != null ? drag.CreateKeyringHardware(unitData) : default;
            _inFlight.Add((view, keyring.root));

            // ⚠ localScale/rotation 직접 대입 — SpineUnitView 는 "스케일 쓰기의 단일 지점"을,
            // Billboard 는 회전을 요구하지만 둘 다 **경합 소유자가 있을 때**의 규칙이다. 떼어낸
            // 뷰는 매 프레임 피드가 끊겼고 Billboard 도 껐으므로 소유자가 여기 하나뿐이다.

            // ── ① 연결 ───────────────────────────────────────────────────────
            float t = 0f, dur = Mathf.Max(0.01f, hookSeconds);
            while (t < 1f)
            {
                if (view == null) { Finish(view, keyring); yield break; }
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle) / dur;
                float k = Mathf.Clamp01(t);
                float e = 1f - (1f - k) * (1f - k); // OutQuad — 줄이 탁 내려온다
                // 걸리는 순간 한 번 움찔: 구간 끝 20% 에서만 짧게 흔든다.
                float hit = k > 0.8f ? Mathf.Sin((k - 0.8f) / 0.2f * Mathf.PI) : 0f;
                view.transform.position = basePos + right * (hit * wiggleAmplitude * 0.5f);
                DrawKeyring(keyring, view, up, hookDropDistance * (1f - e));
                yield return null;
            }

            // ── ② 저항 — 박혀서 움찔거린다 ────────────────────────────────────
            // 고리는 계속 올라가는데 유닛은 resistRise 만큼만 따라온다. 그 **차이가 장력**이고,
            // 장력이 몸을 늘리고 진동을 키운다. 세 값(고리 높이·몸 늘어남·진동 진폭)이 같은
            // 진행도 하나에서 나오므로 서로 어긋나지 않는다.
            float elapsed = 0f;
            float total = Mathf.Max(0.01f, resistSeconds);
            float phase = 0f;
            while (elapsed < total)
            {
                if (view == null) { Finish(view, keyring); yield break; }
                float dt = TimeManager.Instance.DeltaTime(TimeDomain.Battle);
                elapsed += dt;
                phase += dt * wiggleHz;
                float p = Mathf.Clamp01(elapsed / total);          // 장력 진행도
                float ramp = p * p;                                // 후반에 급격히 몸부림친다
                float w = Mathf.Sin(phase * Mathf.PI * 2f);

                view.transform.position = basePos
                    + up * (resistRise * p)
                    + right * (w * wiggleAmplitude * ramp);
                view.transform.localScale = Vector3.Scale(baseScale,
                    new Vector3(1f, Mathf.Lerp(1f, tensionStretchY, p), 1f));
                view.transform.rotation =
                    baseRot * Quaternion.AngleAxis(w * wiggleTiltDegrees * ramp, spinAxis);
                DrawKeyring(keyring, view, up, 0f);
                yield return null;
            }

            // ── ③ 뽑힘 ───────────────────────────────────────────────────────
            // 링은 **뽑히는 순간**에 친다. 연결/저항 때 치면 배치처럼 읽히고, 끝나고 치면 늦다.
            if (vfxSpawner != null) vfxSpawner.SpawnPlacementRing(simWorld);

            Vector3 popFrom = basePos + up * resistRise;
            Vector3 popScale = Vector3.Scale(baseScale, new Vector3(1f, tensionStretchY, 1f));
            Vector3 endScale = Vector3.Scale(baseScale, new Vector3(1f, popStretchY, 1f));

            t = 0f; dur = Mathf.Max(0.01f, popSeconds);
            while (t < 1f)
            {
                if (view == null) { Finish(view, keyring); yield break; }
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle) / dur;
                float k = Mathf.Clamp01(t);
                float e = k >= 1f ? 1f : 1f - Mathf.Pow(2f, -9f * k); // OutExpo — 팡
                view.transform.position = popFrom + up * (popRise * e) + right * (popLateral * e);
                view.transform.localScale = Vector3.Lerp(popScale, endScale, e);
                // 뱅글뱅글 — 회전은 **선형**으로 돌린다(감속시키면 도는 게 멈춰 보인다).
                view.transform.rotation = baseRot * Quaternion.AngleAxis(popSpinDegrees * k, spinAxis);
                DrawKeyring(keyring, view, up, 0f); // 줄이 딸려 올라가며 같이 사라진다
                yield return null;
            }

            Finish(view, keyring);
        }

        // 고리를 머리 위 rope 길이에 두고 줄을 머리까지 잇는다 — 재배치 비행의 UpdateKeyring 과
        // 같은 기하다. extraLift = "아직 다 안 내려온" 여분 높이(연결 구간에서만 > 0).
        //
        // 재배치는 스무스 추종(sway 근사)을 썼지만 여기서는 **즉시 추종**이다. 저항 구간의 움찔은
        // 고주파라 관성을 넣으면 줄이 뭉개지고, 뽑힘은 순간이라 뒤처지면 "따라간다"로 읽힌다.
        private static void DrawKeyring(DefenderDragPlacementController.KeyringHardware kr,
            SpineUnitView view, Vector3 up, float extraLift)
        {
            if (!kr.valid || view == null) return;
            Vector3 head = view.transform.position + up * view.ApproxWorldHeight;
            Vector3 ringPos = head + up * (kr.ropeWorld + extraLift);
            kr.ring.position = ringPos;
            kr.cord.SetPosition(0, ringPos);
            kr.cord.SetPosition(1, head);
        }

        private void Finish(SpineUnitView view, DefenderDragPlacementController.KeyringHardware keyring)
        {
            // 머티리얼은 드래그 컨트롤러 공유 — 루트만 파괴한다(재배치 HideKeyring 과 동일 규약).
            if (keyring.root != null) Destroy(keyring.root);
            for (int i = _inFlight.Count - 1; i >= 0; i--)
                if (_inFlight[i].view == view) _inFlight.RemoveAt(i);
            if (view != null) view.Dispose();
        }

        // 매치 teardown · 씬 언로드 · 컴포넌트 비활성 — 진행 중 연출을 무효화하고 전부 치운다.
        // 풀이 더 이상 모르는 뷰라 여기서 안 치우면 **고아 GameObject 로 남는다**(Detach 의 계약).
        // 2.4초로 길어진 지금 teardown 과 겹칠 창이 rev 2 보다 훨씬 넓다.
        private void OnDisable()
        {
            StopAllCoroutines();
            for (int i = 0; i < _inFlight.Count; i++)
            {
                if (_inFlight[i].keyringRoot != null) Destroy(_inFlight[i].keyringRoot);
                if (_inFlight[i].view != null) _inFlight[i].view.Dispose();
            }
            _inFlight.Clear();
        }

        // 테스트용 관측점 — 진행 중인 퇴근 연출 수.
        public int InFlightCount => _inFlight.Count;
    }
}
