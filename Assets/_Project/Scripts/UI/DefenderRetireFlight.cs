using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Presentation;

namespace Wassup.UI
{
    // defender-clock-out unit 3 rev 2 — **퇴근 스냅.**
    //
    // rev 1 은 배치 아치를 거꾸로 재생했다(0.55초 동안 곡선으로 떠감). 사용자 평가: 밋밋하다.
    // 진단은 방향이 아니라 **구조**였다 — 게임 필은 «예비 → 스냅 → 여운» 인데 rev 1 엔 가운데만
    // 있었다. 예고가 없으니 무슨 일이 일어나는지 못 읽고, 등속에 가까우니 힘이 안 실리고,
    // 0.55초는 전투 흐름에 비해 길다.
    //
    // rev 2 의 3막 (~0.28초):
    //   ① 웅크림(0.10s) — 눌리며 살짝 내려앉는다. "당겨지기 직전" 텐션.
    //   ② 스냅(0.18s)  — 위로 **뽑혀 나간다.** 즉발 가속 + 세로로 길게 늘어나며(squash&stretch)
    //                    가늘어져 사라진다. 만화적 yoink.
    //   ③ 여운        — 떠난 칸에 **배치 링**이 한 번. 올 때 나던 그 링이 갈 때도 난다.
    //
    // 죽음과의 구분이 rev 1 보다 선명해졌다: 죽음은 **아래로 무너지고**, 퇴근은 **위로 뽑힌다.**
    // 아치·베지어는 버렸다 — 곡선은 "날아간다"를, 직선 스냅은 "뽑힌다"를 말한다. 그래서
    // DragController lazy 해석(KeyringSim 곡선)도 함께 사라졌다.
    public class DefenderRetireFlight : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        [Tooltip("떠난 칸에 칠 링. 배치 때 나는 그 링과 같은 것.")]
        [SerializeField] private VfxSpawner vfxSpawner;

        [Header("① 웅크림")]
        [Tooltip("초. Battle 도메인 — 슬로모를 따른다")]
        [SerializeField] private float anticipationSeconds = 0.10f;
        [Tooltip("눌리는 정도. 0.24 = 세로 76%")]
        [SerializeField, Range(0f, 0.6f)] private float crouchAmount = 0.24f;
        [Tooltip("웅크릴 때 내려앉는 거리(view 단위)")]
        [SerializeField] private float crouchDip = 0.14f;

        [Header("② 스냅")]
        [SerializeField] private float snapSeconds = 0.18f;
        [Tooltip("위로 뽑혀 올라가는 거리(view 단위)")]
        [SerializeField] private float riseDistance = 4.6f;
        [Tooltip("끝에서의 세로 배율 — 길게 늘어난다")]
        [SerializeField] private float stretchY = 2.3f;
        [Tooltip("끝에서의 가로 배율 — 가늘어지며 사라진다")]
        [SerializeField, Range(0.01f, 1f)] private float stretchX = 0.2f;

        // 동시 퇴근이 가능하므로 **단일 슬롯이 아니라 목록**이다(재배치는 이동모드가 하나뿐이라
        // 단일 슬롯이었다). teardown 에서 전부 정리해야 고아가 안 남는다.
        private readonly List<SpineUnitView> _inFlight = new List<SpineUnitView>();

        // BattleBridge 가 퇴근 확정 시 호출한다. view 는 이미 풀에서 떼어진 상태이고,
        // **이 시점부터 수명은 여기 것**이다(SpineUnitPool.Detach 의 계약).
        //
        // simWorld 는 링을 칠 좌표다 — VfxSpawner 가 진입부에서 ToView 하므로 **sim 을 넘긴다**
        // (이중 변환 금지). 뷰의 transform 은 view 공간이라 이 용도로 쓸 수 없다.
        public void Fly(SpineUnitView view, Vector3 simWorld)
        {
            if (view == null) return;
            // 그림자는 지면에 남는다 — 유닛이 뽑혀 올라가는 동안 원래 칸에 원본 크기로 눌러앉아
            // "아직 저기 있다"로 읽힌다. 떠나는 연출이므로 함께 걷어낸다.
            var blob = view.GetComponentInChildren<BlobShadow>(true);
            if (blob != null) Destroy(blob.gameObject);

            _inFlight.Add(view);
            StartCoroutine(Run(view, simWorld));
        }

        private IEnumerator Run(SpineUnitView view, Vector3 simWorld)
        {
            Vector3 basePos = view.transform.position;
            Vector3 baseScale = view.transform.localScale;
            var cam = mainCamera != null ? mainCamera : Camera.main;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;

            // ⚠ localScale 직접 대입 — SpineUnitView 는 "스케일 쓰기의 단일 지점"(ApplyRenderScale)
            // 을 요구하지만 그 규칙은 **경합하는 소유자가 둘 이상일 때**의 것이다. 떼어낸 뷰는
            // 매 프레임 피드도 코루틴도 붙어 있지 않아 소유자가 여기 하나뿐이다.

            // ── ① 웅크림 ─────────────────────────────────────────────────────
            float t = 0f;
            float dur = Mathf.Max(0.01f, anticipationSeconds);
            while (t < 1f)
            {
                if (view == null) { Finish(view); yield break; }
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle) / dur;
                float k = Mathf.Clamp01(t);
                float e = 1f - (1f - k) * (1f - k); // OutQuad — 빠르게 눌렸다가 버틴다
                view.transform.position = basePos - up * (crouchDip * e);
                view.transform.localScale = Vector3.Scale(baseScale,
                    new Vector3(1f + crouchAmount * 0.7f * e, 1f - crouchAmount * e, 1f));
                yield return null;
            }

            // ── ③ 여운(링)은 **스냅과 동시에** 친다 ──────────────────────────
            // 웅크림에 치면 배치처럼 읽히고, 끝나고 치면 인과가 늦다. 뽑히는 순간이 사건이다.
            if (vfxSpawner != null) vfxSpawner.SpawnPlacementRing(simWorld);

            // ── ② 스냅 ───────────────────────────────────────────────────────
            Vector3 crouchPos = basePos - up * crouchDip;
            Vector3 crouchScale = Vector3.Scale(baseScale,
                new Vector3(1f + crouchAmount * 0.7f, 1f - crouchAmount, 1f));
            Vector3 endScale = Vector3.Scale(baseScale, new Vector3(stretchX, stretchY, 1f));

            t = 0f;
            dur = Mathf.Max(0.01f, snapSeconds);
            while (t < 1f)
            {
                if (view == null) { Finish(view); yield break; }
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle) / dur;
                float k = Mathf.Clamp01(t);
                // OutExpo — 첫 프레임에 이미 크게 튄다. 이게 "뽑혔다"의 정체이고,
                // rev 1 의 등속 아치가 놓쳤던 것이다.
                float e = k >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * k);
                view.transform.position = crouchPos + up * (riseDistance * e);
                view.transform.localScale = Vector3.Lerp(crouchScale, endScale, e);
                yield return null;
            }

            Finish(view);
        }

        private void Finish(SpineUnitView view)
        {
            _inFlight.Remove(view);
            if (view != null) view.Dispose();
        }

        // 매치 teardown · 씬 언로드 · 컴포넌트 비활성 — 진행 중 연출을 무효화하고 뷰를 치운다.
        // 풀이 더 이상 모르는 뷰라 여기서 안 치우면 **고아 GameObject 로 남는다**(Detach 의 계약).
        private void OnDisable()
        {
            StopAllCoroutines();
            for (int i = 0; i < _inFlight.Count; i++)
                if (_inFlight[i] != null) _inFlight[i].Dispose();
            _inFlight.Clear();
        }

        // 테스트용 관측점 — 진행 중인 퇴근 연출 수.
        public int InFlightCount => _inFlight.Count;
    }
}
