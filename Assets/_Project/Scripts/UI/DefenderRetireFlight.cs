using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Presentation;

namespace Wassup.UI
{
    // defender-clock-out unit 3 — **퇴근 연출.** 판에서 내려온 유닛이 죽은 것처럼 보이지 않게 한다.
    //
    // 왜 기존 비행을 그대로 못 쓰나: 재배치 비행(DefenderRelocationController)은
    // bridge.SetDefenderViewOverride(entity, …) 로 **살아 있는 엔티티**의 뷰를 곡선 위로 민다.
    // 퇴근은 엔티티를 즉시 파괴하므로 그 경로가 없다. 그렇다고 파괴를 비행 끝까지 미루면 그동안
    // 유닛이 판에서 때리고 맞는다. 선례가 답을 준다 — 보스 도약은 "sim 은 즉시 텔레포트하고
    // **뷰만** 아치로 날린다". 그래서 여기서는 **뷰를 풀에서 떼어내(Detach) 따로 몬다.**
    //
    // 움직임 문법으로 죽음과 갈린다: 죽음은 *그 자리에 쓰러진다*, 퇴근은 *온 길로 되돌아 올라간다*.
    // 곡선은 배치·재배치가 이미 공유하는 순수 헬퍼(KeyringSim)를 그대로 쓴다 — 신규 곡선 코드 0.
    public class DefenderRetireFlight : MonoBehaviour
    {
        // 드래그 컨트롤러는 **씬 직렬화 대상이 아니다** — DefenderSelector 가 런타임 AddComponent 로
        // 만들고 DragController 프로퍼티로 노출한다. 재배치 컨트롤러가 쓰는 lazy 해석과 같은 형태.
        // (직렬화 필드로 두면 인스펙터에서 영영 비어 있어 곡선이 직선 폴백으로 조용히 죽는다.)
        [SerializeField] private DefenderSelector defenderSelector;
        [SerializeField] private Camera mainCamera;

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

        [Header("Flight")]
        [Tooltip("이탈에 걸리는 시간(초, Battle 도메인 — 슬로모를 따른다)")]
        [SerializeField] private float durationSeconds = 0.55f;
        [Tooltip("view 공간에서 camUp 방향으로 솟는 거리")]
        [SerializeField] private float riseDistance = 3.4f;
        [Tooltip("좌우로 흘리는 거리. 0 이면 수직으로만 뜬다")]
        [SerializeField] private float lateralDistance = 0.9f;
        [Tooltip("도착 시점 크기 배율. 작아지며 멀어지는 읽기")]
        [SerializeField, Range(0f, 1f)] private float endScale = 0.15f;

        // 동시 퇴근이 가능하므로 **단일 슬롯이 아니라 목록**이다(재배치는 이동모드가 하나뿐이라
        // 단일 슬롯이었다). teardown 에서 전부 정리해야 고아가 안 남는다.
        private readonly List<SpineUnitView> _inFlight = new List<SpineUnitView>();
        private int _gen;

        // BattleBridge 가 퇴근 확정 시 호출한다. view 는 이미 풀에서 떼어진 상태이고,
        // **이 시점부터 수명은 여기 것**이다.
        //
        // 출발점을 인자로 받지 않는다 — 뷰의 현재 transform 이 더 정확하다(SpineVisualOffset ·
        // 넉업 hop 이 얹혀 있어 셀 중심과 다를 수 있고, 그 차이가 곧 "있던 자리에서 뜬다" 이다).
        public void Fly(SpineUnitView view)
        {
            if (view == null) return;
            // 그림자는 지면에 남는다 — 유닛이 떠오르는 동안 원래 칸에 원본 크기로 눌러앉아
            // "아직 저기 있다"로 읽힌다. 떠나는 연출이므로 함께 걷어낸다.
            var blob = view.GetComponentInChildren<BlobShadow>(true);
            if (blob != null) Destroy(blob.gameObject);

            _inFlight.Add(view);
            StartCoroutine(Run(++_gen, view));
        }

        private IEnumerator Run(int gen, SpineUnitView view)
        {
            Vector3 basePos = view.transform.position;
            Vector3 baseScale = view.transform.localScale;

            var cam = mainCamera != null ? mainCamera : Camera.main;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
            Vector3 boardRight = cam != null
                ? Vector3.ProjectOnPlane(cam.transform.right, BoardSpace.RaycastPlane().normal)
                : Vector3.right;

            Vector3 endPos = basePos + camUp * riseDistance + boardRight * lateralDistance;

            // 재배치 비행과 **같은 곡선 로직**. 미배선(테스트/트레이 미준비)이면 직선 폴백 —
            // 재배치가 쓰는 그 규약 그대로다.
            var drag = DragController;
            bool haveArc = drag != null;
            Vector3 c1 = default, c2 = default;
            if (haveArc) drag.ComputeThrowArc(basePos, endPos, camUp, boardRight, gen, out c1, out c2);

            float t = 0f;
            float dur = Mathf.Max(0.05f, durationSeconds);
            while (t < 1f)
            {
                if (view == null) break; // 씬 언로드 등으로 먼저 사라짐
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle) / dur;
                float k = Mathf.Clamp01(t);
                // InCubic — 느리게 떴다가 가속해 사라진다. 배치 착지(OutCubic)의 거울상이라
                // "온 길을 되감는" 읽기가 된다.
                float e = k * k * k;
                view.transform.position = haveArc
                    ? KeyringSim.CubicBezier(basePos, c1, c2, endPos, e)
                    : Vector3.Lerp(basePos, endPos, e);
                // ⚠ localScale 직접 대입 — SpineUnitView 는 "스케일 쓰기의 단일 지점"(ApplyRenderScale)
                // 을 요구하지만 그 규칙은 **경합하는 소유자가 둘 이상일 때**의 것이다. 떼어낸 뷰는
                // 매 프레임 피드도 코루틴도 붙어 있지 않아 소유자가 여기 하나뿐이다.
                view.transform.localScale = baseScale * Mathf.Lerp(1f, endScale, e);
                yield return null;
            }

            Finish(view);
        }

        private void Finish(SpineUnitView view)
        {
            _inFlight.Remove(view);
            if (view != null) view.Dispose();
        }

        // 매치 teardown · 씬 언로드 · 컴포넌트 비활성 — 진행 중 비행을 무효화하고 뷰를 치운다.
        // 풀이 더 이상 모르는 뷰라 여기서 안 치우면 **고아 GameObject 로 남는다**(Detach 의 계약).
        private void OnDisable()
        {
            _gen++;
            StopAllCoroutines();
            for (int i = 0; i < _inFlight.Count; i++)
                if (_inFlight[i] != null) _inFlight[i].Dispose();
            _inFlight.Clear();
        }

        // 테스트용 관측점 — 진행 중인 퇴근 비행 수.
        public int InFlightCount => _inFlight.Count;
    }
}
